﻿using LLRP.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using Yarp.ReverseProxy.Forwarder;

namespace LLRP
{
    internal static class ForwarderSetup
    {
        public static readonly IHttpForwarder Forwarder = CreateForwarder();

        private static IHttpForwarder CreateForwarder()
        {
            var services = new ServiceCollection();
            services.AddReverseProxy();
            services.AddLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Critical);
            });
            var provider = services.BuildServiceProvider();
            return provider.GetRequiredService<IHttpForwarder>();
        }
    }

    internal sealed partial class YarpApplication : ApplicationBase<YarpApplication>
    {
        internal sealed class HttpContextResponseStream : Stream
        {
            private readonly Stream _innerStream;
            private HttpContext _context = null!;
            private bool _sentHeader = false;
            private byte[] _successfulResponseBuffer;
            private byte[] _responseBuffer;

            public HttpContextResponseStream(Stream stream)
            {
                _innerStream = stream;
                _successfulResponseBuffer = new byte[1024];
                _responseBuffer = new byte[1024];
                Constants.Http11OK.CopyTo(_successfulResponseBuffer);
                Constants.Http11Space.CopyTo(_responseBuffer);
            }

            public void SetContext(HttpContext context)
            {
                _context = context;
                _sentHeader = false;
            }

            public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            {
                if (_sentHeader)
                {
                    return _innerStream.WriteAsync(buffer, cancellationToken);
                }

                _sentHeader = true;

                return SendHeaderAndWriteAsync(buffer, cancellationToken);
            }

            private async ValueTask SendHeaderAndWriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            {
                await _innerStream.WriteAsync(SerializeResponse(), cancellationToken);

                await _innerStream.WriteAsync(buffer, cancellationToken);
            }

            private ReadOnlyMemory<byte> SerializeResponse()
            {
                HttpResponse response = _context.Response;

                byte[] buffer;
                int offset;
                if (response.StatusCode == 200)
                {
                    buffer = _successfulResponseBuffer;
                    offset = Constants.Http11OK.Length;
                }
                else
                {
                    buffer = _responseBuffer;
                    offset = Constants.Http11Space.Length;
                    offset += WriteStatusLineSlow(buffer.AsSpan(offset), response);
                }

                foreach (var header in response.Headers)
                {
                    foreach (char c in header.Key)
                    {
                        buffer[offset++] = (byte)c;
                    }
                    buffer[offset++] = (byte)':';
                    buffer[offset++] = (byte)' ';
                    foreach (char c in header.Value.ToString())
                    {
                        buffer[offset++] = (byte)c;
                    }
                    buffer[offset++] = (byte)'\r';
                    buffer[offset++] = (byte)'\n';
                }

                buffer[offset++] = (byte)'\r';
                buffer[offset++] = (byte)'\n';

                return buffer.AsMemory(0, offset);

                static int WriteStatusLineSlow(Span<byte> buffer, HttpResponse response)
                {
                    Utf8Formatter.TryFormat(response.StatusCode, buffer, out int offset);
                    buffer[offset++] = (byte)' ';
                    offset += Encoding.UTF8.GetBytes(ReasonPhrases.GetReasonPhrase(response.StatusCode), buffer.Slice(offset));
                    buffer[offset++] = (byte)'\r';
                    buffer[offset++] = (byte)'\n';
                    return offset;
                }
            }

            public override void Flush() => throw new NotImplementedException();
            public override int Read(byte[] buffer, int offset, int count) => throw new NotImplementedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotImplementedException();
            public override void SetLength(long value) => throw new NotImplementedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotImplementedException();
            public override bool CanRead => throw new NotImplementedException();
            public override bool CanSeek => throw new NotImplementedException();
            public override bool CanWrite => throw new NotImplementedException();
            public override long Length => throw new NotImplementedException();
            public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        }

        private DefaultHttpContext _context = null!;
        private HttpRequest _request = null!;
        private IHeaderDictionary _requestHeaders = null!;
        private readonly ConnectionHeaderValueCache _acceptHeaderCache;
        private readonly ConnectionHeaderValueCache _userAgentHeaderCache;
        private readonly byte[] _pathAndQueryBuffer = new byte[1024];
        private int _pathAndQueryLength;
        private PathString _path;
        private QueryString _query;
        private readonly string _destinationPrefix;
        private readonly HttpMessageInvoker _invoker;
        private readonly ForwarderRequestConfig _requestConfig;
        private readonly HttpTransformer _transformer;
        private HttpContextResponseStream? _bodyWriterStream;

        public YarpApplication() : base()
        {
            _acceptHeaderCache = new();
            _userAgentHeaderCache = new();
            _destinationPrefix = Downstream.Uri.AbsoluteUri;
            _invoker = HttpClientConfiguration.CreateClient();
            _requestConfig = new();
            _transformer = HttpTransformer.Default;
        }

        public override Task InitializeAsync() => Task.CompletedTask;

        public override Task ProcessRequestAsync()
        {
            return ForwarderSetup.Forwarder.SendAsync(_context, _destinationPrefix, _invoker, _requestConfig, _transformer).AsTask();
        }

        public override void OnStartLine(HttpVersionAndMethod versionAndMethod, TargetOffsetPathLength targetPath, Span<byte> startLine)
        {
            _context = new DefaultHttpContext();
            _request = _context.Request;
            _requestHeaders = _request.Headers;
            _context.Response.Body = _bodyWriterStream ??= new HttpContextResponseStream(Writer.AsStream());

            _bodyWriterStream.SetContext(_context);

            _request.Method = versionAndMethod.Method == Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http.HttpMethod.Get
                ? HttpMethods.Get
                : HttpMethods.GetCanonicalizedValue(versionAndMethod.Method.ToString());

            _request.Protocol = versionAndMethod.Version switch
            {
                Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http.HttpVersion.Http11 => HttpProtocol.Http11,
                Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http.HttpVersion.Http2 => HttpProtocol.Http2,
                Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http.HttpVersion.Http3 => HttpProtocol.Http3,
                Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http.HttpVersion.Http10 => HttpProtocol.Http10,
                _ => HttpProtocol.Http11
            };

            ReadOnlySpan<byte> pathAndQuery = startLine.Slice(targetPath.Offset);
            if (!pathAndQuery.SequenceEqual(_pathAndQueryBuffer.AsSpan(0, _pathAndQueryLength)))
            {
                int query = pathAndQuery.IndexOf((byte)'?');
                if (query == -1)
                {
                    _path = Encoding.UTF8.GetString(pathAndQuery);
                }
                else
                {
                    _path = Encoding.UTF8.GetString(pathAndQuery.Slice(0, query));
                    _query = new QueryString(Encoding.UTF8.GetString(pathAndQuery.Slice(query)));
                }

                pathAndQuery.CopyTo(_pathAndQueryBuffer);
                _pathAndQueryLength = pathAndQuery.Length;
            }

            _request.Path = _path;
            _request.QueryString = _query;
        }

        public override void OnHeader(ReadOnlySpan<byte> name, ReadOnlySpan<byte> value)
        {
            KnownHeaders.KnownHeader? header = KnownHeaders.TryGetKnownHeader(name);
            if (header is not null)
            {
                string valueString = header.TryGetValue(value) ?? (
                    ReferenceEquals(header, KnownHeaders.Accept) ? _acceptHeaderCache.GetHeaderValue(value) :
                    ReferenceEquals(header, KnownHeaders.UserAgent) ? _userAgentHeaderCache.GetHeaderValue(value) :
                    Encoding.UTF8.GetString(value));

                _requestHeaders.Append(header.Name, valueString);
            }
            else
            {
                _requestHeaders.Append(Encoding.ASCII.GetString(name), Encoding.UTF8.GetString(value));
            }
        }
    }
}