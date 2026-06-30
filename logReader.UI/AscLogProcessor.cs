using System.Globalization;
using System.Text;
using logReader;

namespace logReader.UI
{
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

        internal static bool TryParseBaseTimeTicksFromHeaderLine(string line, out long baseTicks)
        {
            baseTicks = 0;

            if (string.IsNullOrWhiteSpace(line)) return false;
            var trimmed = line.TrimStart();
            if (!trimmed.StartsWith("date", StringComparison.OrdinalIgnoreCase)) return false;

            var tokens = trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            foreach (var t in tokens)
            {
                if (!t.Contains(':')) continue;
                if (TryParseTimeOfDayTicks(t, out baseTicks)) return true;
            }

            return false;
        }

        internal static bool TryParseTimeOfDayTicks(string raw, out long ticks)
        {
            ticks = 0;
            if (string.IsNullOrWhiteSpace(raw)) return false;

            string normalized = raw.Trim().TrimEnd(',');
            normalized = normalized.Replace(',', '.');

            if (DateTime.TryParseExact(
                normalized,
                TimeFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime dt))
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

            if (!decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal sec)
                && !decimal.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out sec))
                return false;

            decimal tickDecimal = sec * TimeSpan.TicksPerSecond;
            tickDecimal = decimal.Round(tickDecimal, 0, MidpointRounding.AwayFromZero);

            if (tickDecimal > long.MaxValue || tickDecimal < long.MinValue) return false;
            ticks = (long)tickDecimal;
            return true;
        }

        internal static bool TryNormalizeIdToken(string raw, out string id)
            => CanToken.TryNormalizeId(raw, out id, allowTrailingX: true, minHexLength: 3);

        internal static bool TryParseHexByte(string hex, out int value)
            => CanToken.TryParseHexByte(hex, out value, requireTwoChars: true);

        internal static bool TryParseFrameLine(
            string line,
            out long offsetTicks,
            out string id,
            Span<int> bytes,
            out int parsedByteCount)
        {
            offsetTicks = 0;
            id = "";
            parsedByteCount = 0;

            if (string.IsNullOrWhiteSpace(line)) return false;
            string trimmed = line.TrimStart();

            if (trimmed.StartsWith("//")) return false;
            if (trimmed.StartsWith("date", StringComparison.OrdinalIgnoreCase)) return false;
            if (trimmed.StartsWith("base", StringComparison.OrdinalIgnoreCase)) return false;
            if (trimmed.StartsWith("no internal events", StringComparison.OrdinalIgnoreCase)) return false;

            if (trimmed.Length == 0 || (!char.IsDigit(trimmed[0]) && trimmed[0] != '-' && trimmed[0] != '+'))
                return false;

            var tokens = trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 2) return false;

            if (!TryParseOffsetSecondsToTicks(tokens[0], out offsetTicks)) return false;

            int idIndex = -1;
            for (int i = 1; i < tokens.Length; i++)
            {
                if (TryNormalizeIdToken(tokens[i], out id))
                {
                    idIndex = i;
                    break;
                }
            }
            if (idIndex < 0) return false;

            bool isCanFd = tokens[1].Equals("CANFD", StringComparison.OrdinalIgnoreCase);
            if (isCanFd)
            {
                if (!TryFindCanFdDataStart(tokens, idIndex, out int fdDataStart, out int dataByteCount))
                    return false;

                int toRead = Math.Min(dataByteCount, bytes.Length);
                for (int i = 0; i < toRead; i++)
                {
                    if (fdDataStart + i >= tokens.Length) return false;
                    if (!TryParseHexByte(tokens[fdDataStart + i], out int v)) return false;
                    bytes[parsedByteCount++] = v;
                }

                return parsedByteCount > 0;
            }

            // Классический ASC: DLC после маркера «d»; иначе — до восьми подряд идущих hex-байт.
            int dlc = -1;
            for (int i = idIndex + 1; i < tokens.Length - 1; i++)
            {
                if (tokens[i].Equals("d", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(tokens[i + 1], NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out int parsed))
                {
                    dlc = parsed;
                    break;
                }
            }

            int dataStart = idIndex + 1;
            if (dlc >= 0)
            {
                for (int i = idIndex + 1; i < tokens.Length - 1; i++)
                {
                    if (tokens[i].Equals("d", StringComparison.OrdinalIgnoreCase))
                    {
                        dataStart = i + 2;
                        break;
                    }
                }
            }

            for (int i = dataStart; i < tokens.Length && parsedByteCount < bytes.Length; i++)
            {
                if (!TryParseHexByte(tokens[i], out int v)) break;
                bytes[parsedByteCount++] = v;
            }

            int expected = dlc >= 0 ? Math.Min(dlc, bytes.Length) : 0;
            if (expected > 0 && parsedByteCount < expected) return false;

            return parsedByteCount > 0;
        }

        // Vector CAN FD ASC: после ID возможно символьное имя, затем BRS ESI DLC DataLength и байты.
        private static bool TryFindCanFdDataStart(
            string[] tokens,
            int idIndex,
            out int dataStart,
            out int dataByteCount)
        {
            dataStart = -1;
            dataByteCount = 0;

            for (int i = idIndex + 1; i <= tokens.Length - 5; i++)
            {
                if (!int.TryParse(tokens[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                    continue;
                if (!int.TryParse(tokens[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                    continue;
                if (!int.TryParse(tokens[i + 2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int dlc))
                    continue;
                if (!int.TryParse(tokens[i + 3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int dataLen))
                    continue;
                if (dlc is < 0 or > 15) continue;
                if (dataLen is < 0 or > 64) continue;
                if (dataLen > 0 && !TryParseHexByte(tokens[i + 4], out _))
                    continue;

                dataStart = i + 4;
                dataByteCount = dataLen;
                return true;
            }

            return false;
        }

        // Только ID кадра — для списка посылок лога и сканера устройств.
        internal static bool TryParseFrameId(string line, out string id)
        {
            id = "";
            if (string.IsNullOrWhiteSpace(line)) return false;
            string trimmed = line.TrimStart();

            if (trimmed.StartsWith("//")) return false;
            if (trimmed.StartsWith("date", StringComparison.OrdinalIgnoreCase)) return false;
            if (trimmed.StartsWith("base", StringComparison.OrdinalIgnoreCase)) return false;
            if (trimmed.StartsWith("no internal events", StringComparison.OrdinalIgnoreCase)) return false;

            if (trimmed.Length == 0 || (!char.IsDigit(trimmed[0]) && trimmed[0] != '-' && trimmed[0] != '+'))
                return false;

            var tokens = trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 2) return false;
            if (!TryParseOffsetSecondsToTicks(tokens[0], out _)) return false;

            for (int i = 1; i < tokens.Length; i++)
            {
                if (TryNormalizeIdToken(tokens[i], out id))
                    return true;
            }

            return false;
        }
    }

    internal class AscLogProcessor
    {
        public void Process(
            string ascPath,
            List<Device> devices,
            string outputPath,
            OutputFormat outputFormat,
            Action<string> log,
            Dictionary<string, bool>? deviceEnabled = null,
            Dictionary<string, bool[]>? paramEnabled = null,
            CompositeRuntime? composites = null)
        {
            bool hasComposites = composites != null && !composites.IsEmpty;
            if (devices.Count == 0 && !hasComposites) { log("Ошибка: устройства не загружены."); return; }
            if (!File.Exists(ascPath)) { log($"Ошибка: файл не найден: {ascPath}"); return; }

            // Устройства кешируются в UI — сброс перед каждым прогоном.
            logReader.Program.ResetDevicesState(devices);
            composites?.Reset();

            Encoding encoding;
            try
            {
                encoding = LogFileEncoding.Detect(ascPath);
            }
            catch (Exception ex)
            {
                log($"Ошибка определения кодировки: {ex.Message}");
                return;
            }

            long baseTicks = 0;
            bool hasBaseTime = false;
            try
            {
                foreach (var line in File.ReadLines(ascPath, encoding))
                {
                    if (AscLogParser.TryParseBaseTimeTicksFromHeaderLine(line, out baseTicks))
                    {
                        hasBaseTime = true;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                log($"Ошибка чтения файла: {ex.Message}");
                return;
            }

            if (!hasBaseTime)
                log("Предупреждение: не найдено стартовое время (строка 'date ...'). Время будет считаться от 00:00:00.000.");

            var deviceByID = new Dictionary<string, Device>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in devices)
                deviceByID[d.ID] = d;

            var deviceData = new Dictionary<string, List<(double TimeVal, string[] Values)>>(
                StringComparer.OrdinalIgnoreCase);

            try
            {
                Span<int> bytes = stackalloc int[8];

                foreach (var line in File.ReadLines(ascPath, encoding))
                {
                    if (!AscLogParser.TryParseFrameLine(line, out long offsetTicks, out string id, bytes, out int parsedByteCount))
                        continue;

                    // Суммируем смещение без сброса в полночь — иначе ломаются логи через 00:00.
                    long ticks = baseTicks + offsetTicks;
                    double timeVal = ticks / (double)TimeSpan.TicksPerDay;

                    composites?.OnMessage(id, bytes, parsedByteCount);
                    CompositeOutput.EmitTriggered(composites, id, timeVal, deviceData);

                    if (!deviceByID.TryGetValue(id, out Device? device))
                        continue;

                    bool devOn = deviceEnabled == null || deviceEnabled.GetValueOrDefault(id, true);
                    if (!devOn) continue;

                    for (int i = 0; i < 8; i++)
                        device.RawBytes[i] = i < parsedByteCount ? bytes[i] : 0;

                    device.Decode();

                    if (!deviceData.ContainsKey(id))
                        deviceData[id] = new List<(double, string[])>();

                    deviceData[id].Add((timeVal, (string[])device.ProcessedData.Clone()));
                }
            }
            catch (Exception ex)
            {
                log($"Ошибка обработки файла: {ex.Message}");
                return;
            }

            if (deviceData.Count == 0)
            {
                log("Нет совпадающих устройств — проверьте файл посылок.");
                return;
            }

            var outputDevices = CompositeOutput.WithComposites(devices, composites);
            TimeSeriesOutputWriter.Write(
                outputFormat, outputDevices, deviceData, deviceEnabled, paramEnabled,
                outputPath, "ASC Log", isCanfox: false, log);
        }
    }
}
