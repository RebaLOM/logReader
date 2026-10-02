namespace logReader.Processing
{
    // pCAN .trc и CANfox/PCAN-View .txt → time-series.
    internal sealed class PCanLogProcessor
    {
        public ProcessingResult Process(
            string trcPath,
            List<Device> devices,
            string outputPath,
            OutputSettings settings,
            ProcessingContext context)
        {
            if (!File.Exists(trcPath)) return context.Fail($"Ошибка: файл не найден: {trcPath}");

            var composites = settings.Composites;
            var filter = settings.Filter;

            // Устройства кешируются в UI — сброс перед каждым прогоном.
            DeviceFiles.ResetState(devices);
            composites?.Reset();

            var encoding = LogFileEncoding.Detect(trcPath);
            bool isCanfox = CanfoxLogParser.LooksLikeCanfoxLog(trcPath, encoding);

            if (!isCanfox && TrcLogParser.ParseStartTime(trcPath, encoding) == null)
                context.Log("Предупреждение: в .trc нет Start time — время в выводе как мс от начала записи.");

            var deviceByID = new Dictionary<string, Device>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in devices)
                deviceByID[d.ID] = d;

            // pCAN TRC — мс от начала записи; CANfox — время суток.
            var collector = new TimeSeriesCollector(
                CompositeOutput.WithComposites(devices, composites),
                filter,
                isCanfox ? TimeAxisKind.TimeOfDay : TimeAxisKind.Milliseconds);

            Span<int> bytes = stackalloc int[8];
            try
            {
                foreach (var line in LogFileReader.ReadLines(trcPath, encoding, context.Slice(0, 0.8)))
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

                    composites?.OnMessage(id, bytes, parsedByteCount);
                    CompositeOutput.EmitTriggered(composites, id, timeVal, collector, filter);

                    if (!deviceByID.TryGetValue(id, out Device? device)) continue;
                    if (!filter.IsDeviceEnabled(id)) continue;

                    device.SetPayload(bytes[..parsedByteCount]);
                    device.Decode();
                    collector.Record(device, timeVal);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return context.Fail($"Ошибка чтения файла: {ex.Message}");
            }

            return TimeSeriesOutputWriter.Write(collector, outputPath, settings, "pCAN Log", context.Slice(0.8, 0.2));
        }
    }
}
