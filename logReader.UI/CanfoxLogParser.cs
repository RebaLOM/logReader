using System.Globalization;
using System.Text;

namespace logReader.UI
{
    // Логи CANfox/PCAN-View: Date и Len в файле есть, для декодирования используем Time и Data.
    internal static class CanfoxLogParser
    {
        private const int PeekLineCount = 120;

        private static bool IsDataDateToken(string token)
        {
            return token.Length == 10
                   && char.IsDigit(token[0])
                   && char.IsDigit(token[1])
                   && char.IsDigit(token[2])
                   && char.IsDigit(token[3])
                   && token[4] == '.'
                   && char.IsDigit(token[5])
                   && char.IsDigit(token[6])
                   && token[7] == '.'
                   && char.IsDigit(token[8])
                   && char.IsDigit(token[9]);
        }

        internal static bool LooksLikeCanfoxLog(string filePath, Encoding encoding)
        {
            Span<int> bytes = stackalloc int[8];
            int n = 0;
            foreach (var line in File.ReadLines(filePath, encoding))
            {
                if (++n > PeekLineCount) break;
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (TryParseCanfoxFrameLine(line, out _, out _, bytes, out _))
                    return true;
            }

            return false;
        }

        // timeDayFraction — доля суток, как в pCAN/Excel, а не абсолютные секунды.
        internal static bool TryParseCanfoxFrameLine(
            string line,
            out double timeDayFraction,
            out string id,
            Span<int> bytes,
            out int parsedByteCount)
        {
            timeDayFraction = 0;
            id = "";
            parsedByteCount = 0;

            if (string.IsNullOrWhiteSpace(line)) return false;

            var tokens = line.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 5) return false;

            if (!IsDataDateToken(tokens[0])) return false;
            if (!TryParseHmsFff(tokens[1], out timeDayFraction)) return false;
            if (!tokens[2].Equals("-", StringComparison.Ordinal)) return false;

            if (!CanToken.TryNormalizeId(tokens[3], out id)) return false;

            if (!int.TryParse(tokens[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out int dlc) || dlc < 0)
                return false;

            for (int i = 5; i < tokens.Length && parsedByteCount < bytes.Length; i++)
            {
                if (!CanToken.TryParseHexByte(tokens[i], out int value)) break;
                bytes[parsedByteCount++] = value;
            }

            if (dlc == 0) return true;

            if (parsedByteCount < dlc) return false;

            return true;
        }

        private static bool TryParseHmsFff(string raw, out double timeDayFraction)
        {
            timeDayFraction = 0;
            if (string.IsNullOrWhiteSpace(raw)) return false;

            string t = raw.Trim().Replace(',', '.');
            var inv = CultureInfo.InvariantCulture;
            if (!TimeSpan.TryParseExact(t, "h\\:mm\\:ss\\.fff", inv, out TimeSpan ts)
                && !TimeSpan.TryParseExact(t, "hh\\:mm\\:ss\\.fff", inv, out ts)
                && !TimeSpan.TryParseExact(t, "h\\:mm\\:ss\\.ff", inv, out ts)
                && !TimeSpan.TryParseExact(t, "h\\:mm\\:ss\\.f", inv, out ts)
                && !TimeSpan.TryParseExact(t, "h\\:mm\\:ss", inv, out ts)
                && !TimeSpan.TryParseExact(t, "hh\\:mm\\:ss", inv, out ts)
                && !TimeSpan.TryParse(t, inv, out ts))
            {
                return false;
            }

            timeDayFraction = ts.TotalDays;
            if (timeDayFraction < 0d || timeDayFraction >= 1d)
                return false;

            return true;
        }
    }
}
