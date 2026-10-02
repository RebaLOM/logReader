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
            if (devices.Count == 0 && !settings.HasComposites)
                return context.Fail("Ошибка: устройства не загружены.");
            if (csvPaths.Count == 0)
                return context.Fail("Ошибка: не указаны файлы CSV.");

            var deviceByID = new Dictionary<string, Device>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in devices)
                deviceByID[d.ID] = d;

            var inputs = new List<(string Path, System.Text.Encoding Encoding, List<MatrixCsvColumn> Columns)>();
            var seenIDs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string csvPath in csvPaths)
            {
                if (!File.Exists(csvPath))
                {
                    context.Log($"Пропуск: файл не найден: {csvPath}");
                    continue;
                }

                System.Text.Encoding encoding;
                try { encoding = LogFileEncoding.Detect(csvPath); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    context.Log($"Пропуск: ошибка чтения ({Path.GetFileName(csvPath)}): {ex.Message}");
                    continue;
                }

                if (!TryReadHeaderLine(csvPath, encoding, out var columns, out string? headerError))
                {
                    context.Log($"Пропуск ({Path.GetFileName(csvPath)}): {headerError}");
                    continue;
                }

                foreach (var column in columns)
                    seenIDs.Add(column.Id);
                inputs.Add((csvPath, encoding, columns));
            }

            if (inputs.Count == 0)
                return context.Fail($"Ошибка: не найдено пригодных файлов {LogFormatUiNames.Csv}.");

            var activeDevices = devices.Where(d => seenIDs.Contains(d.ID)).ToList();
            var activeBlocks = CanLogProcessor.ActiveCompositeBlocks(composites, settings.Filter, seenIDs);
            var outputColumns = settings.Filter.BuildColumns(activeDevices.Concat(activeBlocks));
            if (outputColumns.Count == 0)
                return context.Fail("Нет совпадающих устройств — проверьте файл посылок.");

            using var writer = new StepOutputWriter(outputPath, settings.Format, outputColumns, settings.IncludeDeviceIdHeaderRow);
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
                    bool headerSkipped = false;
                    var fileContext = context.Slice((double)f / inputs.Count, 1.0 / inputs.Count, Path.GetFileName(csvPath));

                    foreach (string line in LogFileReader.ReadLines(csvPath, encoding, fileContext))
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

                        step++;
                        string timeText = MatrixCsvLogParser.FormatTimeWithMs(absoluteTime);

                        foreach (var column in columns)
                        {
                            if (column.ColumnIndex >= parts.Length) continue;
                            string cell = parts[column.ColumnIndex];
                            if (MatrixCsvLogParser.IsCellEmpty(cell)) continue;
                            if (!MatrixCsvLogParser.TryParsePayloadHex(cell, msgBytes)) continue;

                            if (deviceByID.TryGetValue(column.Id, out Device? dev))
                            {
                                dev.SetPayload(msgBytes);
                                dev.Decode();
                            }

                            composites?.OnMessage(column.Id, msgBytes, 8);
                        }

                        foreach (var block in activeBlocks)
                            block.Decode();

                        writer.WriteRow(step, timeText);
                    }
                }

                if (step == 0)
                    return context.Fail("Ошибка: в файлах нет строк данных.");

                writer.Complete();
            }
            catch (ExcelRowLimitException ex)
            {
                return context.Fail("Ошибка: " + ex.Message);
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

        private static bool TryReadHeaderLine(
            string csvPath,
            System.Text.Encoding encoding,
            out List<MatrixCsvColumn> columns,
            out string? error)
        {
            columns = new List<MatrixCsvColumn>();
            error = null;

            foreach (string line in LogFileReader.ReadLines(csvPath, encoding))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                if (MatrixCsvLogParser.TryReadHeader(line, out columns, out _))
                    return true;

                error = $"первая строка не похожа на заголовок {LogFormatUiNames.Csv} (;ID1;ID2;...).";
                return false;
            }

            error = "файл пуст.";
            return false;
        }
    }
}
