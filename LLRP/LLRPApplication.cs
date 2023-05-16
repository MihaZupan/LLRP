using LLRP.Helpers;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http;
using Microsoft.AspNetCore.WebUtilities;
using System.Net.Http;
using System.Net.Http.LowLevel;
using System.Net.Sockets;

namespace LLRP
{
    internal sealed class LLRPApplication : ApplicationBase<LLRPApplication>, IHttpHeadersSink
    {
        private readonly byte[] _authority;
        private readonly bool _noPathPrefix;
        private readonly int _pathAndQueryOffset;
        private byte[] _pathAndQueryBuffer;

        private Http1Connection _connection;
        private ValueHttpRequest _request;
        private bool _isChunkedResponse;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor.
        public LLRPApplication() : base()
#pragma warning restore CS8618
        {
            _authority = Downstream.Authority;
            _noPathPrefix = Downstream.NoPathPrefix;

            if (!_noPathPrefix)
            {
                ReadOnlySpan<byte> pathPrefix = Downstream.PathPrefix.AsSpan();
                _pathAndQueryBuffer = new byte[Math.Max(256, pathPrefix.Length)];
                pathPrefix.CopyTo(_pathAndQueryBuffer);
                _pathAndQueryOffset = pathPrefix.Length;
            }
        }

        public override async Task InitializeAsync()
        {
            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };

            try
            {
                await socket.ConnectAsync(Downstream.EndPoint);
            }
            catch
            {
                socket.Dispose();
                throw;
            }

            var networkStream = new EnhancedNetworkStream(socket, ownsSocket: true);
            _connection = new Http1Connection(networkStream, HttpPrimitiveVersion.Version11);
        }

        public void OnHeader(object? state, ReadOnlySpan<byte> headerName, ReadOnlySpan<byte> headerValue)
        {
            if (headerName.Length == 17 &&
                headerName.EqualsIgnoreCase(Constants.EncodedTransferEncodingName) &&
                headerValue.EqualsIgnoreCase(Constants.EncodedTransferEncodingChunkedValue))
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

            BufferExtensions.WriteCRLF(ref pBuf);
        }

        public override async Task ProcessRequestAsync()
        {
            await _request.CompleteRequestAsync();

            await _request.ReadToHeadersAsync();

            WriteResponseStatusLine();

            await _request.ReadHeadersAsync(this, null);

            await _request.ReadToContentAsync();

            BufferExtensions.WriteCRLF(ref MemoryMarshal.GetReference(Writer.GetSpan(2)));
            Writer.Advance(2);

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
            ValueTask<int> readTask = _request.ReadContentAsync(ChunkedResponseContentBuffer);

            if (readTask.IsCompletedSuccessfully)
            {
                int read = readTask.GetAwaiter().GetResult();

                var writer = GetWriter(Writer, sizeHint: read + ChunkedEncodingMaxOverhead);

                if (read != 0)
                {
                    writer.WriteChunkedEncodingChunkNoLengthCheck(ResponseContentBuffer.AsSpan(0, read));

                    readTask = _request.ReadContentAsync(ChunkedResponseContentBuffer);

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

            return WaitAndCopyAsync(this, readTask);

            static async Task WaitAndCopyAsync(LLRPApplication app, ValueTask<int> readTask)
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

                    readTask = app._request.ReadContentAsync(app.ChunkedResponseContentBuffer);
                }

                static void WriteChunk(LLRPApplication app, ReadOnlySpan<byte> chunk)
                {
                    var writer = GetWriter(app.Writer, sizeHint: chunk.Length + ChunkedEncodingMaxChunkOverhead);
                    writer.WriteChunkedEncodingChunkNoLengthCheck(chunk);
                    writer.Commit();
                }
            }
        }

        private Task CopyRawResponseContent()
        {
            ValueTask<int> readTask = _request.ReadContentAsync(ResponseContentBufferMemory);

            if (readTask.IsCompletedSuccessfully)
            {
                int read = readTask.GetAwaiter().GetResult();

                if (read != 0)
                {
                    WriteToWriter(ResponseContentBuffer.AsSpan(0, read));

                    readTask = _request.ReadContentAsync(ResponseContentBufferMemory);

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

                    await app.Writer.WriteAsync(app.ResponseContentBufferMemory.Slice(0, read));

                    readTask = app._request.ReadContentAsync(app.ResponseContentBufferMemory);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteResponseStatusLine()
        {
            if (_request.StatusCode == HttpStatusCode.OK)
            {
                WriteToWriter(Constants.Http11OK);
            }
            else
            {
                WriteStatusLineSlow(this);
            }

            static void WriteStatusLineSlow(LLRPApplication app)
            {
                HttpStatusCode statusCode = app._request.StatusCode;
                var writer = GetWriter(app.Writer, sizeHint: 64);
                writer.UnsafeWriteNoLengthCheck(Constants.Http11Space);
                writer.WriteNumeric((uint)statusCode);
                writer.Write((byte)' ');
                writer.WriteAsciiString(ReasonPhrases.GetReasonPhrase((int)statusCode));
                writer.WriteCRLF();
                writer.Commit();
            }
        }

        public override void OnStartLine(HttpVersionAndMethod versionAndMethod, TargetOffsetPathLength targetPath, Span<byte> startLine)
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

            _request.ConfigureRequest(hasContentLength: true, hasTrailingHeaders: false);

            _request.WriteRequestStart(
                startLine.Slice(0, versionAndMethod.MethodEnd),
                _authority.AsSpan(),
                pathAndQuery);

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

        public override void OnHeader(ReadOnlySpan<byte> name, ReadOnlySpan<byte> value)
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

            //Console.WriteLine($"Request header: {Encoding.ASCII.GetString(name)}={Encoding.ASCII.GetString(value)}");

            _request.WriteHeader(name, value);
        }
    }
}
