using System.Globalization;

namespace logReader.Processing
{
    // Legacy CSV: строки кадров с общим номером шага; одна строка результата на шаг.
    internal sealed class CanLogProcessor
    {
        public ProcessingResult Process(
            string csvPath,
            List<Device> devices,
            string outputPath,
            OutputSettings settings,
            ProcessingContext context)
        {
            var composites = settings.Composites;
            if (devices.Count == 0 && !settings.HasComposites) return context.Fail("Ошибка: устройства не загружены.");
            if (!File.Exists(csvPath)) return context.Fail($"Ошибка: файл лога не найден: {csvPath}");

            // Устройства кешируются в UI — без сброса второй прогон унаследует прошлые байты.
            DeviceFiles.ResetState(devices);
            composites?.Reset();

            var deviceByID = new Dictionary<string, Device>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in devices)
                deviceByID[d.ID] = d;

            var encoding = LogFileEncoding.Detect(csvPath);

            // Проход 1: только ID из лога — без декодирования байт.
            var seenIDs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var scanContext = context.Slice(0, 0.3);
            foreach (string line in LogFileReader.ReadLines(csvPath, encoding, scanContext))
            {
                if (StepCsvLogParser.TryParseAcceptedId(line, out string id))
                    seenIDs.Add(id);
            }

            var activeDevices = devices.Where(d => seenIDs.Contains(d.ID)).ToList();
            var activeBlocks = ActiveCompositeBlocks(composites, settings.Filter, seenIDs);
            var columns = settings.Filter.BuildColumns(activeDevices.Concat(activeBlocks));
            if (columns.Count == 0)
                return context.Fail("Нет совпадающих устройств — проверьте файл посылок.");

            using var writer = new StepOutputWriter(outputPath, settings.Format, columns, settings.IncludeDeviceIdHeaderRow);

            int currentStep = 0;
            string currentTime = "";
            bool firstStep = true;
            Span<int> msgBytes = stackalloc int[8];

            void FlushStep()
            {
                foreach (var block in activeBlocks)
                    block.Decode();
                writer.WriteRow(currentStep, currentTime);
            }

            try
            {
                foreach (string line in LogFileReader.ReadLines(csvPath, encoding, context.Slice(0.3, 0.6)))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var parts = line.Split(';');
                    if (parts.Length < 3) continue;

                    // Сначала смена шага (запись предыдущего), затем декод текущей строки.
                    if (!string.IsNullOrWhiteSpace(parts[0]))
                    {
                        if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int newStep)) continue;

                        if (!firstStep)
                            FlushStep();

                        currentStep = newStep;
                        currentTime = parts.Length > 1 ? parts[1] : "";
                        firstStep = false;
                    }

                    if (parts.Length < StepCsvLogParser.FirstByteColumn + 8 || !StepCsvLogParser.IsAccepted(parts[3]))
                        continue;
                    if (!CanId.TryNormalize(parts[2], out string id))
                        continue;

                    bool valid = true;
                    for (int i = 0; i < 8; i++)
                    {
                        if (!StepCsvLogParser.TryParseByte(parts[StepCsvLogParser.FirstByteColumn + i], out int v)) { valid = false; break; }
                        msgBytes[i] = v;
                    }
                    if (!valid) continue;

                    if (deviceByID.TryGetValue(id, out Device? dev))
                    {
                        dev.SetPayload(msgBytes);
                        dev.Decode();
                    }
                    composites?.OnMessage(id, msgBytes, 8);
                }

                if (!firstStep)
                    FlushStep();

                writer.Complete();
                if (writer.SheetCount > 1)
                    context.Log($"XLSX: данные разбиты на {writer.SheetCount} лист(а/ов) (лимит {ExcelLayoutBuilder.RowsPerSheet:N0} строк на лист).");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return context.Fail($"Ошибка обработки файла: {ex.Message}");
            }

            context.ReportProgress(1);
            context.Log("Обработка завершена.");
            return ProcessingResult.Ok(outputPath, writer.RowsWritten);
        }

        internal static List<CompositeDevice> ActiveCompositeBlocks(CompositeRuntime? composites, OutputFilter filter, ISet<string> seenIds)
        {
            // Составной блок в вывод — только если в логе был хотя бы один его источник.
            var activeBlocks = new List<CompositeDevice>();
            if (composites == null || composites.IsEmpty) return activeBlocks;
            foreach (var block in composites.Blocks)
            {
                if (!filter.IsDeviceEnabled(block.ID)) continue;
                if (block.Signals.Any(s => s.Pieces.Any(pc => seenIds.Contains(pc.SourceId))))
                    activeBlocks.Add(block);
            }
            return activeBlocks;
        }
    }
}
