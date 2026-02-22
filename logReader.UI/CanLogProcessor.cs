using System.Text;
using ClosedXML.Excel;
using logReader;

namespace logReader.UI
{
    internal class CanLogProcessor
    {
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
            log("Начало обработки...");

            if (devices.Count == 0)
            {
                log("Ошибка: устройства не загружены.");
                return;
            }
            log($"Загружено устройств: {devices.Count}");

            if (!File.Exists(csvPath))
            {
                log($"Ошибка: файл лога не найден: {csvPath}");
                return;
            }

            string? outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                log($"Ошибка: директория для сохранения не существует: {outputDir}");
                return;
            }

            var deviceByID = new Dictionary<string, Device>();
            foreach (var d in devices)
                deviceByID[d.ID] = d;

            int currentStep = 0;
            string currentTime = "";
            bool firstStep = true;

            log("Чтение CAN лога...");

            string[] lines;
            try
            {
                
                lines = File.ReadAllLines(csvPath, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                log($"Ошибка чтения файла: {ex.Message}");
                return;
            }

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Log");

            int excelRow = logReader.Program.BuildExcelHeaders(
                ws, devices, deviceEnabled, paramEnabled);

            int processedLines = 0;
            int skippedLines = 0;

            foreach (string line in lines)
            {
               
                if (string.IsNullOrWhiteSpace(line)) continue;

                string[] parts = line.Split(';');

               
                if (parts.Length < 3) { skippedLines++; continue; }

                
                int priority = 0;
                if (parts.Length > 3)
                    int.TryParse(parts[3], out priority);

                if (priority == 1
                    && parts.Length >= 12
                    && deviceByID.TryGetValue(parts[2], out Device? currentDevice))
                {
                    for (int i = 0; i < 8; i++)
                    {
                        
                        currentDevice.RawBytes[i] =
                            int.TryParse(parts[4 + i], out int val) ? val : 0;
                    }
                    currentDevice.Decode();
                    processedLines++;
                }

                
                if (!string.IsNullOrWhiteSpace(parts[0]))
                {
                    
                    if (!int.TryParse(parts[0], out int newStep))
                    {
                        skippedLines++;
                        continue;
                    }

                    
                    string newTime = parts.Length > 1 ? parts[1] : "";

                    if (!firstStep)
                    {
                        excelRow = logReader.Program.BuildExcelRow(
                            ws, excelRow, currentStep, currentTime,
                            devices, deviceEnabled, paramEnabled);
                    }

                    currentStep = newStep;
                    currentTime = newTime;
                    firstStep = false;
                }
            }

            // Дозаписываем последний шаг
            if (!firstStep)
            {
                excelRow = logReader.Program.BuildExcelRow(
                    ws, excelRow, currentStep, currentTime,
                    devices, deviceEnabled, paramEnabled);
            }

            if (skippedLines > 0)
                log($"Пропущено некорректных строк: {skippedLines}");

            try
            {
                workbook.SaveAs(outputPath);
                log("Обработка завершена.");
            }
            catch (Exception ex)
            {
                log($"Ошибка сохранения файла: {ex.Message}");
            }
        }
    }
}