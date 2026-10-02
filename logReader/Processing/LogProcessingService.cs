using System.Globalization;

namespace logReader.Processing
{
    // Оркестрация обработки логов без WinForms.
    internal sealed class LogProcessingService
    {
        public readonly record struct BatchOutcome(int Created, int Expected, int Failed);

        public ProcessingResult ProcessSingleFile(
            string logPath,
            string outputPath,
            List<Device> allDevices,
            OutputSettings settings,
            ProcessingContext context)
        {
            string ext = Path.GetExtension(logPath);
            bool isTrc = ext.Equals(".trc", StringComparison.OrdinalIgnoreCase);

            if (settings.Format == OutputFormat.CsvDstConnect)
            {
                if (!isTrc)
                    return context.Fail("Ошибка: CSV ДСТ Коннект применим только к файлам .trc.");

                context.Log("Формат: CSV ДСТ Коннект (pCAN .trc)");
                return new DstConnectTrcProcessor().Process(logPath, allDevices, outputPath, settings, context);
            }

            var kind = LogFormatDetector.Detect(logPath);
            switch (kind)
            {
                case LogFormatKind.Trc:
                    context.Log("Формат: pCAN Viewer");
                    return new PCanLogProcessor().Process(logPath, allDevices, outputPath, settings, context);
                case LogFormatKind.CanfoxTxt:
                    context.Log("Формат: CANfox (PCAN-View / CAN.txt)");
                    return new PCanLogProcessor().Process(logPath, allDevices, outputPath, settings, context);
                case LogFormatKind.Asc:
                    context.Log("Формат: ASC");
                    return new AscLogProcessor().Process(logPath, allDevices, outputPath, settings, context);
                case LogFormatKind.MatrixCsv:
                    context.Log(LogFormatUiNames.GetDetectedFormatMessage(kind));
                    return new MatrixCsvLogProcessor().Process(logPath, allDevices, outputPath, settings, context);
                case LogFormatKind.StepCsv:
                    context.Log(LogFormatUiNames.GetDetectedFormatMessage(kind));
                    return new CanLogProcessor().Process(logPath, allDevices, outputPath, settings, context);
                default:
                    return context.Fail(ext.Equals(".txt", StringComparison.OrdinalIgnoreCase)
                        ? "Пропуск: текстовый файл не распознан как лог CANfox (нужен формат с колонками Date, Time, ID, Data)."
                        : $"Пропуск: неподдерживаемый формат файла {Path.GetFileName(logPath)}.");
            }
        }

        public BatchOutcome ProcessFolderBatch(
            IReadOnlyList<string> files,
            string outputDir,
            string devicesFullPath,
            BatchOutputMode batchMode,
            List<Device> allDevices,
            OutputSettings settings,
            ProcessingContext context)
        {
            int created = 0;
            int expected = 0;
            int failed = 0;

            var kinds = files.ToDictionary(f => f, SafeDetect, StringComparer.OrdinalIgnoreCase);
            var matrixCsvFiles = files.Where(p => kinds[p] == LogFormatKind.MatrixCsv).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
            var trcFiles = files.Where(p => kinds[p] == LogFormatKind.Trc).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
            var otherFiles = files.Where(p => kinds[p] is not (LogFormatKind.Trc or LogFormatKind.MatrixCsv)).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();

            bool mergeMatrix = batchMode == BatchOutputMode.MergeToSingleFile && matrixCsvFiles.Count > 0;
            bool aggregateTrc = batchMode != BatchOutputMode.PerInputFile && trcFiles.Count > 0
                                && settings.Format != OutputFormat.CsvDstConnect;
            int totalSteps = otherFiles.Count
                             + (mergeMatrix ? 1 : matrixCsvFiles.Count)
                             + (aggregateTrc ? 1 : trcFiles.Count);
            int stepIndex = 0;

            ProcessingContext NextStep(string stage)
                => context.Slice((double)stepIndex++ / Math.Max(1, totalSteps), 1.0 / Math.Max(1, totalSteps), stage);

            // Ошибка одного файла не должна обрывать весь пакет — отмена обрывает.
            void Track(Func<ProcessingResult> run, string label)
            {
                expected++;
                try
                {
                    var result = run();
                    if (result.Success) created++;
                    else failed++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failed++;
                    context.Log($"Ошибка ({label}): {ex.Message}");
                }
            }

            bool CanWrite(string outPath)
            {
                if (string.Equals(Path.GetFullPath(outPath), devicesFullPath, StringComparison.OrdinalIgnoreCase))
                {
                    context.Log($"Пропуск: совпадает с файлом посылок — {Path.GetFileName(outPath)}");
                    return false;
                }
                if (IsFileLocked(outPath))
                {
                    context.Log($"Пропуск (файл занят): {Path.GetFileName(outPath)}");
                    return false;
                }
                return true;
            }

            void ProcessPerFile(IEnumerable<string> inputFiles)
            {
                foreach (string logPath in inputFiles)
                {
                    var stepContext = NextStep(Path.GetFileName(logPath));
                    string outPath = BuildBatchOutputPath(logPath, outputDir, settings.Format);
                    if (!CanWrite(outPath)) continue;

                    context.Log($"--- {Path.GetFileName(logPath)} ---");
                    Track(() => ProcessSingleFile(logPath, outPath, allDevices, settings, stepContext), Path.GetFileName(logPath));
                }
            }

            ProcessPerFile(otherFiles);

            if (mergeMatrix)
            {
                var stepContext = NextStep(LogFormatUiNames.Csv);
                string mergedOut = Path.Combine(outputDir, "result_matrix_csv_merged" + GetOutputExtension(settings.Format));
                if (CanWrite(mergedOut))
                {
                    context.Log($"--- {LogFormatUiNames.Csv}: объединение в один файл ---");
                    Track(() => new MatrixCsvLogProcessor().ProcessMerged(matrixCsvFiles, allDevices, mergedOut, settings, stepContext),
                        Path.GetFileName(mergedOut));
                }
            }
            else
            {
                ProcessPerFile(matrixCsvFiles);
            }

            if (!aggregateTrc)
            {
                if (batchMode != BatchOutputMode.PerInputFile && settings.Format == OutputFormat.CsvDstConnect && trcFiles.Count > 0)
                    context.Log("CSV ДСТ: объединение и разбивка по датам не поддерживаются — обработка по одному файлу.");
                ProcessPerFile(trcFiles);
                return new BatchOutcome(created, expected, failed);
            }

            var trcContext = NextStep(".trc");
            if (batchMode == BatchOutputMode.MergeToSingleFile)
            {
                string mergedOut = Path.Combine(outputDir, "result_trc_merged" + GetOutputExtension(settings.Format));
                if (!CanWrite(mergedOut))
                    return new BatchOutcome(created, expected, failed);

                context.Log("--- .trc: объединение в один файл ---");
                Track(() =>
                {
                    var build = TrcBatchAggregator.TryBuildMergedAggregate(trcFiles, allDevices, settings, trcContext.Slice(0, 0.8), out var agg);
                    return build.Success
                        ? TimeSeriesOutputWriter.Write(agg!, mergedOut, settings, "pCAN Log", trcContext.Slice(0.8, 0.2))
                        : build;
                }, Path.GetFileName(mergedOut));
                return new BatchOutcome(created, expected, failed);
            }

            context.Log("--- .trc: разбивка по датам ---");
            var byDateResult = TrcBatchAggregator.TryBuildAggregatesByDate(trcFiles, allDevices, settings, trcContext.Slice(0, 0.8), out var byDate);
            if (!byDateResult.Success)
            {
                expected++;
                failed++;
                return new BatchOutcome(created, expected, failed);
            }

            foreach (var kv in byDate.OrderBy(k => k.Key))
            {
                string datePart = kv.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                string outPath = Path.Combine(outputDir, "result_" + datePart + GetOutputExtension(settings.Format));
                if (!CanWrite(outPath)) continue;

                context.Log($"--- {Path.GetFileName(outPath)} ---");
                Track(() => TimeSeriesOutputWriter.Write(kv.Value, outPath, settings, "pCAN Log", trcContext.Slice(0.8, 0.2)), Path.GetFileName(outPath));
            }

            return new BatchOutcome(created, expected, failed);
        }

        private static LogFormatKind SafeDetect(string path)
        {
            try { return LogFormatDetector.Detect(path); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return LogFormatKind.None; }
        }

        internal static bool IsFileLocked(string path)
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
            => outputFormat == OutputFormat.Xlsx ? ".xlsx" : ".csv";

        internal static string BuildBatchOutputPath(string logFilePath, string outputFolder, OutputFormat outputFormat)
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
    }
}
