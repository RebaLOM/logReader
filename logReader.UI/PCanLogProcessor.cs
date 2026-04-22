using System.Globalization;
using System.Linq;
using System.Text;
using ClosedXML.Excel;
using logReader;

namespace logReader.UI
{
    internal class PCanLogProcessor
    {
        public void Process(
            string trcPath,
            List<Device> devices,
            string outputPath,
            OutputFormat outputFormat,
            Action<string> log,
            Dictionary<string, bool>? deviceEnabled = null,
            Dictionary<string, bool[]>? paramEnabled = null)
        {
            if (!File.Exists(trcPath)) { log($"Ошибка: файл не найден: {trcPath}"); return; }

            // Устройства могут переиспользоваться между обработками в UI.
            logReader.Program.ResetDevicesState(devices);

            Encoding encoding;
            try
            {
                encoding = LogFileEncoding.Detect(trcPath);
            }
            catch (Exception ex) { log($"Ошибка определения кодировки: {ex.Message}"); return; }

            DateTime? startTime;
            try
            {
                startTime = TrcLogParser.ParseStartTime(File.ReadLines(trcPath, encoding));
            }
            catch (Exception ex) { log($"Ошибка чтения файла: {ex.Message}"); return; }

            var deviceByID = new Dictionary<string, Device>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in devices)
                deviceByID[d.ID] = d;

            // TimeVal = доля суток (double), как хранит Excel
            var deviceData = new Dictionary<string, List<(double TimeVal, string[] Values)>>(
                StringComparer.OrdinalIgnoreCase);

            Span<int> bytes = stackalloc int[8];
            foreach (var line in File.ReadLines(trcPath, encoding))
            {
                if (!TrcLogParser.TryParseTrcFrameLine(
                        line,
                        out decimal timeMsRaw,
                        out _,
                        out string id,
                        out _,
                        bytes,
                        out int parsedByteCount))
                {
                    continue;
                }

                if (!deviceByID.TryGetValue(id, out Device? device)) continue;

                bool devOn = deviceEnabled == null || deviceEnabled.GetValueOrDefault(id, true);
                if (!devOn) continue;

                for (int i = 0; i < 8; i++)
                    device.RawBytes[i] = i < parsedByteCount ? bytes[i] : 0;

                device.Decode();

                if (!deviceData.ContainsKey(id))
                    deviceData[id] = new List<(double, string[])>();

                double timeMs = (double)timeMsRaw;
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

            if (outputFormat == OutputFormat.Csv)
                BuildCsv(devices, deviceData, deviceEnabled, paramEnabled, outputPath, log);
            else
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

        private static void BuildCsv(
            List<Device> devices,
            Dictionary<string, List<(double TimeVal, string[] Values)>> deviceData,
            Dictionary<string, bool>? deviceEnabled,
            Dictionary<string, bool[]>? paramEnabled,
            string outputPath,
            Action<string> log)
        {
            try
            {
                using var writer = new StreamWriter(outputPath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                var row1 = new List<string>();
                var row2 = new List<string>();
                var visibleDevices = new List<(Device Device, List<int> ParamIndexes)>();
                int maxRows = 0;

                foreach (var device in devices)
                {
                    bool devOn = deviceEnabled == null || deviceEnabled.GetValueOrDefault(device.ID, true);
                    if (!devOn || !deviceData.ContainsKey(device.ID)) continue;

                    var activeParamIndexes = new List<int>();
                    for (int i = 0; i < device.headers.Length; i++)
                    {
                        bool paramOn = paramEnabled == null
                            || !paramEnabled.TryGetValue(device.ID, out var chk)
                            || (i < chk.Length && chk[i]);
                        if (paramOn) activeParamIndexes.Add(i);
                    }
                    visibleDevices.Add((device, activeParamIndexes));
                    maxRows = Math.Max(maxRows, deviceData[device.ID].Count);

                    row1.Add(device.ID);
                    for (int i = 0; i < activeParamIndexes.Count; i++)
                        row1.Add("");

                    row2.Add("Время");
                    foreach (int idx in activeParamIndexes)
                        row2.Add(device.headers[idx]);
                }

                CsvOutput.WriteRow(writer, row1);
                CsvOutput.WriteRow(writer, row2);

                for (int r = 0; r < maxRows; r++)
                {
                    var row = new List<string>();
                    foreach (var entry in visibleDevices)
                    {
                        var rows = deviceData[entry.Device.ID];
                        if (r < rows.Count)
                        {
                            row.Add(rows[r].TimeVal.ToString(CultureInfo.InvariantCulture));
                            foreach (int idx in entry.ParamIndexes)
                            {
                                string val = idx < rows[r].Values.Length ? rows[r].Values[idx] : "";
                                row.Add(CsvOutput.FormatValue(val));
                            }
                        }
                        else
                        {
                            row.Add("");
                            for (int i = 0; i < entry.ParamIndexes.Count; i++)
                                row.Add("");
                        }
                    }
                    CsvOutput.WriteRow(writer, row);
                }

                log("Обработка завершена.");
            }
            catch (Exception ex)
            {
                log($"Ошибка сохранения: {ex.Message}");
            }
        }
    }
}
