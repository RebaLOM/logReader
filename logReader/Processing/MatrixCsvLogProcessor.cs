using System.Text;

namespace logReader.Processing
{
    // Табличный CSV (время × посылки): одна строка результата на строку лога.
    internal sealed class MatrixCsvLogProcessor
    {
        public ProcessingResult Process(
            string csvPath,
            List<Device> devices,
            string outputPath,
            OutputSettings settings,
            ProcessingContext context)
            => ProcessMerged(new[] { csvPath }, devices, outputPath, settings, context);

        public ProcessingResult ProcessMerged(
            IReadOnlyList<string> csvPaths,
            List<Device> devices,
            string outputPath,
            OutputSettings settings,
            ProcessingContext context)
        {
            var composites = settings.Composites;
            if (devices.Count == 0 && !settings.HasComposites && !settings.CollectDtc)
                return context.Fail("Ошибка: устройства не загружены.");
            if (csvPaths.Count == 0)
                return context.Fail("Ошибка: не указаны файлы CSV.");

            var deviceByID = new Dictionary<string, Device>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in devices)
                deviceByID[d.ID] = d;

            var inputs = new List<(string Path, Encoding Encoding, IReadOnlyList<MatrixCsvColumn> Columns)>();
            var preamble = new List<string>();
            var seenIDs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var scanContext = new ProcessingContext(cancellationToken: context.CancellationToken);

            foreach (string csvPath in csvPaths)
            {
                if (!File.Exists(csvPath))
                {
                    context.Log($"Пропуск: файл не найден: {csvPath}");
                    continue;
                }

                try
                {
                    Encoding encoding = LogFileEncoding.Detect(csvPath);
                    using var lines = LogFileReader.ReadLines(csvPath, encoding, scanContext).GetEnumerator();
                    var inputPreamble = new List<string>();
                    var header = MatrixCsvLogParser.FindHeader(lines, inputPreamble);
                    if (header == null)
                    {
                        context.Log($"В файле «{Path.GetFileName(csvPath)}» не найдена строка с CAN ID (;ID1;ID2;...).");
                        continue;
                    }

                    foreach (var column in header.Columns)
                        seenIDs.Add(column.Id);
                    inputs.Add((csvPath, encoding, header.Columns));
                    preamble.AddRange(inputPreamble);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    context.Log($"Пропуск: ошибка чтения ({Path.GetFileName(csvPath)}): {ex.Message}");
                }
            }

            if (inputs.Count == 0)
                return context.Fail($"Ошибка: не найдено пригодных файлов {LogFormatUiNames.Csv}.");

            var activeDevices = devices.Where(d => seenIDs.Contains(d.ID)).ToList();
            var activeBlocks = CanLogProcessor.ActiveCompositeBlocks(composites, settings.Filter, seenIDs);
            var outputColumns = settings.Filter.BuildColumns(activeDevices.Concat(activeBlocks));
            if (outputColumns.Count == 0 && !settings.CollectDtc)
                return context.Fail("Нет совпадающих устройств — проверьте файл посылок.");

            using var writer = new StepOutputWriter(outputPath, settings.Format, outputColumns, settings.IncludeDeviceIdHeaderRow,
                settings.IncludeDtc, settings.AutoIncludeDtc, settings.DtcDisplayFormat, context, preamble);
            int step = 0;
            Span<int> msgBytes = stackalloc int[8];

            try
            {
                for (int f = 0; f < inputs.Count; f++)
                {
                    var (csvPath, encoding, columns) = inputs[f];
                    if (inputs.Count > 1)
                        context.Log($"--- {Path.GetFileName(csvPath)} ---");

                    DeviceFiles.ResetState(devices);
                    composites?.Reset();

                    var timeTracker = new MatrixCsvTimeTracker();
                    var dtc = settings.CollectDtc ? new DtcRowCollector(settings.DtcTimeoutMs) : null;
                    var fileContext = context.Slice((double)f / inputs.Count, 1.0 / inputs.Count, Path.GetFileName(csvPath));
                    using var lines = LogFileReader.ReadLines(csvPath, encoding, fileContext).GetEnumerator();
                    var header = MatrixCsvLogParser.FindHeader(lines);
                    if (header == null || !columns.SequenceEqual(header.Columns))
                        return context.Fail($"Строка с CAN ID в файле «{Path.GetFileName(csvPath)}» изменилась во время обработки.");

                    foreach (string line in MatrixCsvLogParser.ReadDataLines(lines, header))
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        string[] parts = line.Split(';');
                        if (parts.Length < 2) continue;
                        if (!timeTracker.TryAdvance(parts[0], out TimeSpan absoluteTime))
                            continue;

                        step++;
                        string timeText = MatrixCsvLogParser.FormatTimeWithMs(absoluteTime);

                        foreach (var column in columns)
                        {
                            if (column.ColumnIndex >= parts.Length) continue;
                            string cell = parts[column.ColumnIndex];
                            if (MatrixCsvLogParser.IsCellEmpty(cell)) continue;
                            if (!MatrixCsvLogParser.TryParsePayloadHex(cell, msgBytes))
                            {
                                dtc?.Add(column.Id, ReadOnlySpan<int>.Empty);
                                continue;
                            }

                            dtc?.Add(column.Id, msgBytes);

                            if (deviceByID.TryGetValue(column.Id, out Device? dev))
                            {
                                dev.SetPayload(msgBytes);
                                dev.Decode();
                            }

                            composites?.OnMessage(column.Id, msgBytes, 8);
                        }

                        foreach (var block in activeBlocks)
                            block.Decode();

                        writer.WriteRow(step, timeText, dtc?.Finish(absoluteTime.TotalMilliseconds));
                    }
                }

                if (step == 0)
                    return context.Fail("Ошибка: в файлах нет строк данных.");

                if (outputColumns.Count == 0 && !writer.HasDtcColumns)
                    return context.Fail("Нет совпадающих устройств — проверьте файл посылок.");

                writer.Complete();
                if (writer.SheetCount > 1)
                    context.Log($"XLSX: данные разбиты на {writer.SheetCount} лист(а/ов) (лимит {ExcelLayoutBuilder.RowsPerSheet:N0} строк на лист).");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return context.Fail($"Ошибка чтения файла: {ex.Message}");
            }

            context.Log(csvPaths.Count == 1
                ? "Обработка завершена."
                : $"Обработка завершена: объединено файлов {LogFormatUiNames.Csv}: {inputs.Count}, строк: {step}.");
            return ProcessingResult.Ok(outputPath, writer.RowsWritten);
        }
    }
}
