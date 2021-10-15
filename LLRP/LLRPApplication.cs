using LLRP.Helpers;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http;
using Microsoft.AspNetCore.WebUtilities;
using System.Net.Http;
using System.Net.Http.LowLevel;
using System.Net.Sockets;

namespace LLRP
{
    public sealed class DownstreamAddress
    {
        public readonly AsciiString Authority;
        public readonly AsciiString PathPrefix;
        public readonly bool NoPathPrefix;
        public readonly DnsEndPoint EndPoint;

        public DownstreamAddress(Uri uri)
        {
            Authority = uri.Authority;
            PathPrefix = uri.AbsolutePath;
            NoPathPrefix = uri.AbsolutePath.AsSpan().TrimStart('/').Length == 0;
            EndPoint = new DnsEndPoint(uri.Host, uri.Port);
        }
    }

    public sealed partial class LLRPApplication : IHttpHeadersSink
    {
        public static DownstreamAddress[] DownstreamAddresses = { new DownstreamAddress(new Uri("http://httpbin.org/anything/A")) };
        private static int _connectionCounter;
        private readonly int _connectionCount;

        private static readonly AsciiString _crlf = "\r\n";
        private static readonly AsciiString _http11Space = "HTTP/1.1 ";
        private static readonly AsciiString _chunkedEncodingFinalChunk = "0" + _crlf + _crlf;

        private static ReadOnlySpan<byte> Http11OK => new byte[]
        {
            (byte)'H', (byte)'T', (byte)'T', (byte)'P', (byte)'/', (byte)'1', (byte)'.', (byte)'1',  (byte)' ',
            (byte)'2', (byte)'0', (byte)'0', (byte)' ', (byte)'O', (byte)'K', (byte)'\r', (byte)'\n'
        };
        private static ReadOnlySpan<byte> ChunkedEncodingFinalChunk => new byte[]
        {
            (byte)'0', (byte)'\r', (byte)'n', (byte)'\r', (byte)'\n'
        };

        private static ReadOnlySpan<byte> EncodedTransferEncodingName => new byte[] { (byte)'t', (byte)'r', (byte)'a', (byte)'n', (byte)'s', (byte)'f', (byte)'e', (byte)'r', (byte)'-', (byte)'e', (byte)'n', (byte)'c', (byte)'o', (byte)'d', (byte)'i', (byte)'n', (byte)'g' };
        private static ReadOnlySpan<byte> EncodedTransferEncodingChunkedValue => new byte[] { (byte)'c', (byte)'h', (byte)'u', (byte)'n', (byte)'k', (byte)'e', (byte)'d' };

        private const int CRLF = 2;
        private const int ChunkedEncodingMaxChunkLengthDigits = 4;
        private const int ChunkedEncodingMaxChunkOverhead = ChunkedEncodingMaxChunkLengthDigits + CRLF + CRLF;
        private const int ChunkedEncodingFinalChunkLength = 1 + CRLF + CRLF;
        private const int ChunkedEncodingMaxOverhead = ChunkedEncodingMaxChunkOverhead + ChunkedEncodingFinalChunkLength;

        private const int ResponseContentBufferLength = 4096;
        private readonly byte[] _responseContentBuffer = new byte[ResponseContentBufferLength];
        private readonly Memory<byte> _responseContentBufferMemory;
        private readonly Memory<byte> _chunkedResponseContentBuffer;

        private readonly AsciiString _authority;

        private readonly bool _noPathPrefix;
        private readonly int _pathAndQueryOffset;
        private byte[] _pathAndQueryBuffer;

        private readonly DownstreamAddress _downstream;
        private Http1Connection _connection;
        private ValueHttpRequest _request;
        private bool _isChunkedResponse;

        public LLRPApplication()
        {
            _responseContentBufferMemory = _responseContentBuffer.AsMemory();
            _chunkedResponseContentBuffer = _responseContentBufferMemory.Slice(0, ResponseContentBufferLength - ChunkedEncodingMaxOverhead);

            _connectionCount = Interlocked.Increment(ref _connectionCounter);
            _downstream = DownstreamAddresses[_connectionCount % DownstreamAddresses.Length];

            _authority = _downstream.Authority;
            _noPathPrefix = _downstream.NoPathPrefix;

            if (!_noPathPrefix)
            {
                ReadOnlySpan<byte> pathPrefix = _downstream.PathPrefix.AsSpan();
                _pathAndQueryBuffer = new byte[Math.Max(4096, pathPrefix.Length)];
                pathPrefix.CopyTo(_pathAndQueryBuffer);
                _pathAndQueryOffset = pathPrefix.Length;
            }
        }

        public async Task InitializeAsync()
        {
            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };

            try
            {
                await socket.ConnectAsync(_downstream.EndPoint);
            }
            catch
            {
                socket.Dispose();
                throw;
            }

            var networkStream = new EnhancedNetworkStream(socket, ownsSocket: true);
            _connection = new Http1Connection(networkStream, HttpPrimitiveVersion.Version11);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteToWriter(ReadOnlySpan<byte> buffer)
        {
            Span<byte> destination = Writer.GetSpan(buffer.Length);
            Unsafe.CopyBlockUnaligned(ref MemoryMarshal.GetReference(destination), ref MemoryMarshal.GetReference(buffer), (uint)buffer.Length);
            Writer.Advance(buffer.Length);
        }

        public void OnHeader(object? state, ReadOnlySpan<byte> headerName, ReadOnlySpan<byte> headerValue)
        {
            if (headerName.Length == 17 &&
                EqualsIgnoreCase(headerName, EncodedTransferEncodingName) &&
                EqualsIgnoreCase(headerValue, EncodedTransferEncodingChunkedValue))
            {
                _isChunkedResponse = true;
            }

            int length = headerName.Length + headerValue.Length + 4;
            EncodeHeader(headerName, headerValue, ref MemoryMarshal.GetReference(Writer.GetSpan(length)));
            Writer.Advance(length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void EncodeHeader(ReadOnlySpan<byte> name, ReadOnlySpan<byte> value, ref byte pBuf)
        {
            Unsafe.CopyBlockUnaligned(ref pBuf, ref MemoryMarshal.GetReference(name), (uint)name.Length);
            pBuf = ref Unsafe.AddByteOffset(ref pBuf, (nuint)name.Length);

            if (BitConverter.IsLittleEndian) Unsafe.WriteUnaligned(ref pBuf, (ushort)0x203A);
            else Unsafe.WriteUnaligned(ref pBuf, (ushort)0x3A20);
            pBuf = ref Unsafe.AddByteOffset(ref pBuf, 2);

            if (value.Length != 0)
            {
                Unsafe.CopyBlockUnaligned(ref pBuf, ref MemoryMarshal.GetReference(value), (uint)value.Length);
                pBuf = ref Unsafe.AddByteOffset(ref pBuf, (nuint)value.Length);
            }

            if (BitConverter.IsLittleEndian) Unsafe.WriteUnaligned(ref pBuf, (ushort)0x0A0D);
            else Unsafe.WriteUnaligned(ref pBuf, (ushort)0x0D0Au);
        }

        private static bool EqualsIgnoreCase(ReadOnlySpan<byte> wireValue, ReadOnlySpan<byte> expectedValueLowerCase)
        {
            if (wireValue.Length != expectedValueLowerCase.Length) return false;

            ref byte xRef = ref MemoryMarshal.GetReference(wireValue);
            ref byte yRef = ref MemoryMarshal.GetReference(expectedValueLowerCase);

            for (uint i = 0; i < (uint)wireValue.Length; ++i)
            {
                byte xv = Unsafe.Add(ref xRef, (IntPtr)i);

                if ((xv - (uint)'A') <= ('Z' - 'A'))
                {
                    xv |= 0x20;
                }

                if (xv != Unsafe.Add(ref yRef, (IntPtr)i)) return false;
            }

            return true;
        }

        public async Task ProcessRequestAsync()
        {
            await _request.CompleteRequestAsync();

            await _request.ReadToHeadersAsync();

            WriteResponseStatusLine();

            await _request.ReadHeadersAsync(this, null);

            await _request.ReadToContentAsync();

            WriteToWriter(_crlf);

            if (_isChunkedResponse)
            {
                await CopyChunkedResponseContent();
            }
            else
            {
                await CopyRawResponseContent();
            }

            await _request.DisposeAsync();
        }

        private Task CopyChunkedResponseContent()
        {
            ValueTask<int> readTask = _request.ReadContentAsync(_chunkedResponseContentBuffer);

            if (readTask.IsCompletedSuccessfully)
            {
                int read = readTask.GetAwaiter().GetResult();

                var writer = GetWriter(Writer, sizeHint: read + ChunkedEncodingMaxOverhead);

                if (read != 0)
                {
                    writer.WriteChunkedEncodingChunk(_responseContentBuffer.AsSpan(0, read));

                    readTask = _request.ReadContentAsync(_chunkedResponseContentBuffer);

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
                    writer.UnsafeWriteNoLengthCheck(_chunkedEncodingFinalChunk);
                }

                writer.Commit();

                if (read == 0)
                {
                    return Task.CompletedTask;
                }
            }

            return WaitAndCopyAsync(this, readTask);

            static async Task WaitAndCopyAsync(LLRPApplication app, ValueTask<int> readTask)
            {
                while (true)
                {
                    int read = await readTask;

                    WriteChunk(app, app._responseContentBuffer.AsSpan(0, read));

                    if (read == 0)
                    {
                        return;
                    }

                    await app.Writer.FlushAsync();

                    readTask = app._request.ReadContentAsync(app._chunkedResponseContentBuffer);
                }

                static void WriteChunk(LLRPApplication app, ReadOnlySpan<byte> chunk)
                {
                    if (chunk.Length == 0)
                    {
                        app.WriteToWriter(ChunkedEncodingFinalChunk);
                    }
                    else
                    {
                        var writer = GetWriter(app.Writer, sizeHint: chunk.Length + ChunkedEncodingMaxChunkOverhead);
                        writer.WriteChunkedEncodingChunk(chunk);
                        writer.Commit();
                    }
                }
            }
        }

        private Task CopyRawResponseContent()
        {
            ValueTask<int> readTask = _request.ReadContentAsync(_chunkedResponseContentBuffer);

            if (readTask.IsCompletedSuccessfully)
            {
                int read = readTask.GetAwaiter().GetResult();

                if (read != 0)
                {
                    WriteToWriter(_responseContentBuffer.AsSpan(0, read));

                    readTask = _request.ReadContentAsync(_chunkedResponseContentBuffer);

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

            return WaitAndCopyAsync(this, readTask);

            static async Task WaitAndCopyAsync(LLRPApplication app, ValueTask<int> readTask)
            {
                while (true)
                {
                    int read = await readTask;
                    if (read == 0)
                    {
                        return;
                    }

                    await app.Writer.WriteAsync(app._responseContentBufferMemory.Slice(0, read));

                    readTask = app._request.ReadContentAsync(app._responseContentBufferMemory);
                }
            }
        }

        private void WriteResponseStatusLine()
        {
            if (_request.StatusCode == HttpStatusCode.OK)
            {
                WriteToWriter(Http11OK);
            }
            else
            {
                WriteStatusLineSlow(this);
            }

            static void WriteStatusLineSlow(LLRPApplication app)
            {
                HttpStatusCode statusCode = app._request.StatusCode;
                var writer = GetWriter(app.Writer, sizeHint: 64);
                writer.Write(_http11Space);
                writer.WriteNumeric((uint)statusCode);
                writer.Write((byte)' ');
                writer.WriteUtf8String(ReasonPhrases.GetReasonPhrase((int)statusCode));
                writer.Write(_crlf);
                writer.Commit();
            }
        }

        public void OnStartLine(HttpVersionAndMethod versionAndMethod, TargetOffsetPathLength targetPath, Span<byte> startLine)
        {
            ValueTask<ValueHttpRequest?> requestTask = _connection.CreateNewRequestAsync(HttpPrimitiveVersion.Version11, HttpVersionPolicy.RequestVersionExact);
            Debug.Assert(requestTask.IsCompletedSuccessfully);
            ValueHttpRequest? request = requestTask.GetAwaiter().GetResult();
            Debug.Assert(request.HasValue);
            _request = request.Value;

            Span<byte> pathAndQuery = startLine.Slice(targetPath.Offset);

            if (!_noPathPrefix)
            {
                pathAndQuery = GetPathAndQueryWithPrefix(pathAndQuery);
            }

            _request.WriteRequestStart(
                startLine.Slice(0, versionAndMethod.MethodEnd),
                _authority.AsSpan(),
                pathAndQuery);

            _request.ConfigureRequest(hasContentLength: true, hasTrailingHeaders: false);
            _isChunkedResponse = false;
        }

        private Span<byte> GetPathAndQueryWithPrefix(Span<byte> pathAndQuery)
        {
            int length = _pathAndQueryOffset + pathAndQuery.Length;

            if (_pathAndQueryBuffer.Length < length)
            {
                Array.Resize(ref _pathAndQueryBuffer, Math.Max(length, _pathAndQueryBuffer.Length * 2));
            }

            Unsafe.CopyBlockUnaligned(
                ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_pathAndQueryBuffer), _pathAndQueryOffset),
                ref MemoryMarshal.GetReference(pathAndQuery),
                (uint)pathAndQuery.Length);

            return _pathAndQueryBuffer.AsSpan(0, length);
        }

        public void OnHeader(ReadOnlySpan<byte> name, ReadOnlySpan<byte> value)
        {
            if (name.Length == 4)
            {
                uint host = Unsafe.ReadUnaligned<uint>(ref MemoryMarshal.GetReference(name));
                host |= 0x20202020;

                if (BitConverter.IsLittleEndian)
                {
                    const uint Host = 'h' | ('o' << 8) | ('s' << 16) | ('t' << 24);
                    if (Host == host)
                    {
                        return;
                    }
                }
                else
                {
                    const uint Host = ('h' << 24) | ('o' << 16) | ('s' << 8) | 't';
                    if (Host == host)
                    {
                        return;
                    }
                }
            }

            _request.WriteHeader(name, value);
        }

        public void OnHeadersComplete(bool endStream)
        {

        }
    }
}
