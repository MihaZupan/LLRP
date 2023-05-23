namespace LLRP.Helpers
{
    internal static class BufferExtensions
    {
        private const int _maxULongByteLength = 20;

        [ThreadStatic]
        private static byte[]? _numericBytesScratch;

        public static void WriteUtf8String<T>(ref this BufferWriter<T> buffer, string text)
             where T : struct, IBufferWriter<byte>
        {
            buffer.Ensure(text.Length * 3);
            int byteCount = Encoding.UTF8.GetBytes(text.AsSpan(), buffer.Span);
            buffer.Advance(byteCount);
        }

        public static void WriteAsciiString<T>(ref this BufferWriter<T> buffer, string text)
             where T : struct, IBufferWriter<byte>
        {
            buffer.Ensure(text.Length);
            int byteCount = Encoding.ASCII.GetBytes(text.AsSpan(), buffer.Span);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteCRLF(ref byte pBuf)
        {
            if (BitConverter.IsLittleEndian)
            {
                Unsafe.WriteUnaligned(ref pBuf, (ushort)0x0A0D);
            }
            else
            {
                Unsafe.WriteUnaligned(ref pBuf, (ushort)0x0D0A);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void WriteHexNumberMultiWrite<T>(ref this BufferWriter<T> buffer, uint number)
             where T : IBufferWriter<byte>
        {
            Span<byte> byteBuffer = stackalloc byte[16];

            if (!Utf8Formatter.TryFormat(number, byteBuffer, out int bytesWritten, 'X'))
            {
                Debug.Fail("Failed to encode hex");
            }

            buffer.Write(MemoryMarshal.CreateReadOnlySpan(ref MemoryMarshal.GetReference(byteBuffer), bytesWritten));
        }
    }
}
