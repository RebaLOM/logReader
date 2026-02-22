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
            log("Начало обработки...");

            var devices = logReader.Program.LoadDevicesFromExcel(devicesPath, log);

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

            int excelRow = logReader.Program.BuildExcelHeaders(ws, devices);

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
                        excelRow = logReader.Program.BuildExcelRow(ws, excelRow, currentStep, currentTime, devices);
                    }

                    currentStep = newStep;
                    currentTime = newTime;
                    firstStep = false;
                }
            }

            excelRow = logReader.Program.BuildExcelRow(ws, excelRow, currentStep, currentTime, devices);

            workbook.SaveAs(outputPath);

            log("Обработка завершена.");
        }
    }
}
