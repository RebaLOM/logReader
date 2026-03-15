using System.Globalization;
using System.Text;
using ClosedXML.Excel;
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
        {
            id = "";
            if (string.IsNullOrWhiteSpace(raw)) return false;

            string token = raw.Trim();
            if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                token = token.Substring(2);

            if (token.EndsWith("x", StringComparison.OrdinalIgnoreCase))
                token = token.Substring(0, token.Length - 1);

            if (token.Length < 3) return false;

            for (int i = 0; i < token.Length; i++)
            {
                char c = token[i];
                bool isHex = (c >= '0' && c <= '9')
                    || (c >= 'a' && c <= 'f')
                    || (c >= 'A' && c <= 'F');
                if (!isHex) return false;
            }

            id = token.ToUpperInvariant();
            return true;
        }

        internal static bool TryParseHexByte(string hex, out int value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(hex)) return false;
            if (hex.Length != 2) return false;

            if (!int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
                return false;
            return value >= 0 && value <= byte.MaxValue;
        }

        internal static bool TryParseFrameLine(
            string line,
            out long offsetTicks,
            out string id,
            Span<int> bytes)
        {
            offsetTicks = 0;
            id = "";

            if (string.IsNullOrWhiteSpace(line)) return false;
            string trimmed = line.TrimStart();

            // Header / comments
            if (trimmed.StartsWith("//")) return false;
            if (trimmed.StartsWith("date", StringComparison.OrdinalIgnoreCase)) return false;
            if (trimmed.StartsWith("base", StringComparison.OrdinalIgnoreCase)) return false;
            if (trimmed.StartsWith("no internal events", StringComparison.OrdinalIgnoreCase)) return false;

            // Frames should start with offset seconds (digit)
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

            // Find first sequence of 8 hex bytes after ID
            for (int start = idIndex + 1; start + 7 < tokens.Length; start++)
            {
                bool ok = true;
                for (int j = 0; j < 8; j++)
                {
                    if (!TryParseHexByte(tokens[start + j], out int v))
                    {
                        ok = false;
                        break;
                    }
                    bytes[j] = v;
                }
                if (ok) return true;
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
            Action<string> log,
            Dictionary<string, bool>? deviceEnabled = null,
            Dictionary<string, bool[]>? paramEnabled = null)
        {
            if (devices.Count == 0) { log("Ошибка: устройства не загружены."); return; }
            if (!File.Exists(ascPath)) { log($"Ошибка: файл не найден: {ascPath}"); return; }

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
                    if (!AscLogParser.TryParseFrameLine(line, out long offsetTicks, out string id, bytes))
                        continue;

                    if (!deviceByID.TryGetValue(id, out Device? device))
                        continue;

                    bool devOn = deviceEnabled == null || deviceEnabled.GetValueOrDefault(id, true);
                    if (!devOn) continue;

                    for (int i = 0; i < 8; i++)
                        device.RawBytes[i] = bytes[i];

                    device.Decode();

                    long ticks = baseTicks + offsetTicks;
                    ticks %= TimeSpan.TicksPerDay;
                    if (ticks < 0) ticks += TimeSpan.TicksPerDay;

                    double timeVal = ticks / (double)TimeSpan.TicksPerDay;

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

            BuildExcel(devices, deviceData, deviceEnabled, paramEnabled, outputPath, log);
        }

        private static void BuildExcel(
            List<Device> devices,
            Dictionary<string, List<(double TimeVal, string[] Values)>> deviceData,
            Dictionary<string, bool>? deviceEnabled,
            Dictionary<string, bool[]>? paramEnabled,
            string outputPath,
            Action<string> log)
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("ASC Log");

            var colors = logReader.Program.DeviceColors;
            int colorIdx = 0;
            int col = 1;

            // ── Заголовки ─────────────────────────────────────────────────
            foreach (var device in devices)
            {
                bool devOn = deviceEnabled == null || deviceEnabled.GetValueOrDefault(device.ID, true);
                if (!devOn || !deviceData.ContainsKey(device.ID)) continue;

                var activeParams = new List<string>();
                for (int i = 0; i < device.headers.Length; i++)
                {
                    bool paramOn = paramEnabled == null
                        || !paramEnabled.TryGetValue(device.ID, out var chk)
                        || (i < chk.Length && chk[i]);
                    if (paramOn) activeParams.Add(device.headers[i]);
                }

                XLColor bg = colors[colorIdx % colors.Length];
                XLColor bgDark = XLColor.FromArgb(
                    Math.Max(bg.Color.R - 30, 0),
                    Math.Max(bg.Color.G - 30, 0),
                    Math.Max(bg.Color.B - 30, 0));
                colorIdx++;

                // Строка 1 — ID устройства, объединённая ячейка
                int devStartCol = col;
                int devEndCol = col + activeParams.Count; // +1 за время

                if (devStartCol < devEndCol)
                    ws.Range(1, devStartCol, 1, devEndCol).Merge();

                var devCell = ws.Cell(1, devStartCol);
                devCell.Value = device.ID;
                devCell.Style.Font.Bold = true;
                devCell.Style.Fill.BackgroundColor = bgDark;
                devCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                devCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                devCell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

                // Строка 2 — Время
                StyleHeader(ws.Cell(2, col), "Время", bg);
                col++;

                // Строка 2 — параметры
                foreach (var header in activeParams)
                {
                    StyleHeader(ws.Cell(2, col), header, bg);
                    col++;
                }
            }

            // ── Данные ────────────────────────────────────────────────────
            col = 1;
            foreach (var device in devices)
            {
                bool devOn = deviceEnabled == null || deviceEnabled.GetValueOrDefault(device.ID, true);
                if (!devOn || !deviceData.TryGetValue(device.ID, out var rows)) continue;

                // Сколько колонок занимает это устройство
                int paramCols = 0;
                for (int i = 0; i < device.headers.Length; i++)
                {
                    bool paramOn = paramEnabled == null
                        || !paramEnabled.TryGetValue(device.ID, out var arr2)
                        || (i < arr2.Length && arr2[i]);
                    if (paramOn) paramCols++;
                }

                for (int r = 0; r < rows.Count; r++)
                {
                    int excelRow = r + 3;
                    int c = col;

                    // Время — число (доля суток)
                    ws.Cell(excelRow, c++).Value = rows[r].TimeVal;

                    for (int i = 0; i < device.headers.Length; i++)
                    {
                        bool paramOn = paramEnabled == null
                            || !paramEnabled.TryGetValue(device.ID, out var arr)
                            || (i < arr.Length && arr[i]);
                        if (!paramOn) continue;

                        string val = i < rows[r].Values.Length ? rows[r].Values[i] : "";
                        if (double.TryParse(val, NumberStyles.Float,
                            CultureInfo.InvariantCulture, out double d))
                            ws.Cell(excelRow, c).Value = d;
                        else
                            ws.Cell(excelRow, c).Value = val;
                        c++;
                    }
                }

                col += 1 + paramCols;
            }

            ws.Columns().AdjustToContents();
            ws.SheetView.FreezeRows(2);

            try { workbook.SaveAs(outputPath); log("Обработка завершена."); }
            catch (Exception ex) { log($"Ошибка сохранения: {ex.Message}"); }
        }

        private static void StyleHeader(IXLCell cell, string text, XLColor bg)
        {
            cell.Value = text;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = bg;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Alignment.WrapText = true;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }
    }
}
