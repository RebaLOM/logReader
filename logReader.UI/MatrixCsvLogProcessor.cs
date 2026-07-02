using System.Linq;
using System.Text;
using ClosedXML.Excel;
using logReader;

namespace logReader.UI
{
    internal sealed class MatrixCsvLogProcessor
    {
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
            ProcessMerged(
                new[] { csvPath }, devices, outputPath, outputFormat, log,
                deviceEnabled, paramEnabled, composites);
        }

        public bool ProcessMerged(
            IReadOnlyList<string> csvPaths,
            List<Device> devices,
            string outputPath,
            OutputFormat outputFormat,
            Action<string> log,
            Dictionary<string, bool>? deviceEnabled = null,
            Dictionary<string, bool[]>? paramEnabled = null,
            CompositeRuntime? composites = null)
        {
            bool hasComposites = composites != null && !composites.IsEmpty;
            if (devices.Count == 0 && !hasComposites)
            {
                log("Ошибка: устройства не загружены.");
                return false;
            }

            if (csvPaths.Count == 0)
            {
                log("Ошибка: не указаны файлы CSV.");
                return false;
            }

            string? outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                log($"Ошибка: директория для сохранения не существует: {outputDir}");
                return false;
            }

            var deviceByID = new Dictionary<string, Device>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in devices)
                deviceByID[d.ID] = d;

            var seenIDs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenSourceIDs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int usedFiles = 0;

            foreach (string csvPath in csvPaths)
            {
                if (!File.Exists(csvPath))
                {
                    log($"Пропуск: файл не найден: {csvPath}");
                    continue;
                }

                Encoding encoding;
                try { encoding = LogFileEncoding.Detect(csvPath); }
                catch (Exception ex)
                {
                    log($"Пропуск: ошибка определения кодировки ({Path.GetFileName(csvPath)}): {ex.Message}");
                    continue;
                }

                if (!MatrixCsvLogParser.LooksLikeMatrixCsv(csvPath, encoding))
                {
                    log($"Пропуск: не {LogFormatUiNames.Csv} — {Path.GetFileName(csvPath)}");
                    continue;
                }

                if (!TryReadHeaderLine(csvPath, encoding, out _, out List<string> headerIds, out string? headerError))
                {
                    log($"Пропуск ({Path.GetFileName(csvPath)}): {headerError ?? "не удалось прочитать заголовок."}");
                    continue;
                }

                foreach (string id in headerIds)
                {
                    if (deviceByID.ContainsKey(id))
                        seenIDs.Add(id);
                    if (hasComposites && composites!.IsSourceId(id))
                        seenSourceIDs.Add(id);
                }

                usedFiles++;
            }

            if (usedFiles == 0)
            {
                log($"Ошибка: не найдено пригодных файлов {LogFormatUiNames.Csv}.");
                return false;
            }

            var activeDevices = devices.Where(d => seenIDs.Contains(d.ID)).ToList();

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
                return false;
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

            int step = 0;
            int[] msgBytes = new int[8];
            TimeSpan timeOffset = TimeSpan.Zero;
            int processedFiles = 0;

            try
            {
                foreach (string csvPath in csvPaths)
                {
                    if (!File.Exists(csvPath)) continue;

                    Encoding encoding;
                    try { encoding = LogFileEncoding.Detect(csvPath); }
                    catch { continue; }

                    if (!MatrixCsvLogParser.LooksLikeMatrixCsv(csvPath, encoding)) continue;
                    if (!TryReadHeaderLine(csvPath, encoding, out List<MatrixCsvColumn> columns, out _, out _))
                        continue;

                    log($"--- {Path.GetFileName(csvPath)} ---");
                    processedFiles++;

                    logReader.Program.ResetDevicesState(devices);
                    composites?.Reset();

                    var timeTracker = new MatrixCsvTimeTracker();
                    bool headerSkipped = false;
                    TimeSpan maxTimeInFile = TimeSpan.Zero;
                    int rowsInFile = 0;

                    foreach (string line in File.ReadLines(csvPath, encoding))
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        if (!headerSkipped)
                        {
                            headerSkipped = true;
                            continue;
                        }

                        string[] parts = line.Split(';');
                        if (parts.Length < 2) continue;
                        if (!timeTracker.TryAdvance(parts[0], out TimeSpan absoluteTime))
                            continue;

                        absoluteTime = absoluteTime.Add(timeOffset);
                        if (absoluteTime > maxTimeInFile)
                            maxTimeInFile = absoluteTime;

                        step++;
                        rowsInFile++;
                        string timeText = MatrixCsvLogParser.FormatTimeWithMs(absoluteTime);

                        foreach (var column in columns)
                        {
                            if (column.ColumnIndex >= parts.Length) continue;
                            string cell = parts[column.ColumnIndex];
                            if (MatrixCsvLogParser.IsCellEmpty(cell)) continue;
                            if (!MatrixCsvLogParser.TryParsePayloadHex(cell, msgBytes)) continue;

                            string id = column.Id;
                            if (deviceByID.TryGetValue(id, out Device? dev))
                            {
                                Array.Copy(msgBytes, dev.RawBytes, 8);
                                dev.Decode();
                            }

                            composites?.OnMessage(id, msgBytes, 8);
                        }

                        RefreshComposites();

                        if (outputFormat == OutputFormat.Csv)
                            WriteCsvDataRow(csvWriter!, step, timeText, outputDevices, deviceEnabled, paramEnabled);
                        else
                            excelRow = logReader.Program.BuildExcelRow(
                                ws!, excelRow, step, timeText,
                                outputDevices, deviceEnabled, paramEnabled);
                    }

                    if (rowsInFile > 0)
                    {
                        timeOffset = maxTimeInFile.Add(
                            TimeSpan.FromMilliseconds(MatrixCsvLogParser.RowPeriodMs));
                    }
                }
            }
            catch (Exception ex)
            {
                log($"Ошибка чтения файла: {ex.Message}");
                return false;
            }

            if (step == 0)
            {
                log("Ошибка: в файлах нет строк данных.");
                return false;
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

                log(csvPaths.Count == 1
                    ? "Обработка завершена."
                    : $"Обработка завершена: объединено файлов {LogFormatUiNames.Csv}: {processedFiles}, строк: {step}.");
                return true;
            }
            catch (Exception ex)
            {
                log($"Ошибка сохранения файла: {ex.Message}");
                return false;
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

        private static bool TryReadHeaderLine(
            string csvPath,
            Encoding encoding,
            out List<MatrixCsvColumn> columns,
            out List<string> ids,
            out string? error)
        {
            columns = new List<MatrixCsvColumn>();
            ids = new List<string>();
            error = null;

            foreach (string line in File.ReadLines(csvPath, encoding))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                if (MatrixCsvLogParser.TryReadHeader(line, out columns, out ids))
                    return true;

                error = $"Ошибка: первая строка не похожа на заголовок {LogFormatUiNames.Csv} (;ID1;ID2;...).";
                return false;
            }

            error = "Ошибка: файл пуст.";
            return false;
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
