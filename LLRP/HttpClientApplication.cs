using LLRP.Helpers;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http;
using System.Net.Http;
using System.Net.Http.Headers;

namespace LLRP
{
    internal sealed partial class HttpClientApplication : ApplicationBase<HttpClientApplication>
    {
        private readonly HttpMessageInvoker? _client;
        private int _clientCounter = 0;
        private readonly ConnectionUriBuilder _uriBuilder;
        private readonly ConnectionHeaderValueCache _acceptHeaderCache;
        private readonly ConnectionHeaderValueCache _userAgentHeaderCache;
        private HttpRequestMessage _request;

        public HttpClientApplication() : base()
        {
            _client = HttpClientConfiguration.GetFixedClient();
            _uriBuilder = new ConnectionUriBuilder(Downstream.Uri);
            _acceptHeaderCache = new();
            _userAgentHeaderCache = new();
            _request = new HttpRequestMessage();
        }

        public override Task InitializeAsync() => Task.CompletedTask;

        private Task<HttpResponseMessage> SendAsync()
        {
            Debug.Assert(_request is not null);
            HttpMessageInvoker client = _client ?? HttpClientConfiguration.GetDynamicClient(ref _clientCounter);
            return client.SendAsync(_request, CancellationToken.None);
        }

        public override async Task ProcessRequestAsync()
        {
            using HttpResponseMessage response = await SendAsync();

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

            if (HttpClientConfiguration.ReuseHttpRequestMessage)
            {
                _request.RequestUri = null;
                _request.Headers.Clear();
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
                    writer.UnsafeWriteNoLengthCheck(Constants.ChunkedEncodingFinalChunk);
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

                    if (read == 0)
                    {
                        app.WriteToWriter(Constants.ChunkedEncodingFinalChunk);
                        return;
                    }

                    WriteChunk(app, app.ResponseContentBuffer.AsSpan(0, read));

                    await app.Writer.FlushAsync();

                    readTask = content.ReadAsync(app.ChunkedResponseContentBuffer);
                }

                static void WriteChunk(HttpClientApplication app, ReadOnlySpan<byte> chunk)
                {
                    var writer = GetWriter(app.Writer, sizeHint: chunk.Length + ChunkedEncodingMaxChunkOverhead);
                    writer.WriteChunkedEncodingChunkNoLengthCheck(chunk);
                    writer.Commit();
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
                writer.UnsafeWriteNoLengthCheck(Constants.Http11OK);
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
                    writer.Write(Constants.ColonSpace);
                    Debug.Assert(header.Value.Count <= 1);
                    writer.WriteUtf8String(header.Value.ToString());
                    writer.WriteCRLF();
                }
            }

            static void WriteStatusLineSlow(ref BufferWriter<WriterAdapter> writer, HttpResponseMessage response)
            {
                writer.Write(Constants.Http11Space);
                writer.WriteStatusCode((uint)response.StatusCode);
                writer.Write(Constants.Space);
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

            if (HttpClientConfiguration.ReuseHttpRequestMessage)
            {
                _request.Method = method;
                _request.RequestUri = uri;
            }
            else
            {
                _request = new HttpRequestMessage(method, uri);
            }
        }

        public override void OnHeader(ReadOnlySpan<byte> name, ReadOnlySpan<byte> value)
        {
            KnownHeaders.KnownHeader? header = KnownHeaders.TryGetKnownHeader(name);
            if (header is not null)
            {
                if (ReferenceEquals(header, KnownHeaders.Host))
                {
                    return;
                }

                string valueString = header.TryGetValue(value) ?? (
                    ReferenceEquals(header, KnownHeaders.Accept) ? _acceptHeaderCache.GetHeaderValue(value) :
                    ReferenceEquals(header, KnownHeaders.UserAgent) ? _userAgentHeaderCache.GetHeaderValue(value) :
                    AllocateHeaderValue(value));

                _request.Headers.TryAddWithoutValidation(header.Name, valueString);
            }
            else
            {
                _request.Headers.TryAddWithoutValidation(AllocateHeaderName(name), AllocateHeaderValue(value));
            }

            static string AllocateHeaderName(ReadOnlySpan<byte> name)
            {
                string s = Encoding.UTF8.GetString(name);
                Console.WriteLine($"Allocated header name {s}");
                return s;
            }

            static string AllocateHeaderValue(ReadOnlySpan<byte> value)
            {
                string s = Encoding.UTF8.GetString(value);
                Console.WriteLine($"Allocated header value {s}");
                return s;
            }
        }
    }
}
