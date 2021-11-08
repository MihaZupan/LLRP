namespace LLRP.Helpers
{
    internal sealed class ConnectionHeaderValueCache
    {
        private byte[] _valueBuffer;
        private int _lastValueLength;
        private string _lastValue;

        public ConnectionHeaderValueCache()
        {
            _valueBuffer = Array.Empty<byte>();
            _lastValueLength = 0;
            _lastValue = string.Empty;
        }

        public string GetHeaderValue(ReadOnlySpan<byte> headerValue)
        {
            ReadOnlySpan<byte> lastPathAndQuery = MemoryMarshal.CreateReadOnlySpan(
                ref MemoryMarshal.GetArrayDataReference(_valueBuffer),
                _lastValueLength);

            if (!headerValue.SequenceEqual(lastPathAndQuery))
            {
                CreateNewValue(headerValue);
            }

            return _lastValue;
        }

        private void CreateNewValue(ReadOnlySpan<byte> headerValue)
        {
            if (_valueBuffer.Length < headerValue.Length)
            {
                _valueBuffer = new byte[Math.Max(_valueBuffer.Length * 2, headerValue.Length)];
            }

            headerValue.CopyTo(_valueBuffer);
            _lastValueLength = headerValue.Length;

            _lastValue = Encoding.UTF8.GetString(headerValue);
        }
    }
}
