using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using logReader;

namespace logReader.UI
{
    // Единый вывод time-series в Excel/CSV для всех процессоров с форматом deviceData.
    internal static class TimeSeriesOutputWriter
    {
        public static void Write(
            OutputFormat format,
            List<Device> devices,
            Dictionary<string, List<(double TimeVal, string[] Values)>> deviceData,
            Dictionary<string, bool>? deviceEnabled,
            Dictionary<string, bool[]>? paramEnabled,
            string outputPath,
            string sheetName,
            bool isCanfox,
            Action<string> log)
        {
            if (format == OutputFormat.Csv)
                WriteCsv(devices, deviceData, deviceEnabled, paramEnabled, outputPath, isCanfox, log);
            else
                WriteExcel(devices, deviceData, deviceEnabled, paramEnabled, outputPath, sheetName, isCanfox, log);
        }

        private static void WriteExcel(
            List<Device> devices,
            Dictionary<string, List<(double TimeVal, string[] Values)>> deviceData,
            Dictionary<string, bool>? deviceEnabled,
            Dictionary<string, bool[]>? paramEnabled,
            string outputPath,
            string sheetName,
            bool isCanfox,
            Action<string> log)
        {
            try
            {
                using var workbook = new XLWorkbook();
                var ws = workbook.Worksheets.Add(sheetName);

                ExcelLayoutBuilder.BuildTimeSeriesHeaders(
                    ws, devices, d => deviceData.ContainsKey(d.ID), deviceEnabled, paramEnabled);

                int col = 1;
                foreach (var device in devices)
                {
                    bool devOn = deviceEnabled == null || deviceEnabled.GetValueOrDefault(device.ID, true);
                    if (!devOn || !deviceData.TryGetValue(device.ID, out var rows)) continue;

                    int paramCols = ExcelLayoutBuilder.GetActiveParamHeaders(device, paramEnabled).Count;

                    for (int r = 0; r < rows.Count; r++)
                    {
                        int excelRow = r + 3;
                        int c = col;

                        // CANfox хранит время как долю суток — показываем как HH:mm:ss.fff, не как число Excel.
                        var timeCell = ws.Cell(excelRow, c++);
                        timeCell.Value = rows[r].TimeVal;
                        if (isCanfox)
                            timeCell.Style.DateFormat.Format = "HH:mm:ss.000";

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

                SafeFileWriter.Write(outputPath, tmp =>
                {
                    ws.Columns().AdjustToContents();
                    workbook.SaveAs(tmp);
                });
                log("Обработка завершена.");
            }
            catch (Exception ex)
            {
                log($"Ошибка сохранения: {ex.Message}");
            }
        }

        private static void WriteCsv(
            List<Device> devices,
            Dictionary<string, List<(double TimeVal, string[] Values)>> deviceData,
            Dictionary<string, bool>? deviceEnabled,
            Dictionary<string, bool[]>? paramEnabled,
            string outputPath,
            bool isCanfox,
            Action<string> log)
        {
            string? tempPath = null;
            try
            {
                tempPath = SafeFileWriter.CreateTempPath(outputPath);
                using (var writer = new StreamWriter(tempPath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)))
                {
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
                                string t = isCanfox
                                    ? FormatHmsFffFromDayFraction(rows[r].TimeVal)
                                    : rows[r].TimeVal.ToString(CultureInfo.InvariantCulture);
                                row.Add(t);
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
                }

                SafeFileWriter.Publish(tempPath, outputPath);
                tempPath = null;
                log("Обработка завершена.");
            }
            catch (Exception ex)
            {
                log($"Ошибка сохранения: {ex.Message}");
            }
            finally
            {
                if (tempPath != null && File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
                }
            }
        }

        private static string FormatHmsFffFromDayFraction(double timeDayFraction)
        {
            var ts = TimeSpan.FromDays(timeDayFraction);
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0:00}:{1:00}:{2:00}.{3:000}",
                (int)Math.Floor(ts.TotalHours),
                ts.Minutes,
                ts.Seconds,
                ts.Milliseconds);
        }
    }
}

