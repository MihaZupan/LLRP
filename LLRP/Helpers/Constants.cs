namespace LLRP.Helpers
{
    internal static class Constants
    {
        public static ReadOnlySpan<byte> Space => new byte[]
        {
            (byte)' '
        };
        public static ReadOnlySpan<byte> ColonSpace => new byte[]
        {
            (byte)':', (byte)' '
        };
        public static ReadOnlySpan<byte> Http11Space => new byte[]
        {
            (byte)'H', (byte)'T', (byte)'T', (byte)'P', (byte)'/', (byte)'1', (byte)'.', (byte)'1',  (byte)' '
        };
        public static ReadOnlySpan<byte> Http11OK => new byte[]
        {
            (byte)'H', (byte)'T', (byte)'T', (byte)'P', (byte)'/', (byte)'1', (byte)'.', (byte)'1',  (byte)' ',
            (byte)'2', (byte)'0', (byte)'0', (byte)' ', (byte)'O', (byte)'K', (byte)'\r', (byte)'\n'
        };
        public static ReadOnlySpan<byte> ChunkedEncodingFinalChunk => new byte[]
        {
            (byte)'0', (byte)'\r', (byte)'n', (byte)'\r', (byte)'\n'
        };
        public static ReadOnlySpan<byte> EncodedTransferEncodingName => new byte[]
        {
            (byte)'t', (byte)'r', (byte)'a', (byte)'n', (byte)'s', (byte)'f', (byte)'e', (byte)'r',
            (byte)'-',
            (byte)'e', (byte)'n', (byte)'c', (byte)'o', (byte)'d', (byte)'i', (byte)'n', (byte)'g'
        };
        public static ReadOnlySpan<byte> EncodedTransferEncodingChunkedValue => new byte[]
        {
            (byte)'c', (byte)'h', (byte)'u', (byte)'n', (byte)'k', (byte)'e', (byte)'d'
        };
    }
}
