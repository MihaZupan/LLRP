namespace LLRP.Helpers
{
    internal static class SpanExtensions
    {
        public static bool EqualsIgnoreCase(this ReadOnlySpan<byte> wireValue, ReadOnlySpan<byte> expectedValueLowerCase)
        {
            if (wireValue.Length != expectedValueLowerCase.Length) return false;

            ref byte xRef = ref MemoryMarshal.GetReference(wireValue);
            ref byte yRef = ref MemoryMarshal.GetReference(expectedValueLowerCase);

            for (uint i = 0; i < (uint)wireValue.Length; ++i)
            {
                byte xv = Unsafe.Add(ref xRef, (IntPtr)i);

                if ((xv - (uint)'A') <= ('Z' - 'A'))
                {
                    xv |= 0x20;
                }

                if (xv != Unsafe.Add(ref yRef, (IntPtr)i)) return false;
            }

            return true;
        }
    }
}
