using System.Linq;
using System.Text;
using ClosedXML.Excel;
using logReader;

namespace logReader.UI
{
    internal class CanLogProcessor
    {
        private static bool TryParseCanByte(string raw, out int value)
        {
            if (!int.TryParse(raw, out value))
                return false;
            return value >= 0 && value <= byte.MaxValue;
        }

        public void Process(
            string csvPath,
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
            if (!File.Exists(csvPath)) { log($"Ошибка: файл лога не найден: {csvPath}"); return; }

            // Устройства кешируются в UI — без сброса второй прогон унаследует прошлые байты.
            logReader.Program.ResetDevicesState(devices);
            composites?.Reset();

            string? outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                log($"Ошибка: директория для сохранения не существует: {outputDir}");
                return;
            }

            var deviceByID = new Dictionary<string, Device>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in devices)
                deviceByID[d.ID] = d;

            Encoding encoding = LogFileEncoding.Detect(csvPath);

            // Проход 1: только ID из лога — без декодирования байт.
            var seenIDs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenSourceIDs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (string line in File.ReadLines(csvPath, encoding))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var p = line.Split(';', 5);
                    if (p.Length < 4) continue;
                    if (int.TryParse(p[3], out int pri) && pri == 1)
                    {
                        string id = p[2].Trim();
                        if (deviceByID.ContainsKey(id))
                            seenIDs.Add(id);
                        if (hasComposites && composites!.IsSourceId(id))
                            seenSourceIDs.Add(id);
                    }
                }
            }
            catch (Exception ex) { log($"Ошибка чтения файла: {ex.Message}"); return; }

            var activeDevices = devices.Where(d => seenIDs.Contains(d.ID)).ToList();

            // Составной блок в вывод — только если в логе был хотя бы один его источник.
            var activeBlocks = new List<CompositeDevice>();
            if (hasComposites)
            {
                foreach (var block in composites!.Blocks)
                {
                    bool blockOn = deviceEnabled == null || deviceEnabled.GetValueOrDefault(block.ID, true);
                    if (!blockOn) continue;
                    if (block.Signals.Any(s => s.Pieces.Any(pc => seenSourceIDs.Contains(pc.SourceId))))
                        activeBlocks.Add(block);
                }
            }

            var outputDevices = new List<Device>(activeDevices);
            outputDevices.AddRange(activeBlocks);

            if (outputDevices.Count == 0)
            {
                log("Нет совпадающих устройств — проверьте файл посылок.");
                return;
            }

            void RefreshComposites()
            {
                foreach (var block in activeBlocks)
                    block.Decode();
            }

            string? csvTempPath = null;
            StreamWriter? csvWriter = null;
            if (outputFormat == OutputFormat.Csv)
            {
                csvTempPath = SafeFileWriter.CreateTempPath(outputPath);
                csvWriter = new StreamWriter(csvTempPath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            }

            using var workbook = outputFormat == OutputFormat.Xlsx ? new XLWorkbook() : null;

            IXLWorksheet? ws = null;
            int excelRow = 0;
            if (outputFormat == OutputFormat.Csv)
                WriteCsvHeaders(csvWriter!, outputDevices, deviceEnabled, paramEnabled);
            else
            {
                ws = workbook!.Worksheets.Add("Log");
                excelRow = logReader.Program.BuildExcelHeaders(ws, outputDevices, deviceEnabled, paramEnabled);
            }

            int currentStep = 0;
            string currentTime = "";
            bool firstStep = true;
            int[] msgBytes = new int[8];

            foreach (string line in File.ReadLines(csvPath, encoding))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split(';');
                if (parts.Length < 3) continue;

                // Сначала смена шага (запись предыдущего), затем декод текущей строки.
                if (!string.IsNullOrWhiteSpace(parts[0]))
                {
                    if (!int.TryParse(parts[0], out int newStep)) continue;
                    string newTime = parts.Length > 1 ? parts[1] : "";

                    if (!firstStep)
                    {
                        RefreshComposites();
                        if (outputFormat == OutputFormat.Csv)
                            WriteCsvDataRow(csvWriter!, currentStep, currentTime, outputDevices, deviceEnabled, paramEnabled);
                        else
                            excelRow = logReader.Program.BuildExcelRow(
                                ws!, excelRow, currentStep, currentTime,
                                outputDevices, deviceEnabled, paramEnabled);
                    }

                    currentStep = newStep;
                    currentTime = newTime;
                    firstStep = false;
                }

                int priority = 0;
                if (parts.Length > 3) int.TryParse(parts[3], out priority);

                string id = parts[2].Trim();
                if (priority == 1 && parts.Length >= 12)
                {
                    bool valid = true;
                    for (int i = 0; i < 8; i++)
                    {
                        if (!TryParseCanByte(parts[4 + i], out int v)) { valid = false; break; }
                        msgBytes[i] = v;
                    }

                    if (valid)
                    {
                        if (deviceByID.TryGetValue(id, out Device? dev))
                        {
                            Array.Copy(msgBytes, dev.RawBytes, 8);
                            dev.Decode();
                        }
                        composites?.OnMessage(id, msgBytes, 8);
                    }
                }
            }

            if (!firstStep)
            {
                RefreshComposites();
                if (outputFormat == OutputFormat.Csv)
                    WriteCsvDataRow(csvWriter!, currentStep, currentTime, outputDevices, deviceEnabled, paramEnabled);
                else
                    logReader.Program.BuildExcelRow(
                        ws!, excelRow, currentStep, currentTime,
                        outputDevices, deviceEnabled, paramEnabled);
            }

            try
            {
                if (outputFormat == OutputFormat.Csv)
                {
                    csvWriter!.Flush();
                    csvWriter.Dispose();
                    csvWriter = null;
                    SafeFileWriter.Publish(csvTempPath!, outputPath);
                    csvTempPath = null;
                }
                else
                {
                    SafeFileWriter.Write(outputPath, tmp =>
                    {
                        ws!.Columns().AdjustToContents();
                        workbook!.SaveAs(tmp);
                    });
                }
                log("Обработка завершена.");
            }
            catch (Exception ex)
            {
                log($"Ошибка сохранения файла: {ex.Message}");
            }
            finally
            {
                csvWriter?.Dispose();
                if (csvTempPath != null && File.Exists(csvTempPath))
                {
                    try { File.Delete(csvTempPath); }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
                }
            }
        }

        private static void WriteCsvHeaders(
            StreamWriter writer,
            List<Device> activeDevices,
            Dictionary<string, bool>? deviceEnabled,
            Dictionary<string, bool[]>? paramEnabled)
        {
            var row1 = new List<string> { "Шаг", "Время" };
            var row2 = new List<string> { "", "" };

            foreach (var device in activeDevices)
            {
                bool devOn = deviceEnabled == null || deviceEnabled.GetValueOrDefault(device.ID, true);
                if (!devOn) continue;

                var activeParams = new List<string>();
                for (int i = 0; i < device.headers.Length; i++)
                {
                    bool paramOn = paramEnabled == null
                        || !paramEnabled.TryGetValue(device.ID, out var arr)
                        || (i < arr.Length && arr[i]);
                    if (paramOn) activeParams.Add(device.headers[i]);
                }
                if (activeParams.Count == 0) continue;

                row1.Add(device.ID);
                for (int i = 1; i < activeParams.Count; i++)
                    row1.Add("");

                row2.AddRange(activeParams);
            }

            CsvOutput.WriteRow(writer, row1);
            CsvOutput.WriteRow(writer, row2);
        }

        private static void WriteCsvDataRow(
            StreamWriter writer,
            int step,
            string time,
            List<Device> activeDevices,
            Dictionary<string, bool>? deviceEnabled,
            Dictionary<string, bool[]>? paramEnabled)
        {
            var row = new List<string> { step.ToString(), time };
            foreach (var device in activeDevices)
            {
                bool devOn = deviceEnabled == null || deviceEnabled.GetValueOrDefault(device.ID, true);
                if (!devOn) continue;

                for (int i = 0; i < device.ProcessedData.Length; i++)
                {
                    bool paramOn = paramEnabled == null
                        || !paramEnabled.TryGetValue(device.ID, out var arr)
                        || (i < arr.Length && arr[i]);
                    if (!paramOn) continue;
                    row.Add(CsvOutput.FormatValue(device.ProcessedData[i]));
                }
            }
            CsvOutput.WriteRow(writer, row);
        }
    }
}