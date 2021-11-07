using System.Buffers;

namespace LLRP.Helpers
{
    internal sealed class ConnectionUriBuilder
    {
        private readonly string _uriBase;
        private readonly bool _uriBaseEndsWithSlash;

        private byte[] _lastPathAndQueryBuffer;
        private int _lastPathAndQueryLength;
        private Uri _lastUri;

        public ConnectionUriBuilder(Uri baseUri)
        {
            _uriBase = baseUri.AbsoluteUri;
            _uriBaseEndsWithSlash = _uriBase.EndsWith('/');

            _lastPathAndQueryBuffer = Array.Empty<byte>();
            _lastPathAndQueryLength = 0;
            _lastUri = baseUri;
        }

        public Uri CreateUri(ReadOnlySpan<byte> pathAndQuery)
        {
            ReadOnlySpan<byte> lastPathAndQuery = MemoryMarshal.CreateReadOnlySpan(
                ref MemoryMarshal.GetArrayDataReference(_lastPathAndQueryBuffer),
                _lastPathAndQueryLength);

            if (!pathAndQuery.SequenceEqual(lastPathAndQuery))
            {
                CreateNewUri(pathAndQuery);
            }

            return _lastUri;
        }

        private void CreateNewUri(ReadOnlySpan<byte> pathAndQuery)
        {
            if (_lastPathAndQueryBuffer.Length < pathAndQuery.Length)
            {
                _lastPathAndQueryBuffer = new byte[Math.Max(_lastPathAndQueryBuffer.Length * 2, pathAndQuery.Length)];
            }

            pathAndQuery.CopyTo(_lastPathAndQueryBuffer);
            _lastPathAndQueryLength = pathAndQuery.Length;

            if (_uriBaseEndsWithSlash && pathAndQuery.Length != 0 && pathAndQuery[0] == '/')
            {
                pathAndQuery = pathAndQuery.Slice(1);
            }

            char[]? bufferToReturn = null;
            Span<char> charBuffer = pathAndQuery.Length <= 512
                ? stackalloc char[512]
                : (bufferToReturn = ArrayPool<char>.Shared.Rent(pathAndQuery.Length));

            int length = Encoding.UTF8.GetChars(pathAndQuery, charBuffer);

            string uriString = string.Concat(_uriBase, charBuffer.Slice(0, length));

            if (bufferToReturn is not null)
            {
                ArrayPool<char>.Shared.Return(bufferToReturn);
            }

            _lastUri = new Uri(uriString, UriKind.Absolute);
        }
    }
}
