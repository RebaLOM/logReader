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

            var deviceByID = new Dictionary<string, Device>();
            foreach (var d in devices)
                deviceByID[d.ID] = d;

            int currentStep = 0;
            string currentTime = "";
            bool firstStep = true;

            log("Чтение CAN лога...");

            string[] lines = File.ReadAllLines(csvPath);

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Log");

            int excelRow = logReader.Program.BuildExcelHeaders(ws, devices, deviceEnabled, paramEnabled);

            foreach (string line in lines)
            {
                string[] parts = line.Split(';');

                if (parts.Length < 12)
                    continue;

                int priority;
                int.TryParse(parts[3], out priority);

                if (priority == 1 && deviceByID.TryGetValue(parts[2], out Device? currentDevice))
                {
                    for (int i = 0; i < 8; i++)
                        currentDevice.RawBytes[i] = Convert.ToInt32(parts[4 + i], 10);

                    currentDevice.Decode();
                }

                if (parts[0] != "")
                {
                    int newStep = int.Parse(parts[0]);
                    string newTime = parts[1];

                    if (!firstStep)
                    {
                        excelRow = logReader.Program.BuildExcelRow(ws, excelRow, currentStep, currentTime, devices, deviceEnabled, paramEnabled);
                    }

                    currentStep = newStep;
                    currentTime = newTime;
                    firstStep = false;
                }
            }

            excelRow = logReader.Program.BuildExcelRow(ws, excelRow, currentStep, currentTime, devices, deviceEnabled, paramEnabled);

            workbook.SaveAs(outputPath);

            log("Обработка завершена.");
        }
    }
}
