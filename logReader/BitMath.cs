using System.Globalization;

namespace logReader
{
    // Общая битовая математика для редакторов DBC/XLSX: раньше копии в формах
    // расходились (XLSX проверял Motorola как start+length вместо обхода по DBC).
    public static class BitMath
    {
        public static void ComputeRawRange(int length, bool signed, out long rawMin, out long rawMax)
        {
            if (signed)
            {
                if (length >= 64) { rawMin = long.MinValue; rawMax = long.MaxValue; }
                else { rawMin = -(1L << (length - 1)); rawMax = (1L << (length - 1)) - 1; }
            }
            else
            {
                rawMin = 0;
                rawMax = length >= 64 ? long.MaxValue : (long)((1UL << length) - 1);
            }
        }

        // Десятичное raw-значение: знаковое (−128..127) или беззнаковое (0..255).
        public static string FormatRawBound(long raw, int length, bool signed)
        {
            if (!signed && length > 0 && length < 64)
                return ((long)((ulong)raw & ((1UL << length) - 1))).ToString(CultureInfo.InvariantCulture);
            return raw.ToString(CultureInfo.InvariantCulture);
        }

        public static string FormatHex(long raw, int length)
        {
            int nibbles = Math.Max(1, (length + 3) / 4);
            ulong masked;
            if (length >= 64) masked = unchecked((ulong)raw);
            else
            {
                ulong mask = (1UL << length) - 1;
                masked = unchecked((ulong)raw) & mask;
            }
            return masked.ToString("X" + nibbles, CultureInfo.InvariantCulture);
        }

        public static bool SignalFitsInDlc(int startBit, int length, bool littleEndian, int payloadBits)
        {
            if (length <= 0 || length > payloadBits) return false;

            if (littleEndian)
                return startBit >= 0 && startBit + length <= payloadBits;

            // Motorola: биты идут вниз по байту, при переходе — в MSB следующего (+15).
            int bit = startBit;
            for (int i = 0; i < length; i++)
            {
                if (bit < 0 || bit >= payloadBits) return false;
                if ((bit % 8) == 0) bit += 15;
                else bit -= 1;
            }
            return true;
        }

        public static int CellToGlobalBit(int byteIndex, int bitInByte) => byteIndex * 8 + bitInByte;

        public static bool TryGlobalBitToCell(int globalBit, out int byteIndex, out int bitInByte)
        {
            byteIndex = 0;
            bitInByte = 0;
            if (globalBit < 0) return false;
            byteIndex = globalBit / 8;
            bitInByte = globalBit % 8;
            return true;
        }

        public static IEnumerable<int> EnumerateSignalBits(int startBit, int length, bool littleEndian)
        {
            if (length <= 0) yield break;

            if (littleEndian)
            {
                for (int i = 0; i < length; i++)
                    yield return startBit + i;
                yield break;
            }

            int bit = startBit;
            for (int i = 0; i < length; i++)
            {
                yield return bit;
                if ((bit % 8) == 0) bit += 15;
                else bit -= 1;
            }
        }

        public static bool TryBuildSelectionFromGlobalBits(
            int anchorBit,
            int targetBit,
            bool littleEndian,
            int payloadBits,
            out int startBit,
            out int length)
        {
            startBit = 0;
            length = 0;
            if (anchorBit < 0 || anchorBit >= payloadBits || targetBit < 0 || targetBit >= payloadBits)
                return false;

            if (littleEndian)
            {
                int lo = Math.Min(anchorBit, targetBit);
                int hi = Math.Max(anchorBit, targetBit);
                startBit = lo;
                length = hi - lo + 1;
                return length > 0;
            }

            var path = new List<int>();
            int bit = anchorBit;
            path.Add(bit);
            while (bit != targetBit)
            {
                if ((bit % 8) == 0) bit += 15;
                else bit -= 1;
                if (bit < 0 || bit >= payloadBits) return false;
                path.Add(bit);
                if (path.Count > payloadBits) return false;
            }

            startBit = anchorBit;
            length = path.Count;
            return true;
        }
    }
}
