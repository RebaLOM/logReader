namespace logReader.Processing
{
    // CSV «ДСТ Коннект»: одна строка на завершённый блок посылок шины (last-known значения).
    internal sealed class DstConnectTrcProcessor
    {
        private readonly record struct Frame(int MessageIndex, double TimeMs, string Id, int DataOffset, int DataLength);

        public ProcessingResult Process(
            string trcPath,
            List<Device> devices,
            string outputPath,
            OutputSettings settings,
            ProcessingContext context)
        {
            if (!File.Exists(trcPath))
                return context.Fail($"Ошибка: файл не найден: {trcPath}");

            var options = settings.DstConnect;
            var composites = settings.Composites;
            var filter = settings.Filter;

            DeviceFiles.ResetState(devices);
            composites?.Reset();

            var encoding = LogFileEncoding.Detect(trcPath);
            if (CanfoxLogParser.LooksLikeCanfoxLog(trcPath, encoding))
                return context.Fail("Ошибка: CSV ДСТ Коннект применим только к pCAN .trc, не к CANfox.");

            DateTime? startTime = TrcLogParser.ParseStartTime(trcPath, encoding);

            // Все кадры нужны заранее для детектора блоков; байты — в одном буфере, ID — без дублей строк.
            var frames = new List<Frame>();
            var data = new List<int>();
            var idPool = new Dictionary<string, string>(StringComparer.Ordinal);
            Span<int> byteSpan = stackalloc int[Device.MaxDataLength];
            try
            {
                foreach (var line in LogFileReader.ReadLines(trcPath, encoding, context.Slice(0, 0.5)))
                {
                    if (!TrcLogParser.TryParseTrcFrameLine(line, out int messageIndex, out decimal timeMsRaw,
                            out _, out string id, out _, byteSpan, out int parsedByteCount))
                        continue;

                    if (!idPool.TryGetValue(id, out string? pooled))
                    {
                        pooled = id;
                        idPool[id] = id;
                    }

                    frames.Add(new Frame(messageIndex, (double)timeMsRaw, pooled, data.Count, parsedByteCount));
                    for (int i = 0; i < parsedByteCount; i++)
                        data.Add(byteSpan[i]);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return context.Fail($"Ошибка чтения файла: {ex.Message}");
            }

            if (frames.Count == 0)
                return context.Fail("Ошибка: в .trc нет кадров.");

            int firstFrame = 0;
            if (options.BlockStartIndex > 0)
            {
                firstFrame = frames.FindIndex(f => f.MessageIndex == options.BlockStartIndex);
                if (firstFrame < 0)
                    return context.Fail($"Ошибка: посылка №{options.BlockStartIndex} не найдена в файле.");
                if (firstFrame > 0)
                    context.Log($"ДСТ: обработка с посылки №{options.BlockStartIndex} (ранние кадры пропущены).");
            }

            var activeFrames = new FrameView(frames, firstFrame);
            var targetIds = devices
                .Where(d => filter.IsDeviceEnabled(d.ID))
                .Select(d => d.ID)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var blockDetection = TrcBlockDetector.Detect(
                activeFrames, options.BlockStartIndex, options.BlockPeriodMs, targetIds, context.Log);

            var outputDevices = CompositeOutput.WithComposites(devices, composites);
            var columns = DstConnectCsvWriter.BuildColumns(outputDevices, filter);
            if (columns.Count == 0)
                return context.Fail("Нет активных параметров для вывода.");

            var columnsByDevice = columns
                .GroupBy(c => c.Device)
                .ToDictionary(g => g.Key, g => g.ToArray());
            var deviceById = new Dictionary<string, Device>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in devices)
                deviceById[d.ID] = d;

            var tracker = new DstConnectBlockTracker(options, blockDetection, columns.Count);
            int[] payload = new int[Device.MaxDataLength];
            var decodeContext = context.Slice(0.5, 0.4);

            for (int f = firstFrame; f < frames.Count; f++)
            {
                if ((f & 0xFFFF) == 0)
                {
                    context.ThrowIfCancellationRequested();
                    decodeContext.ReportProgress((double)f / frames.Count);
                }

                var frame = frames[f];
                tracker.OnFrameStart(frame.MessageIndex, frame.TimeMs, frame.Id);

                data.CopyTo(frame.DataOffset, payload, 0, frame.DataLength);
                var bytes = payload.AsSpan(0, frame.DataLength);

                if (deviceById.TryGetValue(frame.Id, out Device? device) && filter.IsDeviceEnabled(frame.Id))
                {
                    device.SetPayload(bytes);
                    device.Decode();
                    PushValues(device, columnsByDevice, tracker);
                }

                if (composites != null && !composites.IsEmpty)
                {
                    composites.OnMessage(frame.Id, bytes, frame.DataLength);
                    if (composites.IsSourceId(frame.Id))
                    {
                        foreach (var block in composites.Blocks)
                        {
                            if (!filter.IsDeviceEnabled(block.ID) || !block.HasReadyParamForSource(frame.Id)) continue;
                            block.Decode();
                            PushValues(block, columnsByDevice, tracker);
                        }
                    }
                }
            }

            tracker.Finish(frames[^1].TimeMs);

            if (tracker.Rows.Count == 0)
                return context.Fail("Нет данных для CSV ДСТ — проверьте файл посылок.");

            var outputColumns = DstConnectCsvWriter.FilterColumnsWithData(columns, tracker.Rows);
            if (outputColumns.Count == 0)
                return context.Fail("Нет колонок для CSV ДСТ — в логе нет посылок из файла устройств.");

            return DstConnectCsvWriter.Write(
                outputPath, startTime, outputColumns, tracker.Rows, settings.IncludeDeviceIdHeaderRow, context.Slice(0.9, 0.1));
        }

        private static void PushValues(
            Device device,
            Dictionary<Device, DstConnectCsvWriter.Column[]> columnsByDevice,
            DstConnectBlockTracker tracker)
        {
            if (!columnsByDevice.TryGetValue(device, out var deviceColumns)) return;
            foreach (var col in deviceColumns)
            {
                double value = device.FieldErrors[col.ParamIndex]
                    ? TimeSeriesCollector.ErrorMarker
                    : device.Values[col.ParamIndex];
                tracker.UpdateParameter(col.Index, value);
            }
        }

        // Кадры для детектора блоков без копирования списка.
        private sealed class FrameView : IReadOnlyList<(int MessageIndex, double TimeMs, string Id)>
        {
            private readonly List<Frame> _frames;
            private readonly int _offset;

            public FrameView(List<Frame> frames, int offset)
            {
                _frames = frames;
                _offset = offset;
            }

            public (int MessageIndex, double TimeMs, string Id) this[int index]
            {
                get
                {
                    var f = _frames[_offset + index];
                    return (f.MessageIndex, f.TimeMs, f.Id);
                }
            }

            public int Count => _frames.Count - _offset;

            public IEnumerator<(int MessageIndex, double TimeMs, string Id)> GetEnumerator()
            {
                for (int i = _offset; i < _frames.Count; i++)
                    yield return (_frames[i].MessageIndex, _frames[i].TimeMs, _frames[i].Id);
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
