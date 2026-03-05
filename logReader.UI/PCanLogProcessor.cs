using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using logReader;

namespace logReader.UI
{
    internal class PCanLogProcessor
    {
        private static readonly Regex _lineRegex = new Regex(
            @"^\s*\d+\)\s+([\d.]+)\s+\w+\s+([0-9A-Fa-f]+)\s+\d+\s+((?:[0-9A-Fa-f]{2}\s*)+)",
            RegexOptions.Compiled);

        private static bool TryParseHexByte(string hex, out int value)
        {
            if (!int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
                return false;
            return value >= 0 && value <= byte.MaxValue;
        }

        public void Process(
            string trcPath,
            List<Device> devices,
            string outputPath,
            Action<string> log,
            Dictionary<string, bool>? deviceEnabled = null,
            Dictionary<string, bool[]>? paramEnabled = null)
        {
            if (!File.Exists(trcPath)) { log($"Ошибка: файл не найден: {trcPath}"); return; }

            string[] lines;
            try
            {
                Encoding encoding = LogFileEncoding.Detect(trcPath);
                lines = File.ReadAllLines(trcPath, encoding);
            }
            catch (Exception ex) { log($"Ошибка чтения файла: {ex.Message}"); return; }

            DateTime? startTime = ParseStartTime(lines);

            var deviceByID = new Dictionary<string, Device>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in devices)
                deviceByID[d.ID] = d;

            // TimeVal = доля суток (double), как хранит Excel
            var deviceData = new Dictionary<string, List<(double TimeVal, string[] Values)>>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith(';'))
                    continue;

                var match = _lineRegex.Match(line);
                if (!match.Success) continue;

                double timeMs = double.TryParse(match.Groups[1].Value,
                    NumberStyles.Float, CultureInfo.InvariantCulture, out double t) ? t : 0;

                string id = match.Groups[2].Value.ToUpperInvariant();

                if (!deviceByID.TryGetValue(id, out Device? device)) continue;

                bool devOn = deviceEnabled == null || deviceEnabled.GetValueOrDefault(id, true);
                if (!devOn) continue;

                var hexParts = match.Groups[3].Value.Trim()
                    .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (hexParts.Length < 8) continue;

                bool valid = true;
                for (int i = 0; i < 8; i++)
                {
                    if (!TryParseHexByte(hexParts[i], out int v)) { valid = false; break; }
                    device.RawBytes[i] = v;
                }
                if (!valid) continue;

                device.Decode();

                if (!deviceData.ContainsKey(id))
                    deviceData[id] = new List<(double, string[])>();

                double timeVal;
                if (startTime.HasValue)
                {
                    // Абсолютное время = время начала + смещение
                    // Доля суток с полной точностью double (≈0.1 мкс разрешение)
                    timeVal = startTime.Value.AddMilliseconds(timeMs).TimeOfDay.TotalDays;
                }
                else
                {
                    // Нет заголовка — пишем смещение в мс как есть (число)
                    timeVal = timeMs;
                }

                deviceData[id].Add((timeVal, (string[])device.ProcessedData.Clone()));
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
            var ws = workbook.Worksheets.Add("pCAN Log");

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

                    // Время — просто число (доля суток или мс), без форматирования
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

        // Парсит время начала. Приоритет: "Start time: 01.09.2025 16:42:14.469.0"
        // Запасной: ";$STARTTIME=45901.6960007986" (OLE Automation Date)
        private static DateTime? ParseStartTime(IEnumerable<string> lines)
        {
            DateTime? fallback = null;

            foreach (var line in lines)
            {
                if (!line.StartsWith(";")) continue;

                int idx = line.IndexOf("Start time:", StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    string raw = line.Substring(idx + 11).Trim();

                    // "16:42:14.469.0" — две точки после секунд.
                    // Убираем вторую точку: "469.0" → "4690" → формат ffff
                    int lastDot = raw.LastIndexOf('.');
                    int prevDot = lastDot > 0 ? raw.LastIndexOf('.', lastDot - 1) : -1;
                    if (lastDot > 0 && prevDot > 0 && lastDot - prevDot <= 5)
                        raw = raw.Remove(lastDot, 1);

                    if (DateTime.TryParseExact(raw, "dd.MM.yyyy HH:mm:ss.ffff",
                            CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt4))
                        return dt4;
                    if (DateTime.TryParseExact(raw, "dd.MM.yyyy HH:mm:ss.fff",
                            CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt3))
                        return dt3;
                }

                if (line.StartsWith(";$STARTTIME=") && fallback == null)
                {
                    var val = line.Substring(12).Trim();
                    if (double.TryParse(val, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out double oaDate))
                    {
                        try { fallback = DateTime.FromOADate(oaDate); } catch { }
                    }
                }
            }

            return fallback;
        }
    }
}