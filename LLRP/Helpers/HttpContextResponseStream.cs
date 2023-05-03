using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;

namespace LLRP.Helpers
{
    internal sealed class HttpContextResponseStream : Stream
    {
        private readonly Stream _innerStream;
        private HttpContext _context = null!;
        private bool _sentHeader = false;
        private bool _chunkedEncoding = false;
        private byte[] _responseContentBuffer;

        public HttpContextResponseStream(Stream stream, byte[] responseContentBuffer)
        {
            _innerStream = stream;
            _responseContentBuffer = responseContentBuffer;
        }

        public void SetContext(HttpContext context)
        {
            _context = context;
            _sentHeader = false;
            _chunkedEncoding = false;
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_sentHeader)
            {
                if (_chunkedEncoding)
                {
                    return WriteChunkAsync(buffer.Span, cancellationToken);
                }
                else
                {
                    return _innerStream.WriteAsync(buffer, cancellationToken);
                }
            }

            _sentHeader = true;
            return SendHeaderAndWriteAsync(buffer.Span, cancellationToken);
        }

        private ValueTask SendHeaderAndWriteAsync(ReadOnlySpan<byte> chunk, CancellationToken cancellationToken = default)
        {
            Span<byte> responseBuffer = _responseContentBuffer;

            int written = SerializeResponse(responseBuffer);
            responseBuffer = responseBuffer.Slice(written);

            if (_chunkedEncoding)
            {
                if (!Utf8Formatter.TryFormat(chunk.Length, responseBuffer, out int bytesWritten, 'X'))
                {
                    Debug.Fail("Failed to encode hex");
                }

                written += bytesWritten + 4;

                ref byte pBuf = ref Unsafe.AddByteOffset(ref MemoryMarshal.GetReference(responseBuffer), (nuint)bytesWritten);

                BufferExtensions.WriteCRLF(ref pBuf);
                pBuf = ref Unsafe.AddByteOffset(ref pBuf, 2);

                Unsafe.CopyBlockUnaligned(ref pBuf, ref MemoryMarshal.GetReference(chunk), (uint)chunk.Length);
                pBuf = ref Unsafe.AddByteOffset(ref pBuf, (nuint)chunk.Length);

                BufferExtensions.WriteCRLF(ref pBuf);
            }
            else
            {
                chunk.CopyTo(responseBuffer.Slice(written));
            }

            written += chunk.Length;

            return _innerStream.WriteAsync(_responseContentBuffer.AsMemory(0, written), cancellationToken);
        }

        private ValueTask WriteChunkAsync(ReadOnlySpan<byte> chunk, CancellationToken cancellationToken)
        {
            Span<byte> responseBuffer = _responseContentBuffer;

            if (!Utf8Formatter.TryFormat(chunk.Length, responseBuffer, out int bytesWritten, 'X'))
            {
                Debug.Fail("Failed to encode hex");
            }

            ref byte pBuf = ref Unsafe.AddByteOffset(ref MemoryMarshal.GetReference(responseBuffer), (nuint)bytesWritten);

            BufferExtensions.WriteCRLF(ref pBuf);
            pBuf = ref Unsafe.AddByteOffset(ref pBuf, 2);

            Unsafe.CopyBlockUnaligned(ref pBuf, ref MemoryMarshal.GetReference(chunk), (uint)chunk.Length);
            pBuf = ref Unsafe.AddByteOffset(ref pBuf, (nuint)chunk.Length);

            BufferExtensions.WriteCRLF(ref pBuf);

            return _innerStream.WriteAsync(_responseContentBuffer.AsMemory(chunk.Length + bytesWritten + 4), cancellationToken);
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return _innerStream.FlushAsync(cancellationToken);
        }

        private int SerializeResponse(Span<byte> buffer)
        {
            HttpResponse response = _context.Response;

            int offset;
            if (response.StatusCode == 200)
            {
                Constants.Http11OK.CopyTo(buffer);
                offset = Constants.Http11OK.Length;
            }
            else
            {
                Constants.Http11Space.CopyTo(buffer);
                offset = Constants.Http11Space.Length;
                offset += WriteStatusLineSlow(buffer.Slice(offset), response);
            }

            foreach (var header in response.Headers)
            {
                string key = header.Key;
                string value = header.Value.ToString();

                if (key == "Transfer-Encoding" && value == "chunked")
                {
                    _chunkedEncoding = true;
                }

                foreach (char c in key)
                {
                    buffer[offset++] = (byte)c;
                }
                buffer[offset++] = (byte)':';
                buffer[offset++] = (byte)' ';
                foreach (char c in value)
                {
                    buffer[offset++] = (byte)c;
                }
                buffer[offset++] = (byte)'\r';
                buffer[offset++] = (byte)'\n';
            }

            buffer[offset++] = (byte)'\r';
            buffer[offset++] = (byte)'\n';

            return offset;

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
}
