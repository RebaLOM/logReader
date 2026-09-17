using System.Globalization;
using System.Text;
using logReader;

namespace logReader.UI
{
    internal class PCanLogProcessor
    {
        internal static void WriteOutput(
            List<Device> devices,
            Dictionary<string, List<(double TimeVal, string[] Values)>> deviceData,
            Dictionary<string, bool>? deviceEnabled,
            Dictionary<string, bool[]>? paramEnabled,
            string outputPath,
            OutputFormat outputFormat,
            bool isCanfox,
            Action<string> log,
            bool includeDeviceIdHeaderRow = false)
            => TimeSeriesOutputWriter.Write(
                outputFormat, devices, deviceData, deviceEnabled, paramEnabled,
                outputPath, "pCAN Log", isCanfox, log, includeDeviceIdHeaderRow);

        public void Process(
            string trcPath,
            List<Device> devices,
            string outputPath,
            OutputFormat outputFormat,
            Action<string> log,
            Dictionary<string, bool>? deviceEnabled = null,
            Dictionary<string, bool[]>? paramEnabled = null,
            CompositeRuntime? composites = null,
            bool includeDeviceIdHeaderRow = false)
        {
            if (!File.Exists(trcPath)) { log($"Ошибка: файл не найден: {trcPath}"); return; }

            // Устройства кешируются в UI — сброс перед каждым прогоном.
            logReader.Program.ResetDevicesState(devices);
            composites?.Reset();

            Encoding encoding;
            try
            {
                encoding = LogFileEncoding.Detect(trcPath);
            }
            catch (Exception ex) { log($"Ошибка определения кодировки: {ex.Message}"); return; }

            bool isCanfox;
            try
            {
                isCanfox = CanfoxLogParser.LooksLikeCanfoxLog(trcPath, encoding);
            }
            catch (Exception ex) { log($"Ошибка чтения файла: {ex.Message}"); return; }

            // Нужен для предупреждения; время кадра всегда — мс из строки TRC.
            if (!isCanfox)
            {
                try
                {
                    if (TrcLogParser.ParseStartTime(File.ReadLines(trcPath, encoding)) == null)
                        log("Предупреждение: в .trc нет Start time — время в выводе как мс от начала записи.");
                }
                catch (Exception ex) { log($"Ошибка чтения файла: {ex.Message}"); return; }
            }

            var deviceByID = new Dictionary<string, Device>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in devices)
                deviceByID[d.ID] = d;

            // timeVal: для pCAN TRC — мс от начала записи; для CANfox — доля суток (HH:mm:ss.fff).
            var deviceData = new Dictionary<string, List<(double TimeVal, string[] Values)>>(
                StringComparer.OrdinalIgnoreCase);

            Span<int> bytes = stackalloc int[8];
            foreach (var line in File.ReadLines(trcPath, encoding))
            {
                string id;
                int parsedByteCount;
                double timeVal;

                if (isCanfox)
                {
                    if (!CanfoxLogParser.TryParseCanfoxFrameLine(
                            line,
                            out timeVal,
                            out id,
                            bytes,
                            out parsedByteCount))
                    {
                        continue;
                    }
                }
                else
                {
                    if (!TrcLogParser.TryParseTrcFrameLine(
                            line,
                            out decimal timeMsRaw,
                            out _,
                            out id,
                            out _,
                            bytes,
                            out parsedByteCount))
                    {
                        continue;
                    }

                    // Всегда мс с начала записи — не TimeOfDay (иначе Excel/CSV путает с часами суток).
                    timeVal = (double)timeMsRaw;
                }

                composites?.OnMessage(id, bytes, parsedByteCount);
                CompositeOutput.EmitTriggered(composites, id, timeVal, deviceData);

                if (!deviceByID.TryGetValue(id, out Device? device)) continue;

                bool devOn = deviceEnabled == null || deviceEnabled.GetValueOrDefault(id, true);
                if (!devOn) continue;

                for (int i = 0; i < 8; i++)
                    device.RawBytes[i] = i < parsedByteCount ? bytes[i] : 0;

                device.Decode();

                if (!deviceData.ContainsKey(id))
                    deviceData[id] = new List<(double, string[])>();

                deviceData[id].Add((timeVal, (string[])device.ProcessedData.Clone()));
            }

            if (deviceData.Count == 0)
            {
                log("Нет совпадающих устройств — проверьте файл посылок.");
                return;
            }

            var outputDevices = CompositeOutput.WithComposites(devices, composites);
            WriteOutput(outputDevices, deviceData, deviceEnabled, paramEnabled, outputPath, outputFormat, isCanfox, log, includeDeviceIdHeaderRow);
        }
    }
}
