using System.Text;
using logReader;

namespace logReader.UI
{
    internal sealed class DstConnectTrcProcessor
    {
        public void Process(
            string trcPath,
            List<Device> devices,
            string outputPath,
            DstConnectOptions options,
            Action<string> log,
            Dictionary<string, bool>? deviceEnabled = null,
            Dictionary<string, bool[]>? paramEnabled = null,
            CompositeRuntime? composites = null)
        {
            if (!File.Exists(trcPath))
            {
                log($"Ошибка: файл не найден: {trcPath}");
                return;
            }

            logReader.Program.ResetDevicesState(devices);
            composites?.Reset();

            Encoding encoding;
            try
            {
                encoding = LogFileEncoding.Detect(trcPath);
            }
            catch (Exception ex)
            {
                log($"Ошибка определения кодировки: {ex.Message}");
                return;
            }

            if (CanfoxLogParser.LooksLikeCanfoxLog(trcPath, encoding))
            {
                log("Ошибка: CSV ДСТ Коннект применим только к pCAN .trc, не к CANfox.");
                return;
            }

            DateTime? startTime;
            try
            {
                startTime = TrcLogParser.ParseStartTime(File.ReadLines(trcPath, encoding));
            }
            catch (Exception ex)
            {
                log($"Ошибка чтения заголовка: {ex.Message}");
                return;
            }

            var allFrames = new List<(int MessageIndex, double TimeMs, string Id, int[] Bytes)>();

            Span<int> byteSpan = stackalloc int[8];
            foreach (var line in File.ReadLines(trcPath, encoding))
            {
                if (!TrcLogParser.TryParseTrcFrameLine(
                        line,
                        out int messageIndex,
                        out decimal timeMsRaw,
                        out _,
                        out string id,
                        out _,
                        byteSpan,
                        out int parsedByteCount))
                {
                    continue;
                }

                double timeMs = (double)timeMsRaw;
                var bytes = new int[parsedByteCount];
                for (int i = 0; i < parsedByteCount; i++)
                    bytes[i] = byteSpan[i];
                allFrames.Add((messageIndex, timeMs, id, bytes));
            }

            if (allFrames.Count == 0)
            {
                log("Ошибка: в .trc нет кадров.");
                return;
            }

            int processingFromIndex = ResolveProcessingStartIndex(allFrames, options.BlockStartIndex, log);
            if (processingFromIndex < 0)
                return;

            var activeFrames = allFrames
                .Where(f => f.MessageIndex >= processingFromIndex)
                .ToList();

            if (activeFrames.Count == 0)
            {
                log($"Ошибка: после посылки №{processingFromIndex} нет кадров.");
                return;
            }

            if (processingFromIndex > allFrames[0].MessageIndex)
                log($"ДСТ: обработка с посылки №{processingFromIndex} (ранние кадры пропущены).");

            var cycleFrames = activeFrames.Select(f => (f.MessageIndex, f.TimeMs, f.Id)).ToList();
            var targetIds = devices
                .Where(d => deviceEnabled == null || deviceEnabled.GetValueOrDefault(d.ID, true))
                .Select(d => d.ID)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var blockDetection = TrcBlockDetector.Detect(
                cycleFrames,
                options.BlockStartIndex,
                options.BlockPeriodMs,
                targetIds,
                log);
            var tracker = new DstConnectBlockTracker(options, blockDetection);

            var deviceById = new Dictionary<string, Device>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in devices)
                deviceById[d.ID] = d;

            var columns = DstConnectCsvWriter.BuildColumns(
                CompositeOutput.WithComposites(devices, composites),
                deviceEnabled,
                paramEnabled);

            if (columns.Count == 0)
            {
                log("Нет активных параметров для вывода.");
                return;
            }

            foreach (var frame in activeFrames)
            {
                tracker.OnFrameStart(frame.MessageIndex, frame.TimeMs, frame.Id);

                if (deviceById.TryGetValue(frame.Id, out Device? device))
                {
                    bool devOn = deviceEnabled == null || deviceEnabled.GetValueOrDefault(frame.Id, true);
                    if (devOn)
                    {
                        for (int i = 0; i < 8; i++)
                            device.RawBytes[i] = i < frame.Bytes.Length ? frame.Bytes[i] : 0;
                        device.Decode();
                        PushDeviceValues(device, columns, tracker);
                    }
                }

                composites?.OnMessage(frame.Id, frame.Bytes, frame.Bytes.Length);
                PushCompositeValues(composites, frame.Id, deviceEnabled, columns, tracker);
            }

            tracker.Finish(activeFrames[^1].TimeMs);

            if (tracker.Rows.Count == 0)
            {
                log("Нет данных для CSV ДСТ — проверьте файл посылок.");
                return;
            }

            var outputColumns = DstConnectCsvWriter.FilterColumnsWithData(columns, tracker.Rows);
            if (outputColumns.Count == 0)
            {
                log("Нет колонок для CSV ДСТ — в логе нет посылок из файла устройств.");
                return;
            }

            DstConnectCsvWriter.Write(outputPath, startTime, outputColumns, tracker.Rows, log);
        }

        private static int ResolveProcessingStartIndex(
            List<(int MessageIndex, double TimeMs, string Id, int[] Bytes)> allFrames,
            int userAnchorMessageIndex,
            Action<string> log)
        {
            if (userAnchorMessageIndex <= 0)
                return allFrames[0].MessageIndex;

            if (!allFrames.Any(f => f.MessageIndex == userAnchorMessageIndex))
            {
                log($"Ошибка: посылка №{userAnchorMessageIndex} не найдена в файле.");
                return -1;
            }

            return userAnchorMessageIndex;
        }

        private static void PushDeviceValues(
            Device device,
            List<DstConnectCsvWriter.Column> columns,
            DstConnectBlockTracker tracker)
        {
            string prefix = device.ID + "|";
            foreach (var col in columns)
            {
                if (!col.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!int.TryParse(col.Key.AsSpan(prefix.Length), out int idx))
                    continue;
                if (idx < 0 || idx >= device.ProcessedData.Length)
                    continue;
                tracker.UpdateParameter(col.Key, device.ProcessedData[idx]);
            }
        }

        private static void PushCompositeValues(
            CompositeRuntime? composites,
            string id,
            Dictionary<string, bool>? deviceEnabled,
            List<DstConnectCsvWriter.Column> columns,
            DstConnectBlockTracker tracker)
        {
            if (composites == null || composites.IsEmpty || !composites.IsSourceId(id))
                return;

            foreach (var block in composites.Blocks)
            {
                if (!block.HasReadyParamForSource(id)) continue;

                bool devOn = deviceEnabled == null || deviceEnabled.GetValueOrDefault(block.ID, true);
                if (!devOn) continue;

                block.Decode();
                PushDeviceValues(block, columns, tracker);
            }
        }
    }
}
