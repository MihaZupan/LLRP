using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Forwarder;

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

    internal sealed class ConnectionHttpContextUriBuilder
    {
        private readonly string _uriBase;

        private char[] _lastPathAndQueryBuffer;
        private int _lastPathLength;
        private int _lastQueryLength;
        private Uri _lastUri;

        public ConnectionHttpContextUriBuilder(Uri baseUri)
        {
            _uriBase = baseUri.AbsoluteUri;

            _lastPathAndQueryBuffer = Array.Empty<char>();
            _lastPathLength = 0;
            _lastQueryLength = 0;
            _lastUri = baseUri;
        }

        public Uri CreateUri(PathString path, QueryString query)
        {
            ReadOnlySpan<char> lastPath = MemoryMarshal.CreateReadOnlySpan(
                ref MemoryMarshal.GetArrayDataReference(_lastPathAndQueryBuffer),
                _lastPathLength);

            if (lastPath.SequenceEqual(path.Value))
            {
                if (!query.HasValue && _lastQueryLength == 0)
                {
                    return _lastUri;
                }

                ReadOnlySpan<char> lastQuery = MemoryMarshal.CreateReadOnlySpan(
                    ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_lastPathAndQueryBuffer), _lastPathLength),
                    _lastQueryLength);

                if (lastQuery.SequenceEqual(query.Value))
                {
                    return _lastUri;
                }
            }

            CreateNewUri(path, query);
            return _lastUri;
        }

        private void CreateNewUri(PathString path, QueryString query)
        {
            string pathValue = path.Value ?? string.Empty;
            string queryValue = query.Value ?? string.Empty;

            _lastPathLength = pathValue.Length;
            _lastQueryLength = queryValue.Length;

            int minLength = pathValue.Length + queryValue.Length;
            if (_lastPathAndQueryBuffer.Length < minLength)
            {
                _lastPathAndQueryBuffer = new char[Math.Max(_lastPathAndQueryBuffer.Length * 2, minLength)];
            }

            pathValue.CopyTo(_lastPathAndQueryBuffer);
            queryValue.CopyTo(_lastPathAndQueryBuffer.AsSpan(pathValue.Length));

            _lastUri = RequestUtilities.MakeDestinationAddress(_uriBase, path, query);
        }
    }
}
