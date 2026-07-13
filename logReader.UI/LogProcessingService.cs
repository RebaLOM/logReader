using System.Globalization;
using System.Linq;
using logReader;

namespace logReader.UI
{
    // Оркестрация обработки логов без WinForms — вынесено из MainForm для тестируемости.
    internal sealed class LogProcessingService
    {
        private readonly Action<string> _log;

        public LogProcessingService(Action<string> log) => _log = log;

        public readonly record struct BatchOutcome(int Created, int Expected);

        public void ProcessSingleFile(
            string logPath,
            string outputPath,
            OutputFormat outputFormat,
            List<Device> allDevices,
            bool hasFilter,
            Dictionary<string, bool> deviceEnabled,
            Dictionary<string, bool[]> paramEnabled,
            CompositeRuntime? composites,
            DstConnectOptions? dstOptions = null)
        {
            bool isTrc = Path.GetExtension(logPath).Equals(".trc", StringComparison.OrdinalIgnoreCase);

            if (outputFormat == OutputFormat.CsvDstConnect)
            {
                if (!isTrc)
                {
                    _log("Ошибка: CSV ДСТ Коннект применим только к файлам .trc.");
                    return;
                }

                _log("Формат: CSV ДСТ Коннект (pCAN .trc)");
                new DstConnectTrcProcessor().Process(
                    logPath, allDevices, outputPath, dstOptions ?? new DstConnectOptions(), _log,
                    hasFilter ? deviceEnabled : null,
                    hasFilter ? paramEnabled : null,
                    composites);
                return;
            }

            bool isPCan = IsPCanLog(logPath);
            bool isAsc = IsAscLog(logPath);
            if (isPCan)
            {
                if (Path.GetExtension(logPath).Equals(".trc", StringComparison.OrdinalIgnoreCase))
                    _log("Формат: pCAN Viewer");
                else
                    _log("Формат: CANfox (PCAN-View / CAN.txt)");
            }
            else if (isAsc) _log("Формат: ASC");
            else if (Path.GetExtension(logPath).Equals(".csv", StringComparison.OrdinalIgnoreCase))
            {
                var kind = LogFormatDetector.Detect(logPath);
                string? msg = LogFormatUiNames.GetDetectedFormatMessage(kind);
                if (!string.IsNullOrEmpty(msg))
                    _log(msg);
            }

            if (isPCan)
            {
                new PCanLogProcessor().Process(
                    logPath, allDevices, outputPath, outputFormat, _log,
                    hasFilter ? deviceEnabled : null,
                    hasFilter ? paramEnabled : null,
                    composites);
            }
            else if (isAsc)
            {
                new AscLogProcessor().Process(
                    logPath, allDevices, outputPath, outputFormat, _log,
                    hasFilter ? deviceEnabled : null,
                    hasFilter ? paramEnabled : null,
                    composites);
            }
            else
            {
                if (Path.GetExtension(logPath).Equals(".txt", StringComparison.OrdinalIgnoreCase))
                {
                    _log("Пропуск: текстовый файл не распознан как лог CANfox (нужен формат с колонками Date, Time, ID, Data).");
                    return;
                }

                if (Path.GetExtension(logPath).Equals(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    var enc = LogFileEncoding.Detect(logPath);
                    if (MatrixCsvLogParser.LooksLikeMatrixCsv(logPath, enc))
                    {
                        new MatrixCsvLogProcessor().Process(
                            logPath, allDevices, outputPath, outputFormat, _log,
                            hasFilter ? deviceEnabled : null,
                            hasFilter ? paramEnabled : null,
                            composites);
                        return;
                    }
                }

                new CanLogProcessor().Process(
                    logPath, allDevices, outputPath, outputFormat, _log,
                    hasFilter ? deviceEnabled : null,
                    hasFilter ? paramEnabled : null,
                    composites);
            }
        }

        public BatchOutcome ProcessFolderBatch(
            IReadOnlyList<string> files,
            string outputDir,
            string devicesFullPath,
            OutputFormat outputFormat,
            BatchOutputMode batchMode,
            List<Device> allDevices,
            bool hasFilter,
            Dictionary<string, bool> deviceEnabled,
            Dictionary<string, bool[]> paramEnabled,
            CompositeRuntime? composites,
            DstConnectOptions? dstOptions = null)
        {
            int created = 0;
            int expected = 0;

            var matrixCsvFiles = files
                .Where(IsMatrixCsvLog)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var trcFiles = files
                .Where(p => Path.GetExtension(p).Equals(".trc", StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var otherFiles = files
                .Where(p => !Path.GetExtension(p).Equals(".trc", StringComparison.OrdinalIgnoreCase)
                            && !IsMatrixCsvLog(p))
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();

            void ProcessPerFile(IEnumerable<string> inputFiles)
            {
                foreach (string logPath in inputFiles)
                {
                    string outPath = BuildBatchOutputPath(logPath, outputDir, outputFormat);
                    if (string.Equals(Path.GetFullPath(outPath), devicesFullPath, StringComparison.OrdinalIgnoreCase))
                    {
                        _log($"Пропуск: совпадает с файлом посылок — {Path.GetFileName(outPath)}");
                        continue;
                    }

                    if (IsFileLocked(outPath))
                    {
                        _log($"Пропуск (файл занят): {Path.GetFileName(outPath)}");
                        continue;
                    }

                    _log($"--- {Path.GetFileName(logPath)} ---");
                    expected++;
                    ProcessSingleFile(
                        logPath, outPath, outputFormat, allDevices, hasFilter,
                        deviceEnabled, paramEnabled, composites, dstOptions);

                    if (File.Exists(outPath))
                        created++;
                }
            }

            // Не-.trc и не-CSV (новый формат) всегда обрабатываем по одному файлу.
            ProcessPerFile(otherFiles);

            if (batchMode == BatchOutputMode.MergeToSingleFile && matrixCsvFiles.Count > 0)
            {
                string mergedOut = Path.Combine(outputDir, "result_matrix_csv_merged" + GetOutputExtension(outputFormat));
                if (string.Equals(Path.GetFullPath(mergedOut), devicesFullPath, StringComparison.OrdinalIgnoreCase))
                {
                    _log("Пропуск: выходной файл совпадает с файлом посылок.");
                    return new BatchOutcome(created, expected);
                }

                if (IsFileLocked(mergedOut))
                {
                    _log($"Пропуск (файл занят): {Path.GetFileName(mergedOut)}");
                    return new BatchOutcome(created, expected);
                }

                _log($"--- {LogFormatUiNames.Csv}: объединение в один файл ---");
                expected++;
                bool ok = new MatrixCsvLogProcessor().ProcessMerged(
                    matrixCsvFiles, allDevices, mergedOut, outputFormat, _log,
                    hasFilter ? deviceEnabled : null,
                    hasFilter ? paramEnabled : null,
                    composites);

                if (ok && File.Exists(mergedOut))
                    created++;
            }
            else
            {
                ProcessPerFile(matrixCsvFiles);
            }

            if (batchMode == BatchOutputMode.PerInputFile || trcFiles.Count == 0)
            {
                ProcessPerFile(trcFiles);
                return new BatchOutcome(created, expected);
            }

            if (outputFormat == OutputFormat.CsvDstConnect)
            {
                _log("CSV ДСТ: объединение и разбивка по датам не поддерживаются — обработка по одному файлу.");
                ProcessPerFile(trcFiles);
                return new BatchOutcome(created, expected);
            }

            if (batchMode == BatchOutputMode.MergeToSingleFile)
            {
                string mergedOut = Path.Combine(outputDir, "result_trc_merged" + GetOutputExtension(outputFormat));
                if (string.Equals(Path.GetFullPath(mergedOut), devicesFullPath, StringComparison.OrdinalIgnoreCase))
                {
                    _log("Пропуск: выходной файл совпадает с файлом посылок.");
                    return new BatchOutcome(created, expected);
                }

                if (IsFileLocked(mergedOut))
                {
                    _log($"Пропуск (файл занят): {Path.GetFileName(mergedOut)}");
                    return new BatchOutcome(created, expected);
                }

                _log("--- .trc: объединение в один файл ---");
                expected++;
                if (!TrcBatchAggregator.TryBuildMergedAggregate(
                        trcFiles, allDevices,
                        hasFilter ? deviceEnabled : null,
                        hasFilter ? paramEnabled : null,
                        _log, out var agg, composites))
                {
                    _log("Ошибка: не удалось собрать данные из .trc для объединения.");
                    return new BatchOutcome(created, expected);
                }

                PCanLogProcessor.WriteOutput(
                    CompositeOutput.WithComposites(allDevices, composites),
                    agg.DeviceData,
                    hasFilter ? deviceEnabled : null,
                    hasFilter ? paramEnabled : null,
                    mergedOut, outputFormat, agg.IsCanfox, _log);

                if (File.Exists(mergedOut))
                    created++;

                return new BatchOutcome(created, expected);
            }

            if (batchMode == BatchOutputMode.SplitTrcByDate)
            {
                _log("--- .trc: разбивка по датам ---");
                if (!TrcBatchAggregator.TryBuildAggregatesByDate(
                        trcFiles, allDevices,
                        hasFilter ? deviceEnabled : null,
                        hasFilter ? paramEnabled : null,
                        _log, out var byDate, composites))
                {
                    _log("Ошибка: не удалось собрать данные из .trc для разбивки по датам.");
                    return new BatchOutcome(created, expected);
                }

                foreach (var kv in byDate.OrderBy(k => k.Key))
                {
                    string datePart = kv.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    string outPath = Path.Combine(outputDir, "result_" + datePart + GetOutputExtension(outputFormat));
                    if (string.Equals(Path.GetFullPath(outPath), devicesFullPath, StringComparison.OrdinalIgnoreCase))
                    {
                        _log($"Пропуск: совпадает с файлом посылок — {Path.GetFileName(outPath)}");
                        continue;
                    }

                    if (IsFileLocked(outPath))
                    {
                        _log($"Пропуск (файл занят): {Path.GetFileName(outPath)}");
                        continue;
                    }

                    _log($"--- {Path.GetFileName(outPath)} ---");
                    expected++;
                    PCanLogProcessor.WriteOutput(
                        CompositeOutput.WithComposites(allDevices, composites),
                        kv.Value.DeviceData,
                        hasFilter ? deviceEnabled : null,
                        hasFilter ? paramEnabled : null,
                        outPath, outputFormat, isCanfox: false, _log);

                    if (File.Exists(outPath))
                        created++;
                }
            }

            return new BatchOutcome(created, expected);
        }

        private static bool IsMatrixCsvLog(string path)
        {
            if (!Path.GetExtension(path).Equals(".csv", StringComparison.OrdinalIgnoreCase))
                return false;

            try
            {
                return MatrixCsvLogParser.LooksLikeMatrixCsv(path, LogFileEncoding.Detect(path));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static bool IsFileLocked(string path)
        {
            if (!File.Exists(path)) return false;
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                return false;
            }
            catch (IOException)
            {
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return true;
            }
        }

        internal static string GetOutputExtension(OutputFormat outputFormat)
            => outputFormat switch
            {
                OutputFormat.Xlsx => ".xlsx",
                OutputFormat.CsvDstConnect => ".csv",
                _ => ".csv"
            };

        private static string BuildBatchOutputPath(string logFilePath, string outputFolder, OutputFormat outputFormat)
        {
            string stem = Path.GetFileNameWithoutExtension(logFilePath);
            string ext = Path.GetExtension(logFilePath).TrimStart('.');
            if (string.IsNullOrEmpty(ext)) ext = "log";

            if (outputFormat == OutputFormat.CsvDstConnect
                && ext.Equals("trc", StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(outputFolder, $"{stem}_trc_dst.csv");
            }

            return Path.Combine(outputFolder, $"{stem}_{ext}_result{GetOutputExtension(outputFormat)}");
        }

        private static bool IsPCanLog(string path)
        {
            if (Path.GetExtension(path).Equals(".trc", StringComparison.OrdinalIgnoreCase))
                return true;
            if (!File.Exists(path)) return false;
            try
            {
                var enc = LogFileEncoding.Detect(path);
                return CanfoxLogParser.LooksLikeCanfoxLog(path, enc);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static bool IsAscLog(string path) =>
            Path.GetExtension(path).Equals(".asc", StringComparison.OrdinalIgnoreCase);
    }
}
