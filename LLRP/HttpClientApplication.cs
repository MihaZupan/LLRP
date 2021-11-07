using LLRP.Helpers;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http;
using System.IO;
using System.IO.Pipelines;
using System.Net.Http;
using System.Net.Http.Headers;

namespace LLRP
{
    internal sealed partial class HttpClientApplication : ApplicationBase<HttpClientApplication>
    {
        private static readonly HttpMessageInvoker _client = new(new SocketsHttpHandler()
        {
            UseProxy = false,
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
            UseCookies = false
        });
        private static readonly Encoding _utf8 = Encoding.UTF8;

        private static readonly AsciiString _space = " ";
        private static readonly AsciiString _colonSpace = ": ";
        private static readonly AsciiString _crlf = "\r\n";
        private static readonly AsciiString _http11Space = "HTTP/1.1 ";
        private static readonly AsciiString _http11OK = _http11Space + "200 OK" + _crlf;
        private static readonly AsciiString _chunkedEncodingFinalChunk = "0" + _crlf + _crlf;

        private const int CRLF = 2;
        private const int ChunkedEncodingMaxChunkLengthDigits = 4;
        private const int ChunkedEncodingFinalChunkLength = 1 + CRLF + CRLF;
        private const int ChunkedEncodingMaxChunkOverhead = ChunkedEncodingMaxChunkLengthDigits + CRLF + CRLF;
        private const int ChunkedEncodingMaxOverhead = ChunkedEncodingMaxChunkOverhead + ChunkedEncodingFinalChunkLength;

        private const int ResponseContentBufferLength = 4096;
        private readonly byte[] _responseContentBuffer = new byte[ResponseContentBufferLength];
        private readonly Memory<byte> _chunkedResponseContentBuffer;

        private readonly DownstreamAddress _downstream;
        private readonly string _downstreamBase;
        private readonly int _downstreamSlashSkipOffset;
        private HttpRequestMessage? _request;
        private HttpHeaders? _requestHeaders;

        public HttpClientApplication()
        {
            _chunkedResponseContentBuffer = _responseContentBuffer.AsMemory(0, ResponseContentBufferLength - ChunkedEncodingMaxOverhead);
            _downstream = DownstreamAddress.GetNextAddress();
            _downstreamBase = _downstream.Uri.AbsoluteUri;
            _downstreamSlashSkipOffset = _downstreamBase.EndsWith('/') ? 1 : 0;
        }

        public override Task InitializeAsync() => Task.CompletedTask;

        public override async Task ProcessRequestAsync()
        {
            Debug.Assert(_request is not null);

            using HttpResponseMessage response = await _client.SendAsync(_request, CancellationToken.None);

            WriteResponseLineAndHeaders(response, Writer);

            using Stream responseStream = response.Content.ReadAsStream();
            if (response.Headers.TransferEncodingChunked == true)
            {
                await CopyChunkedResponseContent(responseStream);
            }
            else
            {
                await CopyRawResponseContent(responseStream);
            }
        }

        private Task CopyChunkedResponseContent(Stream content)
        {
            Memory<byte> buffer = _chunkedResponseContentBuffer;

            ValueTask<int> readTask = content.ReadAsync(buffer);

            if (readTask.IsCompletedSuccessfully)
            {
                int read = readTask.GetAwaiter().GetResult();

                var writer = GetWriter(Writer, sizeHint: read + ChunkedEncodingMaxOverhead);

                if (read != 0)
                {
                    writer.WriteHexNumber((uint)read);
                    writer.Write(_crlf);
                    writer.Write(buffer.Span.Slice(0, read));
                    writer.Write(_crlf);

                    readTask = content.ReadAsync(buffer);

                    if (readTask.IsCompletedSuccessfully)
                    {
                        read = readTask.GetAwaiter().GetResult();

                        if (read != 0)
                        {
                            readTask = new ValueTask<int>(read);
                        }
                    }
                }

                if (read == 0)
                {
                    writer.Write(_chunkedEncodingFinalChunk);
                }

                writer.Commit();

                if (read == 0)
                {
                    return Task.CompletedTask;
                }
            }

            return WaitAndCopyAsync(Writer, content, buffer, readTask);

            static async Task WaitAndCopyAsync(PipeWriter pipeWriter, Stream content, Memory<byte> buffer, ValueTask<int> readTask)
            {
                while (true)
                {
                    int read = await readTask;

                    WriteChunk(pipeWriter, buffer.Span.Slice(0, read));

                    if (read == 0)
                    {
                        return;
                    }

                    await pipeWriter.FlushAsync();

                    readTask = content.ReadAsync(buffer);
                }

                static void WriteChunk(PipeWriter pipeWriter, ReadOnlySpan<byte> chunk)
                {
                    var writer = GetWriter(pipeWriter, sizeHint: chunk.Length + ChunkedEncodingMaxChunkOverhead);

                    if (chunk.Length == 0)
                    {
                        writer.Write(_chunkedEncodingFinalChunk);
                    }
                    else
                    {
                        writer.WriteHexNumber((uint)chunk.Length);
                        writer.Write(_crlf);
                        writer.Write(chunk);
                        writer.Write(_crlf);
                    }

                    writer.Commit();
                }
            }
        }

        private Task CopyRawResponseContent(Stream content)
        {
            byte[] buffer = _responseContentBuffer;

            while (true)
            {
                ValueTask<int> readTask = content.ReadAsync(buffer);

                if (readTask.IsCompletedSuccessfully)
                {
                    int read = readTask.GetAwaiter().GetResult();
                    if (read == 0)
                    {
                        return Task.CompletedTask;
                    }

                    Span<byte> writerBuffer = Writer.GetSpan(read);
                    if (read <= writerBuffer.Length)
                    {
                        buffer.AsSpan(0, read).CopyTo(writerBuffer);
                        Writer.Advance(read);
                        continue;
                    }

                    readTask = new ValueTask<int>(read);
                }

                return WaitAndCopyAsync(Writer, content, buffer, readTask);
            }

            static async Task WaitAndCopyAsync(PipeWriter pipeWriter, Stream content, Memory<byte> buffer, ValueTask<int> readTask)
            {
                while (true)
                {
                    int read = await readTask;
                    if (read == 0)
                    {
                        return;
                    }

                    await pipeWriter.WriteAsync(buffer.Slice(0, read));

                    readTask = content.ReadAsync(buffer);
                }
            }
        }

        private static void WriteResponseLineAndHeaders(HttpResponseMessage response, PipeWriter pipeWriter)
        {
            var writer = GetWriter(pipeWriter, sizeHint: 2560);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                writer.Write(_http11OK);
            }
            else
            {
                WriteStatusLineSlow(ref writer, response);
            }

            WriteHeaders(ref writer, response.Headers);
            if (response.Content is HttpContent content)
            {
                WriteHeaders(ref writer, content.Headers);
            }

            writer.Write(_crlf);

            writer.Commit();

            static void WriteHeaders(ref BufferWriter<WriterAdapter> writer, HttpHeaders headers)
            {
                var enumerator = headers.NonValidated.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    var header = enumerator.Current;
                    writer.WriteAsciiString(header.Key);
                    writer.Write(_colonSpace);
                    writer.WriteUtf8String(header.Value.ToString());
                    writer.Write(_crlf);
                }
            }

            static void WriteStatusLineSlow(ref BufferWriter<WriterAdapter> writer, HttpResponseMessage response)
            {
                writer.Write(_http11Space);
                writer.WriteNumeric((uint)response.StatusCode);
                writer.Write(_space);
                writer.WriteUtf8String(response.ReasonPhrase ?? "Unknown");
                writer.Write(_crlf);
            }
        }

        public override void OnStartLine(HttpVersionAndMethod versionAndMethod, TargetOffsetPathLength targetPath, Span<byte> startLine)
        {
            System.Net.Http.HttpMethod method =
                HttpMethodHelper.TryConvertMethod(versionAndMethod.Method) ??
                HttpMethodHelper.GetMethod(startLine.Slice(0, versionAndMethod.MethodEnd));

            string uriString = GetUriString(startLine.Slice(targetPath.Offset));
            var uri = new Uri(uriString, UriKind.Absolute);

            _request = new HttpRequestMessage(method, uri);
            _requestHeaders = _request.Headers;
        }

        private string GetUriString(ReadOnlySpan<byte> pathAndQuery)
        {
            Debug.Assert(pathAndQuery.Length > 0 && pathAndQuery[0] == '/');

            Span<char> pathAndQueryBuffer = pathAndQuery.Length < 256
                ? stackalloc char[256]
                : new char[pathAndQuery.Length];

            int charCount = _utf8.GetChars(pathAndQuery, pathAndQueryBuffer);

            return string.Concat(_downstreamBase, pathAndQueryBuffer.Slice(_downstreamSlashSkipOffset, charCount));
        }

        public override void OnHeader(ReadOnlySpan<byte> name, ReadOnlySpan<byte> value)
        {
            Debug.Assert(_requestHeaders is not null);

            KnownHeaders.KnownHeader? header = KnownHeaders.TryGetKnownHeader(name);
            if (header is not null)
            {
                if (ReferenceEquals(header, KnownHeaders.Host))
                {
                    return;
                }

                _requestHeaders.TryAddWithoutValidation(header.Name, header.GetValue(value));
            }
            else
            {
                _requestHeaders.TryAddWithoutValidation(_utf8.GetString(name), _utf8.GetString(value));
            }
        }
    }
}
