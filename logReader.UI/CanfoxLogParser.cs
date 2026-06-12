using System.Globalization;
using System.Text;

namespace logReader.UI
{
    /// <summary>
    /// Логи PCAN-View с CANfox: колонки Date, Time, ID, Len, Data; дата и длина для декодирования не используются.
    /// </summary>
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

        /// <summary>
        /// Доля суток (0..1) по времени HH:mm:ss.fff в строке лога — как в pCAN+Excel.
        /// </summary>
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

            if (!TryNormalizeId(tokens[3], out id)) return false;

            if (!int.TryParse(tokens[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out int dlc) || dlc < 0)
                return false;

            for (int i = 5; i < tokens.Length && parsedByteCount < bytes.Length; i++)
            {
                if (!TryParseHexByte(tokens[i], out int value)) break;
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

        private static bool TryNormalizeId(string rawToken, out string id)
        {
            id = "";
            if (string.IsNullOrWhiteSpace(rawToken)) return false;

            string token = rawToken.Trim();
            if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                token = token[2..];

            if (token.Length == 0) return false;

            for (int i = 0; i < token.Length; i++)
            {
                char c = token[i];
                bool isHex = (c >= '0' && c <= '9')
                             || (c >= 'A' && c <= 'F')
                             || (c >= 'a' && c <= 'f');
                if (!isHex) return false;
            }

            id = token.ToUpperInvariant();
            return true;
        }

        private static bool TryParseHexByte(string rawToken, out int value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(rawToken)) return false;

            string token = rawToken.Trim();
            if (token.Length is 0 or > 2) return false;

            return int.TryParse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value)
                   && value is >= 0 and <= byte.MaxValue;
        }
    }
}
