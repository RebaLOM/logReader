using System.Globalization;
using System.Text;
using logReader;

namespace logReader.UI
{
    internal static class TrcBatchAggregator
    {
        internal sealed class TrcAggregate
        {
            public required Dictionary<string, List<(double TimeVal, string[] Values)>> DeviceData { get; init; }
            public required bool IsCanfox { get; init; }
        }

        internal static bool TryBuildMergedAggregate(
            IEnumerable<string> trcPaths,
            List<Device> devices,
            Dictionary<string, bool>? deviceEnabled,
            Dictionary<string, bool[]>? paramEnabled,
            Action<string> log,
            out TrcAggregate aggregate,
            CompositeRuntime? composites = null)
        {
            aggregate = default!;

            var deviceById = new Dictionary<string, Device>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in devices)
                deviceById[d.ID] = d;

            bool? isCanfoxExpected = null;
            var deviceData = new Dictionary<string, List<(double TimeVal, string[] Values)>>(StringComparer.OrdinalIgnoreCase);

            Span<int> bytes = stackalloc int[8];

            int usedFiles = 0;
            foreach (var trcPath in trcPaths)
            {
                if (!File.Exists(trcPath))
                {
                    log($"Пропуск: файл не найден: {trcPath}");
                    continue;
                }

                Encoding encoding;
                try { encoding = LogFileEncoding.Detect(trcPath); }
                catch (Exception ex)
                {
                    log($"Пропуск: ошибка определения кодировки ({Path.GetFileName(trcPath)}): {ex.Message}");
                    continue;
                }

                bool isCanfox;
                try { isCanfox = CanfoxLogParser.LooksLikeCanfoxLog(trcPath, encoding); }
                catch (Exception ex)
                {
                    log($"Пропуск: ошибка чтения ({Path.GetFileName(trcPath)}): {ex.Message}");
                    continue;
                }

                if (isCanfoxExpected == null)
                    isCanfoxExpected = isCanfox;
                else if (isCanfoxExpected.Value != isCanfox)
                {
                    log($"Пропуск: несовместимый тип .trc — {Path.GetFileName(trcPath)}");
                    continue;
                }

                DateTime? startTime = null;
                if (!isCanfox)
                {
                    try { startTime = TrcLogParser.ParseStartTime(File.ReadLines(trcPath, encoding)); }
                    catch (Exception ex)
                    {
                        log($"Пропуск: ошибка чтения заголовка ({Path.GetFileName(trcPath)}): {ex.Message}");
                        continue;
                    }
                }

                // Устройства кешируются в UI — сброс перед каждым файлом пакета.
                logReader.Program.ResetDevicesState(devices);
                composites?.Reset();

                foreach (var line in File.ReadLines(trcPath, encoding))
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

                        double timeMs = (double)timeMsRaw;
                        timeVal = startTime.HasValue
                            ? startTime.Value.AddMilliseconds(timeMs).TimeOfDay.TotalDays
                            : timeMs;
                    }

                    composites?.OnMessage(id, bytes, parsedByteCount);
                    CompositeOutput.EmitTriggered(composites, id, timeVal, deviceData);

                    if (!deviceById.TryGetValue(id, out Device? device))
                        continue;

                    bool devOn = deviceEnabled == null || deviceEnabled.GetValueOrDefault(id, true);
                    if (!devOn) continue;

                    for (int i = 0; i < 8; i++)
                        device.RawBytes[i] = i < parsedByteCount ? bytes[i] : 0;
                    device.Decode();

                    if (!deviceData.TryGetValue(id, out var list))
                    {
                        list = new List<(double, string[])>();
                        deviceData[id] = list;
                    }
                    list.Add((timeVal, (string[])device.ProcessedData.Clone()));
                }

                usedFiles++;
            }

            if (usedFiles == 0 || deviceData.Count == 0 || isCanfoxExpected == null)
                return false;

            aggregate = new TrcAggregate
            {
                DeviceData = deviceData,
                IsCanfox = isCanfoxExpected.Value
            };
            return true;
        }

        internal static bool TryBuildAggregatesByDate(
            IEnumerable<string> trcPaths,
            List<Device> devices,
            Dictionary<string, bool>? deviceEnabled,
            Dictionary<string, bool[]>? paramEnabled,
            Action<string> log,
            out Dictionary<DateOnly, TrcAggregate> aggregatesByDate,
            CompositeRuntime? composites = null)
        {
            aggregatesByDate = new Dictionary<DateOnly, TrcAggregate>();

            var deviceById = new Dictionary<string, Device>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in devices)
                deviceById[d.ID] = d;

            Span<int> bytes = stackalloc int[8];

            int usedFiles = 0;
            foreach (var trcPath in trcPaths)
            {
                if (!File.Exists(trcPath))
                {
                    log($"Пропуск: файл не найден: {trcPath}");
                    continue;
                }

                Encoding encoding;
                try { encoding = LogFileEncoding.Detect(trcPath); }
                catch (Exception ex)
                {
                    log($"Пропуск: ошибка определения кодировки ({Path.GetFileName(trcPath)}): {ex.Message}");
                    continue;
                }

                bool isCanfox;
                try { isCanfox = CanfoxLogParser.LooksLikeCanfoxLog(trcPath, encoding); }
                catch (Exception ex)
                {
                    log($"Пропуск: ошибка чтения ({Path.GetFileName(trcPath)}): {ex.Message}");
                    continue;
                }

                if (isCanfox)
                {
                    log($"Пропуск: разбивка по датам не поддерживается для CANfox .trc — {Path.GetFileName(trcPath)}");
                    continue;
                }

                DateTime? startTime;
                try { startTime = TrcLogParser.ParseStartTime(File.ReadLines(trcPath, encoding)); }
                catch (Exception ex)
                {
                    log($"Пропуск: ошибка чтения заголовка ({Path.GetFileName(trcPath)}): {ex.Message}");
                    continue;
                }

                if (!startTime.HasValue)
                {
                    log($"Пропуск: в .trc не найден Start time (нужен для разбивки по датам) — {Path.GetFileName(trcPath)}");
                    continue;
                }

                // Устройства кешируются в UI — сброс перед каждым файлом пакета.
                logReader.Program.ResetDevicesState(devices);
                composites?.Reset();

                foreach (var line in File.ReadLines(trcPath, encoding))
                {
                    if (!TrcLogParser.TryParseTrcFrameLine(line, out decimal timeMsRaw, out _, out string id, out _, bytes, out int parsedByteCount))
                        continue;

                    double timeMs = (double)timeMsRaw;
                    DateTime frameTime = startTime.Value.AddMilliseconds(timeMs);
                    DateOnly date = DateOnly.FromDateTime(frameTime);
                    double timeVal = frameTime.TimeOfDay.TotalDays;

                    composites?.OnMessage(id, bytes, parsedByteCount);

                    bool hasDevice = deviceById.TryGetValue(id, out Device? device);
                    bool devOn = hasDevice && (deviceEnabled == null || deviceEnabled.GetValueOrDefault(id, true));
                    bool isCompositeSource = composites != null && composites.IsSourceId(id);

                    if (!devOn && !isCompositeSource)
                        continue;

                    if (!aggregatesByDate.TryGetValue(date, out var agg))
                    {
                        agg = new TrcAggregate
                        {
                            IsCanfox = false,
                            DeviceData = new Dictionary<string, List<(double, string[])>>(StringComparer.OrdinalIgnoreCase)
                        };
                        aggregatesByDate[date] = agg;
                    }

                    CompositeOutput.EmitTriggered(composites, id, timeVal, agg.DeviceData);

                    if (!devOn) continue;

                    for (int i = 0; i < 8; i++)
                        device!.RawBytes[i] = i < parsedByteCount ? bytes[i] : 0;
                    device!.Decode();

                    if (!agg.DeviceData.TryGetValue(id, out var list))
                    {
                        list = new List<(double, string[])>();
                        agg.DeviceData[id] = list;
                    }
                    list.Add((timeVal, (string[])device.ProcessedData.Clone()));
                }

                usedFiles++;
            }

            return usedFiles > 0 && aggregatesByDate.Count > 0;
        }
    }
}

