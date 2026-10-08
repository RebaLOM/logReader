using System.Globalization;
using System.Text;

namespace logReader.Processing
{
    // Параметры файла из заголовка Vector ASC: «date …», «base hex|dec timestamps absolute|relative».
    internal readonly record struct AscFileFormat(bool DecimalIds, bool RelativeTimestamps, long? BaseTimeTicks)
    {
        public static AscFileFormat Default => new(false, false, null);

        private const int HeaderScanLines = 50;

        public static AscFileFormat Read(string path, Encoding encoding)
        {
            bool decimalIds = false;
            bool relative = false;
            long? baseTicks = null;
            int n = 0;

            foreach (var line in LogFileReader.ReadLines(path, encoding))
            {
                if (++n > HeaderScanLines) break;
                string trimmed = line.Trim();

                if (baseTicks == null && AscLogParser.TryParseBaseTimeTicksFromHeaderLine(trimmed, out long ticks))
                    baseTicks = ticks;

                if (trimmed.StartsWith("base", StringComparison.OrdinalIgnoreCase))
                {
                    var tokens = trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                    decimalIds = tokens.Length > 1 && tokens[1].Equals("dec", StringComparison.OrdinalIgnoreCase);
                    relative = tokens.Any(t => t.Equals("relative", StringComparison.OrdinalIgnoreCase));
                    break;
                }
            }

            return new AscFileFormat(decimalIds, relative, baseTicks);
        }
    }

    internal static class AscLogParser
    {
        private static readonly string[] TimeFormats = BuildTimeFormats();

        private static string[] BuildTimeFormats()
        {
            var list = new List<string>();
            foreach (string hourFmt in new[] { "H", "HH" })
            {
                list.Add($"{hourFmt}:mm:ss");
                for (int frac = 1; frac <= 7; frac++)
                    list.Add($"{hourFmt}:mm:ss.{new string('F', frac)}");
            }
            return list.ToArray();
        }

        // «date Wed Apr 26 01:56:32.811 pm 2023» — CANoe в английской локали пишет 12-часовое время.
        internal static bool TryParseBaseTimeTicksFromHeaderLine(string line, out long baseTicks)
        {
            baseTicks = 0;

            if (string.IsNullOrWhiteSpace(line)) return false;
            var trimmed = line.TrimStart();
            if (!trimmed.StartsWith("date", StringComparison.OrdinalIgnoreCase)) return false;

            var tokens = trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length; i++)
            {
                if (!tokens[i].Contains(':')) continue;
                if (!TryParseTimeOfDayTicks(tokens[i], out baseTicks)) continue;

                string? meridiem = i + 1 < tokens.Length ? tokens[i + 1] : null;
                long hourTicks = TimeSpan.TicksPerHour;
                long hours = baseTicks / hourTicks;
                if (string.Equals(meridiem, "pm", StringComparison.OrdinalIgnoreCase) && hours < 12)
                    baseTicks += 12 * hourTicks;
                else if (string.Equals(meridiem, "am", StringComparison.OrdinalIgnoreCase) && hours == 12)
                    baseTicks -= 12 * hourTicks;
                return true;
            }

            return false;
        }

        internal static bool TryParseTimeOfDayTicks(string raw, out long ticks)
        {
            ticks = 0;
            if (string.IsNullOrWhiteSpace(raw)) return false;

            string normalized = raw.Trim().TrimEnd(',').Replace(',', '.');

            if (DateTime.TryParseExact(normalized, TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt))
            {
                ticks = dt.TimeOfDay.Ticks;
                return true;
            }

            return false;
        }

        internal static bool TryParseOffsetSecondsToTicks(string raw, out long ticks)
        {
            ticks = 0;
            if (string.IsNullOrWhiteSpace(raw)) return false;

            if (!decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal sec))
                return false;

            decimal tickDecimal = decimal.Round(sec * TimeSpan.TicksPerSecond, 0, MidpointRounding.AwayFromZero);
            if (tickDecimal > long.MaxValue || tickDecimal < long.MinValue) return false;
            ticks = (long)tickDecimal;
            return true;
        }

        internal static bool TryParseId(string raw, bool decimalIds, out string id)
        {
            id = "";
            if (string.IsNullOrWhiteSpace(raw)) return false;
            string token = raw.Trim();
            bool extendedMark = token.EndsWith("x", StringComparison.OrdinalIgnoreCase);
            if (extendedMark) token = token[..^1];

            if (!decimalIds)
                return CanToken.TryNormalizeId(token, out id);

            if (!uint.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint value) || value > CanId.MaxExtendedId)
                return false;
            id = CanId.Format(value);
            return true;
        }

        internal static bool TryParseHexByte(string hex, out int value)
            => CanToken.TryParseHexByte(hex, out value, requireTwoChars: true);

        private static bool IsDirection(string token)
            => token.Equals("Rx", StringComparison.OrdinalIgnoreCase) || token.Equals("Tx", StringComparison.OrdinalIgnoreCase);

        private static bool IsFrameCandidate(string line, out string[] tokens)
        {
            tokens = Array.Empty<string>();
            if (string.IsNullOrWhiteSpace(line)) return false;
            string trimmed = line.TrimStart();
            if (trimmed.Length == 0 || !(char.IsDigit(trimmed[0]) || trimmed[0] == '-' || trimmed[0] == '+'))
                return false;
            tokens = trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            return tokens.Length >= 4;
        }

        internal static bool TryParseFrameLine(
            string line,
            out long offsetTicks,
            out string id,
            Span<int> bytes,
            out int parsedByteCount)
            => TryParseFrameLine(line, AscFileFormat.Default, out offsetTicks, out id, bytes, out parsedByteCount);

        // Классика: «время канал ID Rx|Tx d DLC байты…»; CAN FD: «время CANFD канал Rx|Tx ID [имя] BRS ESI DLC DataLength байты…».
        internal static bool TryParseFrameLine(
            string line,
            AscFileFormat format,
            out long offsetTicks,
            out string id,
            Span<int> bytes,
            out int parsedByteCount)
        {
            offsetTicks = 0;
            id = "";
            parsedByteCount = 0;

            if (!IsFrameCandidate(line, out var tokens)) return false;
            if (!TryParseOffsetSecondsToTicks(tokens[0], out offsetTicks)) return false;

            if (tokens[1].Equals("CANFD", StringComparison.OrdinalIgnoreCase))
            {
                if (tokens.Length < 6 || !IsDirection(tokens[3]) || !TryParseId(tokens[4], format.DecimalIds, out id))
                    return false;
                if (!TryFindCanFdDataStart(tokens, 4, out int fdDataStart, out int dataByteCount))
                    return false;

                int toRead = Math.Min(dataByteCount, bytes.Length);
                for (int i = 0; i < toRead; i++)
                {
                    if (fdDataStart + i >= tokens.Length || !TryParseHexByte(tokens[fdDataStart + i], out int v))
                        return false;
                    bytes[parsedByteCount++] = v;
                }
                return true;
            }

            if (!int.TryParse(tokens[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                return false;
            if (!TryParseId(tokens[2], format.DecimalIds, out id) || !IsDirection(tokens[3]))
                return false;

            // «r» — remote frame без данных; служебные строки (ErrorFrame и т.п.) сюда не доходят.
            int marker = 4;
            if (marker >= tokens.Length || !tokens[marker].Equals("d", StringComparison.OrdinalIgnoreCase))
                return false;
            if (marker + 1 >= tokens.Length
                || !int.TryParse(tokens[marker + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int dlc)
                || dlc < 0)
                return false;

            int expected = Math.Min(dlc, bytes.Length);
            for (int i = 0; i < expected; i++)
            {
                int t = marker + 2 + i;
                if (t >= tokens.Length || !TryParseHexByte(tokens[t], out int v))
                    return false;
                bytes[parsedByteCount++] = v;
            }
            return true;
        }

        // Vector CAN FD ASC: после ID возможно символьное имя, затем BRS ESI DLC DataLength и байты.
        private static bool TryFindCanFdDataStart(string[] tokens, int idIndex, out int dataStart, out int dataByteCount)
        {
            dataStart = -1;
            dataByteCount = 0;

            for (int i = idIndex + 1; i <= tokens.Length - 4; i++)
            {
                if (!int.TryParse(tokens[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int brs) || brs is < 0 or > 1)
                    continue;
                if (!int.TryParse(tokens[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int esi) || esi is < 0 or > 1)
                    continue;
                if (!int.TryParse(tokens[i + 2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int dlc) || dlc is < 0 or > 15)
                    continue;
                if (!int.TryParse(tokens[i + 3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int dataLen) || dataLen is < 0 or > 64)
                    continue;
                if (dataLen > 0 && (i + 4 >= tokens.Length || !TryParseHexByte(tokens[i + 4], out _)))
                    continue;

                dataStart = i + 4;
                dataByteCount = dataLen;
                return true;
            }

            return false;
        }

        // Только ID кадра — для списка посылок лога и сканера устройств.
        internal static bool TryParseFrameId(string line, out string id)
            => TryParseFrameId(line, AscFileFormat.Default, out id);

        internal static bool TryParseFrameId(string line, AscFileFormat format, out string id)
        {
            Span<int> bytes = stackalloc int[Device.MaxDataLength];
            return TryParseFrameLine(line, format, out _, out id, bytes, out _);
        }
    }
}
