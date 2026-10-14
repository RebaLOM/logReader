namespace logReader.Processing
{
    // Пакетная обработка .trc: объединение в один результат или разбивка по датам.
    internal static class TrcBatchAggregator
    {
        private sealed record TrcInput(string Path, System.Text.Encoding Encoding, bool IsCanfox, DateTime? StartTime);

        // Объединение на общей оси времени: pCAN — абсолютное время (Start time + смещение),
        // CANfox — дата и время из строк. Если Start time есть не у всех .trc, файлы
        // выстраиваются друг за другом в миллисекундах, без «прыжков» к нулю.
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

            var inputs = ReadInputs(trcPaths, context);
            if (inputs.Count == 0)
                return context.Fail("Ошибка: не удалось собрать данные из .trc для объединения.");

            bool isCanfox = inputs[0].IsCanfox;
            foreach (var skipped in inputs.Where(i => i.IsCanfox != isCanfox))
                context.Log($"Пропуск: несовместимый тип .trc — {Path.GetFileName(skipped.Path)}");
            inputs = inputs.Where(i => i.IsCanfox == isCanfox).ToList();

            bool absolute = isCanfox || inputs.All(i => i.StartTime.HasValue);
            if (!isCanfox && !absolute)
                context.Log("Предупреждение: не во всех .trc есть Start time — файлы объединены последовательно, время в мс от начала первого файла.");
            if (!isCanfox && absolute)
                inputs = inputs.OrderBy(i => i.StartTime).ToList();

            aggregate = new TimeSeriesCollector(CompositeOutput.WithComposites(devices, composites), filter,
                absolute ? TimeAxisKind.DateTime : TimeAxisKind.Milliseconds,
                settings.IncludeDtc ? settings.DtcTimeoutMs : null);

            var deviceById = new Dictionary<string, Device>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in devices)
                deviceById[d.ID] = d;

            Span<int> bytes = stackalloc int[Device.MaxDataLength];
            double sequentialOffsetMs = 0;
            int usedFiles = 0;

            for (int f = 0; f < inputs.Count; f++)
            {
                var input = inputs[f];
                var fileContext = context.Slice((double)f / inputs.Count, 1.0 / inputs.Count, Path.GetFileName(input.Path));
                double lastTimeMs = 0;

                try
                {
                    // Устройства кешируются в UI — сброс перед каждым файлом пакета.
                    DeviceFiles.ResetState(devices);
                    composites?.Reset();
                    aggregate.BeginDtcFile();

                    foreach (var line in LogFileReader.ReadLines(input.Path, input.Encoding, fileContext))
                    {
                        string id;
                        int parsedByteCount;
                        double timeVal;
                        bool diagnosticOnly = false;

                        if (isCanfox)
                        {
                            if (!CanfoxLogParser.TryParseCanfoxFrameLine(line, out DateOnly date, out double dayFraction, out id, bytes, out parsedByteCount))
                            {
                                if (!settings.IncludeDtc || !DtcFrameBoundary.TryCanfox(line, out date, out dayFraction, out id)) continue;
                                diagnosticOnly = true;
                                parsedByteCount = 0;
                            }
                            timeVal = date.ToDateTime(TimeOnly.MinValue).ToOADate() + dayFraction;
                        }
                        else
                        {
                            if (!TrcLogParser.TryParseTrcFrameLine(line, out decimal timeMsRaw, out _, out id, out _, bytes, out parsedByteCount))
                            {
                                if (!settings.IncludeDtc || !DtcFrameBoundary.TryTrc(line, out _, out timeMsRaw, out id)) continue;
                                diagnosticOnly = true;
                                parsedByteCount = 0;
                            }
                            double timeMs = (double)timeMsRaw;
                            if (!diagnosticOnly) lastTimeMs = Math.Max(lastTimeMs, timeMs);
                            timeVal = absolute
                                ? TimeAxisFormat.ToOADate(input.StartTime!.Value.AddMilliseconds(timeMs))
                                : sequentialOffsetMs + timeMs;
                        }

                        if (diagnosticOnly) aggregate.ObserveDtc(id, ReadOnlySpan<int>.Empty, timeVal);
                        else Record(id, timeVal, bytes[..parsedByteCount], deviceById, composites, filter, aggregate);
                    }

                    sequentialOffsetMs += lastTimeMs;
                    usedFiles++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    context.Log($"Пропуск: ошибка чтения ({Path.GetFileName(input.Path)}): {ex.Message}");
                }
            }

            if (usedFiles == 0 || !aggregate.HasData)
                return context.Fail("Ошибка: не удалось собрать данные из .trc для объединения.");

            return ProcessingResult.Ok("", aggregate.MaxRows);
        }

        private static List<TrcInput> ReadInputs(IReadOnlyList<string> trcPaths, ProcessingContext context)
        {
            var inputs = new List<TrcInput>();
            foreach (string path in trcPaths)
            {
                if (!File.Exists(path))
                {
                    context.Log($"Пропуск: файл не найден: {path}");
                    continue;
                }

                try
                {
                    var encoding = LogFileEncoding.Detect(path);
                    bool isCanfox = CanfoxLogParser.LooksLikeCanfoxLog(path, encoding);
                    DateTime? start = isCanfox ? null : TrcLogParser.ParseStartTime(path, encoding);
                    inputs.Add(new TrcInput(path, encoding, isCanfox, start));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    context.Log($"Пропуск: ошибка чтения ({Path.GetFileName(path)}): {ex.Message}");
                }
            }
            return inputs;
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

            Span<int> bytes = stackalloc int[Device.MaxDataLength];

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
                    var dtcTimeline = settings.IncludeDtc ? new DtcOutputTimeline(settings.DtcTimeoutMs) : null;
                    var dtcDates = new HashSet<DateOnly>();

                    foreach (var line in LogFileReader.ReadLines(trcPath, encoding, fileContext))
                    {
                        bool diagnosticOnly = false;
                        if (!TrcLogParser.TryParseTrcFrameLine(line, out decimal timeMsRaw, out _, out string id, out _, bytes, out int parsedByteCount))
                        {
                            if (!settings.IncludeDtc || !DtcFrameBoundary.TryTrc(line, out _, out timeMsRaw, out id)) continue;
                            diagnosticOnly = true;
                            parsedByteCount = 0;
                        }

                        DateTime frameTime = startTime.Value.AddMilliseconds((double)timeMsRaw);
                        DateOnly date = DateOnly.FromDateTime(frameTime);

                        if (!aggregatesByDate.TryGetValue(date, out var agg))
                        {
                            agg = new TimeSeriesCollector(outputDevices, filter, TimeAxisKind.DateTime,
                                settings.IncludeDtc ? settings.DtcTimeoutMs : null);
                            aggregatesByDate[date] = agg;
                        }

                        double outputTime = TimeAxisFormat.ToOADate(frameTime);
                        if (dtcTimeline != null)
                        {
                            int priorRows = dtcTimeline.Rows.Count;
                            dtcTimeline.Observe(id, bytes[..parsedByteCount], outputTime, TimeAxisKind.DateTime);
                            if (priorRows == dtcTimeline.Rows.Count && dtcDates.Add(date))
                                agg.DtcRows!.Add((outputTime, dtcTimeline.CurrentAt(outputTime, TimeAxisKind.DateTime)));
                            else
                            {
                                dtcDates.Add(date);
                                for (int i = priorRows; i < dtcTimeline.Rows.Count; i++) agg.DtcRows!.Add(dtcTimeline.Rows[i]);
                            }
                            dtcTimeline.Rows.Clear();
                        }

                        if (!diagnosticOnly) Record(id, outputTime, bytes[..parsedByteCount], deviceById, composites, filter, agg, observeDtc: false);
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
            TimeSeriesCollector collector,
            bool observeDtc = true)
        {
            if (observeDtc) collector.ObserveDtc(id, bytes, time);
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
