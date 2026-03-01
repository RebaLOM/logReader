using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using logReader;

namespace logReader.UI
{
    internal class PCanLogProcessor
    {
        // Паттерн строки данных:
        // "     1)         2.0  Rx     1801D0EF  8  00 03 D4 17 40 3A 20 4E"
        private static readonly Regex _lineRegex = new Regex(
            @"^\s*\d+\)\s+([\d.]+)\s+\w+\s+([0-9A-Fa-f]+)\s+\d+\s+((?:[0-9A-Fa-f]{2}\s*)+)",
            RegexOptions.Compiled);

        private static bool TryParseHexCanByte(string rawHex, out int value)
        {
            if (!int.TryParse(rawHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
                return false;

            return value >= 0 && value <= byte.MaxValue;
        }

        public void Process(
            string csvPath,
            List<Device> devices,
            string outputPath,
            Action<string> log,
            Dictionary<string, bool>? deviceEnabled = null,
            Dictionary<string, bool[]>? paramEnabled = null)
        {
            if (!File.Exists(csvPath))
            {
                log($"Ошибка: файл не найден: {csvPath}");
                return;
            }

            List<string> lines;
            try
            {
                Encoding encoding = LogFileEncoding.Detect(csvPath);
                lines = File.ReadLines(csvPath, encoding).ToList();
            }
            catch (Exception ex) { log($"Ошибка чтения файла: {ex.Message}"); return; }

            // Читаем время старта из заголовка
            DateTime? startTime = ParseStartTime(lines);
            // Строим словарь устройств
            var deviceByID = new Dictionary<string, Device>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in devices)
                deviceByID[d.ID] = d;

            // Для каждого устройства храним список (время как доля суток для Excel, данные[])
            var deviceData = new Dictionary<string, List<(double TimeVal, string[] Values)>>();

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith(';'))
                    continue;

                var match = _lineRegex.Match(line);
                if (!match.Success) { continue; }

                double timeMs = double.TryParse(match.Groups[1].Value,
                    NumberStyles.Float, CultureInfo.InvariantCulture, out double t) ? t : 0;

                string id = match.Groups[2].Value.ToUpperInvariant();

                if (!deviceByID.TryGetValue(id, out Device? device))
                    continue;

                // Проверяем фильтр устройства
                bool devOn = deviceEnabled == null || deviceEnabled.GetValueOrDefault(id, true);
                if (!devOn) continue;

                // Парсим hex байты
                var hexParts = match.Groups[3].Value.Trim().Split(
                    new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                if (hexParts.Length < 8)
                    continue;

                bool validPayload = true;
                for (int i = 0; i < 8; i++)
                {
                    if (!TryParseHexCanByte(hexParts[i], out int value))
                    {
                        validPayload = false;
                        break;
                    }

                    device.RawBytes[i] = value;
                }

                if (!validPayload)
                    continue;

                device.Decode();

                if (!deviceData.ContainsKey(id))
                    deviceData[id] = new List<(double, string[])>();

                // Абсолютное время = время начала + смещение в мс
                // Для Excel: время хранится как доля суток (0.0=00:00, 0.5=12:00, 1.0=24:00)
                DateTime absTime = (startTime ?? DateTime.MinValue).AddMilliseconds(timeMs);
                double timeVal = absTime.TimeOfDay.TotalDays;
                deviceData[id].Add((timeVal, (string[])device.ProcessedData.Clone()));
            }

            if (deviceData.Count == 0)
            {
                log("Нет совпадающих устройств — проверьте файл посылок.");
                return;
            }

            // Строим Excel с индивидуальными колонками времени для каждого устройства
            BuildExcel(devices, deviceData, deviceEnabled, paramEnabled, outputPath, log);
        }

        private void BuildExcel(
            List<Device> devices,
            Dictionary<string, List<(double TimeVal, string[] Values)>> deviceData,
            Dictionary<string, bool>? deviceEnabled,
            Dictionary<string, bool[]>? paramEnabled,
            string outputPath,
            Action<string> log)
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("pCAN Log");

            int col = 1;

            // Заголовки — для каждого устройства: время + параметры
            foreach (var device in devices)
            {
                bool devOn = deviceEnabled == null || deviceEnabled.GetValueOrDefault(device.ID, true);
                if (!devOn) continue;

                if (!deviceData.ContainsKey(device.ID)) continue;

                // Строка 1 — ID устройства, растянутый на все его колонки
                var activeParams = new List<string>();
                for (int i = 0; i < device.headers.Length; i++)
                {
                    bool paramOn = paramEnabled == null
                        || !paramEnabled.TryGetValue(device.ID, out var chk)
                        || (i < chk.Length && chk[i]);
                    if (paramOn) activeParams.Add(device.headers[i]);
                }

                int devStartCol = col;
                int devEndCol = col + activeParams.Count; // +1 за колонку времени
                if (devStartCol < devEndCol)
                {
                    ws.Range(1, devStartCol, 1, devEndCol).Merge();
                    ws.Cell(1, devStartCol).Value = device.ID;
                }
                else
                {
                    ws.Cell(1, devStartCol).Value = device.ID;
                }
                var devCell = ws.Cell(1, devStartCol);
                devCell.Style.Font.Bold = true;
                devCell.Style.Fill.BackgroundColor = XLColor.FromArgb(170, 195, 235);
                devCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                devCell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

                // Строка 2 — Время
                var timeCell = ws.Cell(2, col);
                timeCell.Value = $"Время";
                timeCell.Style.Font.Bold = true;
                timeCell.Style.Fill.BackgroundColor = XLColor.FromArgb(200, 220, 255);
                timeCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                timeCell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                ws.Column(col).Width = 14;
                col++;

                // Строка 2 — параметры
                foreach (var header in activeParams)
                {
                    var hCell = ws.Cell(2, col);
                    hCell.Value = header;
                    hCell.Style.Font.Bold = true;
                    hCell.Style.Fill.BackgroundColor = XLColor.FromArgb(220, 235, 255);
                    hCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    hCell.Style.Alignment.WrapText = true;
                    hCell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    ws.Column(col).Width = Math.Max(header.Length * 1.1, 12);
                    col++;
                }
            }

            // Данные — каждое устройство в своих колонках независимо
            col = 1;
            foreach (var device in devices)
            {
                bool devOn = deviceEnabled == null || deviceEnabled.GetValueOrDefault(device.ID, true);
                if (!devOn) continue;

                if (!deviceData.TryGetValue(device.ID, out var rows)) continue;

                for (int r = 0; r < rows.Count; r++)
                {
                    int excelRow = r + 3;
                    int c = col;

                    var tCell = ws.Cell(excelRow, c++);
                    tCell.Value = rows[r].TimeVal;
                    tCell.Style.NumberFormat.Format = "hh:mm:ss.000";

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

                // Считаем сколько колонок занимает это устройство
                int paramCols = device.headers.Length;
                if (paramEnabled != null && paramEnabled.TryGetValue(device.ID, out var pArr))
                    paramCols = pArr.Count(v => v);

                col += 1 + paramCols; // +1 за колонку времени
            }

            // Автоширина и заморозка заголовков
            ws.Columns().AdjustToContents();
            ws.SheetView.FreezeRows(2);

            try
            {
                workbook.SaveAs(outputPath);
                log("Обработка завершена.");
            }
            catch (Exception ex)
            {
                log($"Ошибка сохранения: {ex.Message}");
            }
        }

        // Парсит время начала из заголовка .trc файла.
        // Приоритет: строка ";   Start time: 01.09.2025 16:42:14.469.0"
        // Запасной вариант: ";$STARTTIME=45901.6960007986" (OLE Automation Date)
        private static DateTime? ParseStartTime(IEnumerable<string> lines)
        {
            DateTime? fallback = null;

            foreach (var line in lines)
            {
                if (!line.StartsWith(";")) continue;

                // Основной формат: ";   Start time: 01.09.2025 16:42:14.469.0"
                int idx = line.IndexOf("Start time:", StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    // Убираем лишний ".0" в конце если есть: "16:42:14.469.0" → "16:42:14.469"
                    string raw = line.Substring(idx + 11).Trim();
                    int lastDot = raw.LastIndexOf('.');
                    int prevDot = raw.LastIndexOf('.', lastDot - 1);
                    // Если два разряда после последней точки — это лишний суффикс
                    if (lastDot > prevDot && lastDot - prevDot <= 4)
                        raw = raw.Substring(0, lastDot);

                    if (DateTime.TryParseExact(raw, "dd.MM.yyyy HH:mm:ss.fff",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt))
                        return dt;
                }

                // Запасной: ";$STARTTIME=45901.6960007986"
                if (line.StartsWith(";$STARTTIME=") && fallback == null)
                {
                    var val = line.Substring(12).Trim();
                    if (double.TryParse(val, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out double oaDate))
                    {
                        try { fallback = DateTime.FromOADate(oaDate); }
                        catch { }
                    }
                }
            }

            return fallback;
        }
    }
}
