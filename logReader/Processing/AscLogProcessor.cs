namespace logReader.Processing
{
    internal sealed class AscLogProcessor
    {
        public ProcessingResult Process(
            string ascPath,
            List<Device> devices,
            string outputPath,
            OutputSettings settings,
            ProcessingContext context)
        {
            var composites = settings.Composites;
            var filter = settings.Filter;
            if (devices.Count == 0 && !settings.HasComposites) return context.Fail("Ошибка: устройства не загружены.");
            if (!File.Exists(ascPath)) return context.Fail($"Ошибка: файл не найден: {ascPath}");

            // Устройства кешируются в UI — сброс перед каждым прогоном.
            DeviceFiles.ResetState(devices);
            composites?.Reset();

            var encoding = LogFileEncoding.Detect(ascPath);

            var format = AscFileFormat.Read(ascPath, encoding);
            long baseTicks = format.BaseTimeTicks ?? 0;
            if (format.BaseTimeTicks == null)
                context.Log("Предупреждение: не найдено стартовое время (строка 'date ...'). Время будет считаться от 00:00:00.000.");

            var deviceByID = new Dictionary<string, Device>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in devices)
                deviceByID[d.ID] = d;

            var collector = new TimeSeriesCollector(
                CompositeOutput.WithComposites(devices, composites), filter, TimeAxisKind.TimeOfDay);

            try
            {
                Span<int> bytes = stackalloc int[Device.MaxDataLength];
                long previousTicks = 0;

                foreach (var line in LogFileReader.ReadLines(ascPath, encoding, context.Slice(0, 0.8)))
                {
                    if (!AscLogParser.TryParseFrameLine(line, format, out long offsetTicks, out string id, bytes, out int parsedByteCount))
                        continue;

                    // «timestamps relative» — смещение от предыдущего кадра, а не от начала записи.
                    long offset = format.RelativeTimestamps ? previousTicks + offsetTicks : offsetTicks;
                    previousTicks = offset;

                    // Суммируем смещение без сброса в полночь — иначе ломаются логи через 00:00.
                    double timeVal = (baseTicks + offset) / (double)TimeSpan.TicksPerDay;

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
                return context.Fail($"Ошибка обработки файла: {ex.Message}");
            }

            return TimeSeriesOutputWriter.Write(collector, outputPath, settings, "ASC Log", context.Slice(0.8, 0.2));
        }
    }
}
