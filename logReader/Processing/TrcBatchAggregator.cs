namespace logReader.Processing
{
    // Пакетная обработка .trc: объединение в один результат или разбивка по датам.
    internal static class TrcBatchAggregator
    {
        internal static ProcessingResult TryBuildMergedAggregate(
            IReadOnlyList<string> trcPaths,
            List<Device> devices,
            OutputSettings settings,
            ProcessingContext context,
            out TimeSeriesCollector? aggregate)
        {
            aggregate = null;
            var composites = settings.Composites;
            var filter = settings.Filter;
            var outputDevices = CompositeOutput.WithComposites(devices, composites);

            var deviceById = new Dictionary<string, Device>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in devices)
                deviceById[d.ID] = d;

            bool? isCanfoxExpected = null;
            Span<int> bytes = stackalloc int[8];

            int usedFiles = 0;
            for (int f = 0; f < trcPaths.Count; f++)
            {
                string trcPath = trcPaths[f];
                var fileContext = context.Slice((double)f / trcPaths.Count, 1.0 / trcPaths.Count, Path.GetFileName(trcPath));
                if (!File.Exists(trcPath))
                {
                    context.Log($"Пропуск: файл не найден: {trcPath}");
                    continue;
                }

                try
                {
                    var encoding = LogFileEncoding.Detect(trcPath);
                    bool isCanfox = CanfoxLogParser.LooksLikeCanfoxLog(trcPath, encoding);

                    if (isCanfoxExpected == null)
                    {
                        isCanfoxExpected = isCanfox;
                        aggregate = new TimeSeriesCollector(outputDevices, filter,
                            isCanfox ? TimeAxisKind.TimeOfDay : TimeAxisKind.Milliseconds);
                    }
                    else if (isCanfoxExpected.Value != isCanfox)
                    {
                        context.Log($"Пропуск: несовместимый тип .trc — {Path.GetFileName(trcPath)}");
                        continue;
                    }

                    if (!isCanfox && TrcLogParser.ParseStartTime(trcPath, encoding) == null)
                        context.Log($"Предупреждение: в .trc нет Start time — {Path.GetFileName(trcPath)} (время как мс от начала записи).");

                    // Устройства кешируются в UI — сброс перед каждым файлом пакета.
                    DeviceFiles.ResetState(devices);
                    composites?.Reset();

                    foreach (var line in LogFileReader.ReadLines(trcPath, encoding, fileContext))
                    {
                        string id;
                        int parsedByteCount;
                        double timeVal;

                        if (isCanfox)
                        {
                            if (!CanfoxLogParser.TryParseCanfoxFrameLine(line, out timeVal, out id, bytes, out parsedByteCount))
                                continue;
                        }
                        else
                        {
                            if (!TrcLogParser.TryParseTrcFrameLine(line, out decimal timeMsRaw, out _, out id, out _, bytes, out parsedByteCount))
                                continue;
                            timeVal = (double)timeMsRaw;
                        }

                        Record(id, timeVal, bytes[..parsedByteCount], deviceById, composites, filter, aggregate!);
                    }

                    usedFiles++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    context.Log($"Пропуск: ошибка чтения ({Path.GetFileName(trcPath)}): {ex.Message}");
                }
            }

            if (usedFiles == 0 || aggregate == null || !aggregate.HasData)
                return context.Fail("Ошибка: не удалось собрать данные из .trc для объединения.");

            return ProcessingResult.Ok("", aggregate.MaxRows);
        }

        internal static ProcessingResult TryBuildAggregatesByDate(
            IReadOnlyList<string> trcPaths,
            List<Device> devices,
            OutputSettings settings,
            ProcessingContext context,
            out Dictionary<DateOnly, TimeSeriesCollector> aggregatesByDate)
        {
            aggregatesByDate = new Dictionary<DateOnly, TimeSeriesCollector>();
            var composites = settings.Composites;
            var filter = settings.Filter;
            var outputDevices = CompositeOutput.WithComposites(devices, composites);

            var deviceById = new Dictionary<string, Device>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in devices)
                deviceById[d.ID] = d;

            Span<int> bytes = stackalloc int[8];

            int usedFiles = 0;
            for (int f = 0; f < trcPaths.Count; f++)
            {
                string trcPath = trcPaths[f];
                var fileContext = context.Slice((double)f / trcPaths.Count, 1.0 / trcPaths.Count, Path.GetFileName(trcPath));
                if (!File.Exists(trcPath))
                {
                    context.Log($"Пропуск: файл не найден: {trcPath}");
                    continue;
                }

                try
                {
                    var encoding = LogFileEncoding.Detect(trcPath);
                    if (CanfoxLogParser.LooksLikeCanfoxLog(trcPath, encoding))
                    {
                        context.Log($"Пропуск: разбивка по датам не поддерживается для CANfox .trc — {Path.GetFileName(trcPath)}");
                        continue;
                    }

                    DateTime? startTime = TrcLogParser.ParseStartTime(trcPath, encoding);
                    if (!startTime.HasValue)
                    {
                        context.Log($"Пропуск: в .trc не найден Start time (нужен для разбивки по датам) — {Path.GetFileName(trcPath)}");
                        continue;
                    }

                    // Устройства кешируются в UI — сброс перед каждым файлом пакета.
                    DeviceFiles.ResetState(devices);
                    composites?.Reset();

                    foreach (var line in LogFileReader.ReadLines(trcPath, encoding, fileContext))
                    {
                        if (!TrcLogParser.TryParseTrcFrameLine(line, out decimal timeMsRaw, out _, out string id, out _, bytes, out int parsedByteCount))
                            continue;

                        double timeMs = (double)timeMsRaw;
                        DateOnly date = DateOnly.FromDateTime(startTime.Value.AddMilliseconds(timeMs));

                        if (!aggregatesByDate.TryGetValue(date, out var agg))
                        {
                            agg = new TimeSeriesCollector(outputDevices, filter, TimeAxisKind.Milliseconds);
                            aggregatesByDate[date] = agg;
                        }

                        Record(id, timeMs, bytes[..parsedByteCount], deviceById, composites, filter, agg);
                    }

                    usedFiles++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    context.Log($"Пропуск: ошибка чтения ({Path.GetFileName(trcPath)}): {ex.Message}");
                }
            }

            if (usedFiles == 0 || !aggregatesByDate.Values.Any(a => a.HasData))
                return context.Fail("Ошибка: не удалось собрать данные из .trc для разбивки по датам.");

            return ProcessingResult.Ok("", aggregatesByDate.Count);
        }

        private static void Record(
            string id,
            double time,
            ReadOnlySpan<int> bytes,
            Dictionary<string, Device> deviceById,
            CompositeRuntime? composites,
            OutputFilter filter,
            TimeSeriesCollector collector)
        {
            composites?.OnMessage(id, bytes, bytes.Length);
            CompositeOutput.EmitTriggered(composites, id, time, collector, filter);

            if (!deviceById.TryGetValue(id, out Device? device)) return;
            if (!filter.IsDeviceEnabled(id)) return;

            device.SetPayload(bytes);
            device.Decode();
            collector.Record(device, time);
        }
    }
}
