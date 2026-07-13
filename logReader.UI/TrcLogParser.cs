using System.Globalization;

namespace logReader.UI
{
    internal static class TrcLogParser
    {
        private static readonly string[] StartTimeFormats = new[]
        {
            "dd.MM.yyyy HH:mm:ss.ffff",
            "dd.MM.yyyy HH:mm:ss.fff",
            "dd.MM.yyyy HH:mm:ss",
            "d.M.yyyy H:mm:ss.ffff",
            "d.M.yyyy H:mm:ss.fff",
            "d.M.yyyy H:mm:ss"
        };

        internal static bool TryParseTrcFrameLine(
            string line,
            out decimal timeMs,
            out string direction,
            out string id,
            out int dlc,
            Span<int> bytes,
            out int parsedByteCount)
            => TryParseTrcFrameLine(line, out _, out timeMs, out direction, out id, out dlc, bytes, out parsedByteCount);

        internal static bool TryParseTrcFrameLine(
            string line,
            out int messageIndex,
            out decimal timeMs,
            out string direction,
            out string id,
            out int dlc,
            Span<int> bytes,
            out int parsedByteCount)
        {
            messageIndex = 0;
            timeMs = 0m;
            direction = "";
            id = "";
            dlc = 0;
            parsedByteCount = 0;

            if (string.IsNullOrWhiteSpace(line))
                return false;

            string trimmed = line.TrimStart();
            if (trimmed.StartsWith(";"))
                return false;

            var tokens = trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 5)
                return false;

            if (!TryParseFrameIndex(tokens[0], out messageIndex))
                return false;

            if (!TryParseMilliseconds(tokens[1], out timeMs))
                return false;

            if (!TryParseDirection(tokens[2], out direction))
                return false;

            if (!CanToken.TryNormalizeId(tokens[3], out id))
                return false;

            if (!int.TryParse(tokens[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out dlc))
                return false;
            if (dlc < 0)
                return false;

            for (int i = 5; i < tokens.Length && parsedByteCount < bytes.Length; i++)
            {
                if (!CanToken.TryParseHexByte(tokens[i], out int value))
                    break;

                bytes[parsedByteCount++] = value;
            }

            int expectedByteCount = Math.Min(dlc, bytes.Length);
            if (expectedByteCount > 0 && parsedByteCount < expectedByteCount)
                return false;

            return true;
        }

        internal static DateTime? ParseStartTime(IEnumerable<string> lines)
        {
            DateTime? fallback = null;

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string trimmed = line.Trim();

                if (TryParseStartTimeAfterMarker(trimmed, "Start time:", out DateTime startTime))
                    return startTime;
                if (TryParseStartTimeAfterMarker(trimmed, "\u0412\u0440\u0435\u043C\u044F \u043D\u0430\u0447\u0430\u043B\u0430 \u0437\u0430\u043F\u0438\u0441\u0438:", out startTime))
                    return startTime;

                int oaIdx = trimmed.IndexOf("$STARTTIME=", StringComparison.OrdinalIgnoreCase);
                if (oaIdx >= 0 && fallback == null)
                {
                    string valueText = trimmed.Substring(oaIdx + "$STARTTIME=".Length).Trim();
                    if (double.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out double oaDate)
                        || double.TryParse(valueText, NumberStyles.Float, CultureInfo.CurrentCulture, out oaDate))
                    {
                        // FromOADate бросает ArgumentException на датах вне диапазона OLE —
                        // просто игнорируем некорректный $STARTTIME.
                        try { fallback = DateTime.FromOADate(oaDate); }
                        catch (ArgumentException) { }
                    }
                }
            }

            return fallback;
        }

        private static bool TryParseFrameIndex(string rawToken, out int index)
        {
            index = 0;
            if (string.IsNullOrWhiteSpace(rawToken))
                return false;

            string token = rawToken.Trim();
            if (token.EndsWith(")", StringComparison.Ordinal))
                token = token.Substring(0, token.Length - 1);

            return int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out index);
        }

        private static bool TryParseMilliseconds(string rawToken, out decimal value)
        {
            value = 0m;
            if (string.IsNullOrWhiteSpace(rawToken))
                return false;

            string token = rawToken.Trim().Replace(',', '.');
            return decimal.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                   || decimal.TryParse(token, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
        }

        private static bool TryParseDirection(string rawToken, out string direction)
        {
            direction = "";
            if (rawToken.Equals("Rx", StringComparison.OrdinalIgnoreCase))
            {
                direction = "Rx";
                return true;
            }
            if (rawToken.Equals("Tx", StringComparison.OrdinalIgnoreCase))
            {
                direction = "Tx";
                return true;
            }

            return false;
        }

        private static bool TryParseStartTimeAfterMarker(string line, string marker, out DateTime startTime)
        {
            startTime = default;

            int idx = line.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
                return false;

            string raw = line.Substring(idx + marker.Length).Trim();
            raw = NormalizeStartTime(raw);

            if (DateTime.TryParseExact(
                raw,
                StartTimeFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out startTime))
            {
                return true;
            }

            return DateTime.TryParse(raw, CultureInfo.GetCultureInfo("ru-RU"), DateTimeStyles.None, out startTime)
                   || DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out startTime);
        }

        private static string NormalizeStartTime(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return raw;

            string normalized = raw.Trim().Replace(',', '.');
            int spaceIdx = normalized.IndexOf(' ');
            if (spaceIdx < 0 || spaceIdx + 1 >= normalized.Length)
                return normalized;

            string datePart = normalized.Substring(0, spaceIdx);
            string timePart = normalized.Substring(spaceIdx + 1);

            int lastDot = timePart.LastIndexOf('.');
            int prevDot = lastDot > 0 ? timePart.LastIndexOf('.', lastDot - 1) : -1;
            if (lastDot > 0 && prevDot > 0 && lastDot - prevDot <= 5)
                timePart = timePart.Remove(lastDot, 1);

            return datePart + " " + timePart;
        }
    }
}
