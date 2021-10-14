using System.Buffers.Text;

namespace LLRP.Helpers
{
    internal static class DateHeader
    {
        const int PrefixLength = 6;     // "Date: ".Length
        const int DateTimeRLength = 29; // Tue, 16 May 2000 12:34:56 GMT
        const int SuffixLength = 2;     // crlf
        const int SuffixIndex = DateTimeRLength + PrefixLength;
        const int HeaderLength = PrefixLength + DateTimeRLength + 2 * SuffixLength;

        private static readonly Timer s_timer = new(_ => UpdateDateValues());

        private static byte[] s_headerBytesMaster = new byte[HeaderLength];
        private static byte[] s_headerBytesScratch = new byte[HeaderLength];

        public static ReadOnlySpan<byte> HeaderBytes => s_headerBytesMaster;

        [ModuleInitializer]
        internal static void Initialize()
        {
            Encoding.ASCII.GetBytes("Date: ", s_headerBytesScratch);
            s_headerBytesScratch[SuffixIndex] = (byte)'\r';
            s_headerBytesScratch[SuffixIndex + 1] = (byte)'\n';
            s_headerBytesScratch[SuffixIndex + 2] = (byte)'\r';
            s_headerBytesScratch[SuffixIndex + 3] = (byte)'\n';

            FormatDateTime();

            s_headerBytesScratch.CopyTo(s_headerBytesMaster, 0);

            s_timer.Change(1000, 1000);
        }

        private static void FormatDateTime()
        {
            bool success = Utf8Formatter.TryFormat(DateTimeOffset.UtcNow, s_headerBytesScratch.AsSpan(PrefixLength), out var written, 'R');
            Debug.Assert(success);
            Debug.Assert(written == DateTimeRLength);
        }

        private static void UpdateDateValues()
        {
            lock (s_timer)
            {
                FormatDateTime();
                byte[] temp = s_headerBytesMaster;
                s_headerBytesMaster = s_headerBytesScratch;
                s_headerBytesScratch = temp;
            }
        }
    }
}
