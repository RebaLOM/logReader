using System.Text;
using ClosedXML.Excel;
using logReader;

namespace logReader.UI
{
    internal class CanLogProcessor
    {
        private static bool TryParseCanByte(string raw, out int value)
        {
            if (!int.TryParse(raw, out value))
                return false;

            return value >= 0 && value <= byte.MaxValue;
        }

        public void Process(
            string csvPath,
            string devicesPath,
            string outputPath,
            Action<string> log)
        {
            var devices = logReader.Program.LoadDevicesFromExcel(devicesPath, log);
            Process(csvPath, devices, outputPath, log, null, null);
        }

        public void Process(
            string csvPath,
            List<Device> devices,
            string outputPath,
            Action<string> log,
            Dictionary<string, bool>? deviceEnabled = null,
            Dictionary<string, bool[]>? paramEnabled = null)
        {
            if (devices.Count == 0) { log("Ошибка: устройства не загружены."); return; }

            if (!File.Exists(csvPath)) { log($"Ошибка: файл лога не найден: {csvPath}"); return; }

            string? outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                log($"Ошибка: директория для сохранения не существует: {outputDir}");
                return;
            }

            var deviceByID = new Dictionary<string, Device>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in devices)
                deviceByID[d.ID] = d;

            Encoding encoding = LogFileEncoding.Detect(csvPath);

            // ── Проход 1: определяем какие устройства есть в логе ────────
            var seenIDs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (string line in File.ReadLines(csvPath, encoding))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var p = line.Split(';');
                    if (p.Length < 4) continue;
                    if (int.TryParse(p[3], out int pri) && pri == 1 && p.Length >= 12)
                    {
                        string id = p[2].Trim();
                        if (deviceByID.ContainsKey(id))
                            seenIDs.Add(id);
                    }
                }
            }
            catch (Exception ex) { log($"Ошибка чтения файла: {ex.Message}"); return; }

            var activeDevices = devices
                .Where(d => seenIDs.Contains(d.ID))
                .ToList();

            if (activeDevices.Count == 0)
            {
                log("Нет совпадающих устройств — проверьте файл посылок.");
                return;
            }

            // ── Проход 2: декодируем и пишем Excel ───────────────────────
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Log");

            int excelRow = logReader.Program.BuildExcelHeaders(
                ws, activeDevices, deviceEnabled, paramEnabled);

            int currentStep = 0;
            string currentTime = "";
            bool firstStep = true;
            int writtenRows = 0;

            foreach (string line in File.ReadLines(csvPath, encoding))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split(';');
                if (parts.Length < 3) { continue; }

                int priority = 0;
                if (parts.Length > 3) int.TryParse(parts[3], out priority);

                string id = parts[2].Trim();
                if (priority == 1 && parts.Length >= 12
                    && deviceByID.TryGetValue(id, out Device? dev)
                    && seenIDs.Contains(id))
                {
                    bool validPayload = true;
                    for (int i = 0; i < 8; i++)
                    {
                        if (!TryParseCanByte(parts[4 + i], out int value))
                        {
                            validPayload = false;
                            break;
                        }
                        dev.RawBytes[i] = value;
                    }

                    if (validPayload)
                        dev.Decode();
                }

                if (!string.IsNullOrWhiteSpace(parts[0]))
                {
                    if (!int.TryParse(parts[0], out int newStep)) { continue; }
                    string newTime = parts.Length > 1 ? parts[1] : "";

                    if (!firstStep)
                    {
                        excelRow = logReader.Program.BuildExcelRow(
                            ws, excelRow, currentStep, currentTime,
                            activeDevices, deviceEnabled, paramEnabled);
                        writtenRows++;
                    }

                    currentStep = newStep;
                    currentTime = newTime;
                    firstStep = false;
                }
            }

            if (!firstStep)
            {
                logReader.Program.BuildExcelRow(
                    ws, excelRow, currentStep, currentTime,
                    activeDevices, deviceEnabled, paramEnabled);
                writtenRows++;
            }

            ws.Columns().AdjustToContents();

            try { workbook.SaveAs(outputPath); log("Обработка завершена."); }
            catch (Exception ex) { log($"Ошибка сохранения файла: {ex.Message}"); }
        }
    }
}
