namespace LLRP.Helpers
{
    internal ref struct BufferWriter<T> where T : IBufferWriter<byte>
    {
        private readonly T _output;
        private Span<byte> _span;
        private int _buffered;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BufferWriter(T output, int sizeHint)
        {
            _buffered = 0;
            _output = output;
            _span = output.GetSpan(sizeHint);
        }

        public Span<byte> Span => _span;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Commit()
        {
            Debug.Assert(_buffered > 0);
            int buffered = _buffered;
            _buffered = 0;
            _output.Advance(buffered);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance(int count)
        {
            Debug.Assert(count >= 0 && count <= _span.Length);
            _buffered += count;
            _span = MemoryMarshal.CreateSpan(ref Unsafe.Add(ref MemoryMarshal.GetReference(_span), count), _span.Length - count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(ReadOnlySpan<byte> source)
        {
            Span<byte> span = _span;

            if (span.Length >= source.Length)
            {
                source.CopyTo(span);
                Advance(source.Length);
            }
            else
            {
                WriteMultiBuffer(source);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UnsafeWriteNoLengthCheck(ReadOnlySpan<byte> source)
        {
            source.CopyTo(_span);
            Advance(source.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(byte b)
        {
            Ensure();
            MemoryMarshal.GetReference(_span) = b;
            Advance(1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Ensure()
        {
            if (_span.Length == 0)
            {
                EnsureMore();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Ensure(int count)
        {
            if (_span.Length < count)
            {
                EnsureMore(count);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void EnsureMore()
        {
            Commit();
            _span = _output.GetSpan();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void EnsureMore(int count)
        {
            Commit();
            _span = _output.GetSpan(count);
        }

        private void WriteMultiBuffer(ReadOnlySpan<byte> source)
        {
            while (source.Length > 0)
            {
                EnsureMore();
                var writable = Math.Min(source.Length, _span.Length);
                source.Slice(0, writable).CopyTo(_span);
                source = source.Slice(writable);
                Advance(writable);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteCRLF()
        {
            Ensure(2);
            BufferExtensions.WriteCRLF(ref MemoryMarshal.GetReference(_span));
            Advance(2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteStatusCode(uint number)
        {
            Debug.Assert(number >= 100 && number <= 999);

            const byte AsciiDigitStart = (byte)'0';

            Span<byte> span = Span;

            if (span.Length >= 3)
            {
                var digit0 = (byte)((number * 41u) >> 12); // div100, valid to 1098
                var digits01 = (byte)((number * 205u) >> 11); // div10, valid to 1028
                span[0] = (byte)(digit0 + AsciiDigitStart);
                span[1] = (byte)(digits01 - (digit0 * 10) + AsciiDigitStart);
                span[2] = (byte)(number - (digits01 * 10) + AsciiDigitStart);
                Advance(3);
            }
            else
            {
                BufferExtensions.WriteNumericMultiWrite(ref this, number);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteChunkedEncodingChunkNoLengthCheck(ReadOnlySpan<byte> chunk)
        {
            Span<byte> span = _span;

            Debug.Assert(chunk.Length <= 65535);
            Debug.Assert(chunk.Length + 4 + 4 < span.Length);

            if (!Utf8Formatter.TryFormat(chunk.Length, span, out int bytesWritten, 'X'))
            {
                Debug.Fail("Failed to encode hex");
            }

            ref byte pBuf = ref Unsafe.AddByteOffset(ref MemoryMarshal.GetReference(span), (nuint)bytesWritten);

            BufferExtensions.WriteCRLF(ref pBuf);
            pBuf = ref Unsafe.AddByteOffset(ref pBuf, 2);

            Unsafe.CopyBlockUnaligned(ref pBuf, ref MemoryMarshal.GetReference(chunk), (uint)chunk.Length);
            pBuf = ref Unsafe.AddByteOffset(ref pBuf, (nuint)chunk.Length);

            BufferExtensions.WriteCRLF(ref pBuf);

            Advance(chunk.Length + bytesWritten + 4);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteHexNumber(uint number)
        {
            if (Utf8Formatter.TryFormat(number, Span, out int bytesWritten, 'X'))
            {
                Advance(bytesWritten);
            }
            else
            {
                BufferExtensions.WriteHexNumberMultiWrite(ref this, number);
            }
        }
    }
}
