using System.Buffers.Text;
using System.Net.Http.LowLevel;

namespace LLRP.Helpers
{
    internal static class DateHeader
    {
        const int DateTimeRLength = 29; // Tue, 16 May 2000 12:34:56 GMT

        private static readonly Timer s_timer = new(_ => UpdateHeader());

        private static readonly PreparedHeaderName s_dateHeaderName = new("Date");
        private static PreparedHeaderSet s_dateHeader = null!;

        public static PreparedHeaderSet Header => s_dateHeader;

        [ModuleInitializer]
        internal static void Initialize()
        {
            UpdateHeader();
            s_timer.Change(1000, 1000);
        }

        private static void UpdateHeader()
        {
            Span<byte> value = stackalloc byte[DateTimeRLength];
            bool success = Utf8Formatter.TryFormat(DateTimeOffset.UtcNow, value, out var written, 'R');
            Debug.Assert(success);
            Debug.Assert(written == DateTimeRLength);

            s_dateHeader = new PreparedHeaderSet
            {
                { s_dateHeaderName, value }
            };
        }
    }
}
