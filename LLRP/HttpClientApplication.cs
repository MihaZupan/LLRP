using LLRP.Helpers;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http;
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

        private static ReadOnlySpan<byte> Space => new byte[]
        {
            (byte)' '
        };
        private static ReadOnlySpan<byte> ColonSpace => new byte[]
        {
            (byte)':', (byte)' '
        };

        private readonly ConnectionUriBuilder _uriBuilder;
        private HttpRequestMessage? _request;
        private HttpHeaders? _requestHeaders;

        public HttpClientApplication() : base()
        {
            _uriBuilder = new ConnectionUriBuilder(Downstream.Uri);
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
            ValueTask<int> readTask = content.ReadAsync(ChunkedResponseContentBuffer);

            if (readTask.IsCompletedSuccessfully)
            {
                int read = readTask.GetAwaiter().GetResult();

                var writer = GetWriter(Writer, sizeHint: read + ChunkedEncodingMaxOverhead);

                if (read != 0)
                {
                    writer.WriteChunkedEncodingChunkNoLengthCheck(ResponseContentBuffer.AsSpan(0, read));

                    readTask = content.ReadAsync(ChunkedResponseContentBuffer);

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
                    writer.UnsafeWriteNoLengthCheck(ChunkedEncodingFinalChunk);
                }

                writer.Commit();

                if (read == 0)
                {
                    return Task.CompletedTask;
                }
            }

            return WaitAndCopyAsync(this, content, readTask);

            static async Task WaitAndCopyAsync(HttpClientApplication app, Stream content, ValueTask<int> readTask)
            {
                while (true)
                {
                    int read = await readTask;

                    WriteChunk(app, app.ResponseContentBuffer.AsSpan(0, read));

                    if (read == 0)
                    {
                        return;
                    }

                    await app.Writer.FlushAsync();

                    readTask = content.ReadAsync(app.ChunkedResponseContentBuffer);
                }

                static void WriteChunk(HttpClientApplication app, ReadOnlySpan<byte> chunk)
                {
                    if (chunk.Length == 0)
                    {
                        app.WriteToWriter(ChunkedEncodingFinalChunk);
                    }
                    else
                    {
                        var writer = GetWriter(app.Writer, sizeHint: chunk.Length + ChunkedEncodingMaxChunkOverhead);
                        writer.WriteChunkedEncodingChunkNoLengthCheck(chunk);
                        writer.Commit();
                    }
                }
            }
        }

        private Task CopyRawResponseContent(Stream content)
        {
            ValueTask<int> readTask = content.ReadAsync(ResponseContentBufferMemory);

            if (readTask.IsCompletedSuccessfully)
            {
                int read = readTask.GetAwaiter().GetResult();

                if (read != 0)
                {
                    WriteToWriter(ResponseContentBuffer.AsSpan(0, read));

                    readTask = content.ReadAsync(ResponseContentBufferMemory);

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
                    return Task.CompletedTask;
                }
            }

            return WaitAndCopyAsync(this, content, readTask);

            static async Task WaitAndCopyAsync(HttpClientApplication app, Stream content, ValueTask<int> readTask)
            {
                while (true)
                {
                    int read = await readTask;
                    if (read == 0)
                    {
                        return;
                    }

                    await app.Writer.WriteAsync(app.ResponseContentBufferMemory.Slice(0, read));

                    readTask = content.ReadAsync(app.ResponseContentBufferMemory);
                }
            }
        }

        private static void WriteResponseLineAndHeaders(HttpResponseMessage response, PipeWriter pipeWriter)
        {
            var writer = GetWriter(pipeWriter, sizeHint: 1024);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                writer.UnsafeWriteNoLengthCheck(Http11OK);
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

            writer.WriteCRLF();

            writer.Commit();

            static void WriteHeaders(ref BufferWriter<WriterAdapter> writer, HttpHeaders headers)
            {
                var enumerator = headers.NonValidated.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    var header = enumerator.Current;
                    writer.WriteAsciiString(header.Key);
                    writer.Write(ColonSpace);
                    Debug.Assert(header.Value.Count <= 1);
                    writer.WriteUtf8String(header.Value.ToString());
                    writer.WriteCRLF();
                }
            }

            static void WriteStatusLineSlow(ref BufferWriter<WriterAdapter> writer, HttpResponseMessage response)
            {
                writer.Write(Http11Space);
                writer.WriteNumeric((uint)response.StatusCode);
                writer.Write(Space);
                writer.WriteUtf8String(response.ReasonPhrase ?? "Unknown");
                writer.WriteCRLF();
            }
        }

        public override void OnStartLine(HttpVersionAndMethod versionAndMethod, TargetOffsetPathLength targetPath, Span<byte> startLine)
        {
            System.Net.Http.HttpMethod method =
                HttpMethodHelper.TryConvertMethod(versionAndMethod.Method) ??
                HttpMethodHelper.GetMethod(startLine.Slice(0, versionAndMethod.MethodEnd));

            Uri uri = _uriBuilder.CreateUri(startLine.Slice(targetPath.Offset));

            _request = new HttpRequestMessage(method, uri);
            _requestHeaders = _request.Headers;
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
                _requestHeaders.TryAddWithoutValidation(Encoding.ASCII.GetString(name), Encoding.UTF32.GetString(value));
            }
        }
    }
}
