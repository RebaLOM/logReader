using System.Globalization;

namespace logReader.Processing
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

        // Раскладка строки определяется по самой строке, поэтому заголовок $COLUMNS не обязателен:
        //  1.0: «N) O ID L D…»;              1.1: «N) O Rx ID L D…»;
        //  1.2: «N) O Bus Rx ID L D…»;       1.3: «N) O Bus Rx ID - L D…»;
        //  ДСТ: «N O Rx ID L D…», без скобки после номера.
        //  2.0: «N O T ID Rx l D…» (l — длина данных); 2.1: «N O T Bus ID Rx - L D…» (L — код DLC).
        // dlc на выходе — длина данных в байтах.
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
            if (trimmed.StartsWith(';'))
                return false;

            var tokens = trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 4)
                return false;

            bool versionOne = tokens[0].EndsWith(')') || TryParseDirection(tokens[2], out _);
            if (!TryParseFrameIndex(tokens[0], out messageIndex))
                return false;
            if (!TryParseMilliseconds(tokens[1], out timeMs))
                return false;

            int idIndex;
            int lengthIndex;
            bool lengthIsDlcCode = false;
            bool isFd = false;
            direction = "Rx";

            if (versionOne)
            {
                if (IsStatusType(tokens[2]))
                    return false;
                if (TryParseDirection(tokens[2], out direction))
                {
                    idIndex = 3;
                    lengthIndex = 4;
                }
                else if (IsBusNumber(tokens[2]) && tokens.Length > 4 && TryParseDirection(tokens[3], out direction))
                {
                    idIndex = 4;
                    lengthIndex = tokens.Length > 5 && tokens[5] == "-" ? 6 : 5;
                }
                else
                {
                    direction = "Rx";
                    idIndex = 2;
                    lengthIndex = 3;
                }
            }
            else
            {
                if (!TryParseFrameType(tokens[2], out isFd))
                    return false;
                if (tokens.Length > 4 && TryParseDirection(tokens[4], out direction))
                {
                    idIndex = 3;
                    lengthIndex = 5;
                }
                else if (tokens.Length > 5 && IsBusNumber(tokens[3]) && TryParseDirection(tokens[5], out direction))
                {
                    idIndex = 4;
                    lengthIndex = tokens.Length > 6 && tokens[6] == "-" ? 7 : 6;
                    lengthIsDlcCode = true;
                }
                else
                {
                    return false;
                }
            }

            if (lengthIndex >= tokens.Length || !CanToken.TryNormalizeId(tokens[idIndex], out id))
                return false;
            if (!int.TryParse(tokens[lengthIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out int lengthField) || lengthField < 0)
                return false;

            dlc = lengthIsDlcCode && isFd ? VectorCanFdAscWriter.LengthForDlc(lengthField) : lengthField;

            // RTR и прочие кадры без данных («RTR» вместо байтов) пропускаются.
            int expected = Math.Min(dlc, bytes.Length);
            int dataStart = lengthIndex + 1;
            for (int i = 0; i < expected; i++)
            {
                if (dataStart + i >= tokens.Length || !CanToken.TryParseHexByte(tokens[dataStart + i], out int value))
                    return false;
                bytes[parsedByteCount++] = value;
            }

            return dataStart >= tokens.Length || !tokens[dataStart].Equals("RTR", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsBusNumber(string token)
            => int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out int bus) && bus is >= 0 and < 100;

        private static bool IsStatusType(string token)
            => token.Equals("Warng", StringComparison.OrdinalIgnoreCase)
               || token.Equals("Error", StringComparison.OrdinalIgnoreCase);

        // Типы записей TRC 2.x: DT — CAN, FD/FB/FE/BI — CAN FD; RR (remote), ST/EC/ER/EV (статус, ошибки, события) — без данных.
        private static bool TryParseFrameType(string token, out bool isFd)
        {
            isFd = false;
            switch (token.ToUpperInvariant())
            {
                case "DT":
                    return true;
                case "FD":
                case "FB":
                case "FE":
                case "BI":
                    isFd = true;
                    return true;
                default:
                    return false;
            }
        }

        internal static DateTime? ParseStartTime(string path, System.Text.Encoding encoding)
            => ParseStartTime(LogFileReader.ReadLines(path, encoding));

        // pCAN использует «;…», ДСТ — текстовый заголовок без «;».
        // Останавливаемся до кадров; неизвестный текстовый заголовок ограничен 64 строками.
        internal static DateTime? ParseStartTime(IEnumerable<string> lines)
        {
            DateTime? fallback = null;
            int plainHeaderLines = 0;

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string trimmed = line.Trim();
                if (!trimmed.StartsWith(';') && (char.IsDigit(trimmed[0]) || ++plainHeaderLines > 64))
                    break;

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
