namespace LLRP
{
    public sealed class DownstreamAddress
    {
        public readonly Uri Uri;
        public readonly byte[] Authority;
        public readonly byte[] PathPrefix;
        public readonly bool NoPathPrefix;
        public readonly DnsEndPoint EndPoint;

        public DownstreamAddress(Uri uri)
        {
            Uri = uri;
            Authority = Encoding.UTF8.GetBytes(uri.Authority);
            PathPrefix = Encoding.UTF8.GetBytes(uri.AbsolutePath);
            NoPathPrefix = uri.AbsolutePath.AsSpan().TrimStart('/').Length == 0;
            EndPoint = new DnsEndPoint(uri.Host, uri.Port);
        }

        public static DownstreamAddress[] DownstreamAddresses { private get; set; } = null!;
        private static int _index;

        public static DownstreamAddress GetNextAddress()
        {
            int index = Interlocked.Increment(ref _index);
            var downstreams = DownstreamAddresses;
            return downstreams[index % downstreams.Length];
        }
    }
}
