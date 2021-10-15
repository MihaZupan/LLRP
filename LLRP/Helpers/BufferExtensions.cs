using System.Buffers;
using System.Buffers.Text;

namespace LLRP.Helpers
{
    // Same as KestrelHttpServer\src\Kestrel.Core\Internal\Http\PipelineExtensions.cs
    // However methods accept T : struct, IBufferWriter<byte> rather than PipeWriter.
    // This allows a struct wrapper to turn CountingBufferWriter into a non-shared generic,
    // while still offering the WriteNumeric extension.

    internal static class BufferExtensions
    {
        private const int _maxULongByteLength = 20;

        [ThreadStatic]
        private static byte[]? _numericBytesScratch;

        public static void WriteUtf8String<T>(ref this BufferWriter<T> buffer, string text)
             where T : struct, IBufferWriter<byte>
        {
            var byteCount = Encoding.UTF8.GetByteCount(text);
            buffer.Ensure(byteCount);
            byteCount = Encoding.UTF8.GetBytes(text.AsSpan(), buffer.Span);
            buffer.Advance(byteCount);
        }

        public static void WriteAsciiString<T>(ref this BufferWriter<T> buffer, string text)
             where T : struct, IBufferWriter<byte>
        {
            buffer.Ensure(text.Length);
            int byteCount = Encoding.UTF8.GetBytes(text.AsSpan(), buffer.Span);
            Debug.Assert(byteCount == text.Length);
            buffer.Advance(byteCount);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void WriteNumericMultiWrite<T>(ref this BufferWriter<T> buffer, uint number)
             where T : IBufferWriter<byte>
        {
            const byte AsciiDigitStart = (byte)'0';

            var value = number;
            var position = _maxULongByteLength;
            var byteBuffer = NumericBytesScratch;
            do
            {
                // Consider using Math.DivRem() if available
                var quotient = value / 10;
                byteBuffer[--position] = (byte)(AsciiDigitStart + (value - quotient * 10)); // 0x30 = '0'
                value = quotient;
            }
            while (value != 0);

            var length = _maxULongByteLength - position;
            buffer.Write(new ReadOnlySpan<byte>(byteBuffer, position, length));
        }

        private static byte[] NumericBytesScratch => _numericBytesScratch ?? CreateNumericBytesScratch();

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static byte[] CreateNumericBytesScratch()
        {
            var bytes = new byte[_maxULongByteLength];
            _numericBytesScratch = bytes;
            return bytes;
        }
    }
}
