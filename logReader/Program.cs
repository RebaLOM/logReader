using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ClosedXML.Excel;

namespace logReader
{
    public class Device
    {
        public string ID;
        public string[] headers;
        public int[] RawBytes = new int[8];
        public string[] RawBinaries = new string[8];
        public string[] ProcessedData;

        public void ToBinaries(int index)
        {
            // 🟡 УЛУЧШЕНО: проверка диапазона индекса
            if (index < 0 || index >= RawBytes.Length) return;
            RawBinaries[index] = Convert.ToString(RawBytes[index], 2).PadLeft(8, '0');
        }

        public Device(string ID, int headersCount)
        {
            this.ID = ID;
            headers = new string[headersCount];
            ProcessedData = new string[headersCount];
            for (int i = 0; i < headersCount; i++) ProcessedData[i] = "0";
        }

        public virtual void Decode() { }
    }

    public class FieldInstruction
    {
        public int FieldIndex;
        public string Header = "";
        public int ByteLow;
        public int? ByteHigh;
        public double Scale;
        public double Offset;
        public string Type = "";
        public int StartBit;
        public int LenghtBit;
    }

    public class DynamicDevice : Device
    {
        private List<FieldInstruction> instructions;

        public DynamicDevice(string deviceID, List<FieldInstruction> fieldInstructions)
            : base(deviceID, fieldInstructions.Count)
        {
            instructions = fieldInstructions;
            foreach (var instr in instructions)
                headers[instr.FieldIndex] = instr.Header;
        }

        public override void Decode()
        {
            foreach (var instr in instructions)
            {
                try
                {
                    if (instr.Type == "NUM")
                    {
                        // 🔴 ИСПРАВЛЕНО: проверка что ByteLow и ByteHigh в диапазоне 0-7
                        if (instr.ByteLow < 0 || instr.ByteLow >= RawBytes.Length)
                        {
                            ProcessedData[instr.FieldIndex] = "ERR";
                            continue;
                        }

                        int rawValue;
                        if (instr.ByteHigh.HasValue)
                        {
                            if (instr.ByteHigh.Value < 0 || instr.ByteHigh.Value >= RawBytes.Length)
                            {
                                ProcessedData[instr.FieldIndex] = "ERR";
                                continue;
                            }
                            rawValue = (RawBytes[instr.ByteHigh.Value] * 256) + RawBytes[instr.ByteLow];
                        }
                        else
                        {
                            rawValue = RawBytes[instr.ByteLow];
                        }

                        double physicalValue = (rawValue * instr.Scale) + instr.Offset;
                        ProcessedData[instr.FieldIndex] = physicalValue.ToString(CultureInfo.InvariantCulture);
                    }
                    else if (instr.Type == "BIN")
                    {
                        // 🔴 ИСПРАВЛЕНО: проверка ByteLow в диапазоне
                        if (instr.ByteLow < 0 || instr.ByteLow >= RawBytes.Length)
                        {
                            ProcessedData[instr.FieldIndex] = "ERR";
                            continue;
                        }

                        ToBinaries(instr.ByteLow);
                        string binary = RawBinaries[instr.ByteLow] ?? "00000000";

                        // 🔴 ИСПРАВЛЕНО: проверка что StartBit + LenghtBit не выходят за пределы строки
                        if (instr.StartBit < 0 || instr.LenghtBit <= 0
                            || instr.StartBit + instr.LenghtBit > binary.Length)
                        {
                            ProcessedData[instr.FieldIndex] = "ERR";
                            continue;
                        }

                        string bits = binary.Substring(instr.StartBit, instr.LenghtBit);
                        ProcessedData[instr.FieldIndex] = Convert.ToInt32(bits, 2).ToString();
                    }
                }
                catch (Exception)
                {
                    // Защита от любой неожиданной ошибки декодирования одного поля
                    if (instr.FieldIndex >= 0 && instr.FieldIndex < ProcessedData.Length)
                        ProcessedData[instr.FieldIndex] = "ERR";
                }
            }
        }
    }

    // Статические устройства — без изменений
    internal class Device_180128D0 : Device
    {
        public Device_180128D0() : base("180128D0", 2)
        {
            headers[0] = "Текущий максимальный предел крутящего момента";
            headers[1] = "Целевая скорость";
        }
        public override void Decode()
        {
            int rawTorque = (RawBytes[3] * 256) + RawBytes[2];
            ProcessedData[0] = (rawTorque - 10000).ToString();
            int rawSpeed = (RawBytes[5] * 256) + RawBytes[4];
            ProcessedData[1] = ((rawSpeed * 0.5) - 15000).ToString();
        }
    }

    internal class Device_1801D0EF : Device
    {
        public Device_1801D0EF() : base("1801D0EF", 4)
        {
            headers[0] = "Напряжение шины";
            headers[1] = "Температура контроллера мотора";
            headers[2] = "Температура мотора";
            headers[3] = "Ток шины";
        }
        public override void Decode()
        {
            ProcessedData[0] = ((RawBytes[3] * 256) + RawBytes[2]).ToString();
            ProcessedData[1] = (RawBytes[4] - 40).ToString();
            ProcessedData[2] = (RawBytes[5] - 40).ToString();
            double busCurrent = ((RawBytes[7] * 256) + RawBytes[6]) * 0.1 - 20000;
            ProcessedData[3] = busCurrent.ToString();
        }
    }

    internal class Device_1802D0EF : Device
    {
        public Device_1802D0EF() : base("1802D0EF", 4)
        {
            headers[0] = "Трехфазный выходной ток";
            headers[1] = "Текущий крутящий момент двигателя";
            headers[2] = "Фактический крутящий момент";
            headers[3] = "Верхний предел крутящего момента";
        }
        public override void Decode()
        {
            ProcessedData[0] = (((RawBytes[1] * 256) + RawBytes[0]) * 0.1).ToString();
            ProcessedData[1] = ((RawBytes[3] * 256) + RawBytes[2] - 30000).ToString();
            ProcessedData[2] = ((RawBytes[5] * 256) + RawBytes[4] - 10000).ToString();
            ProcessedData[3] = ((RawBytes[7] * 256) + RawBytes[6] - 10000).ToString();
        }
    }

    internal class Device_18FF0101 : Device
    {
        public Device_18FF0101() : base("18FF0101", 2)
        {
            headers[0] = "Команда управления скоростью";
            headers[1] = "Команда управления крутящим моментом";
        }
        public override void Decode()
        {
            ProcessedData[0] = (((RawBytes[1] * 256) + RawBytes[0]) * 0.5 - 15000).ToString();
            ProcessedData[1] = (((RawBytes[3] * 256) + RawBytes[2]) * 0.1 - 3200).ToString();
        }
    }

    internal class Device_18FF0201 : Device
    {
        public Device_18FF0201() : base("18FF0201", 2)
        {
            headers[0] = "Команда управления скоростью";
            headers[1] = "Команда управления крутящим моментом";
        }
        public override void Decode()
        {
            ProcessedData[0] = (((RawBytes[1] * 256) + RawBytes[0]) * 0.5 - 15000).ToString();
            ProcessedData[1] = (((RawBytes[3] * 256) + RawBytes[2]) * 0.1 - 3200).ToString();
        }
    }

    internal class Device_18FF31F1 : Device
    {
        public Device_18FF31F1() : base("18FF31F1", 3)
        {
            headers[0] = "Фактическая скорость вращения двигателя";
            headers[1] = "Фактический крутящий момент двигателя";
            headers[2] = "Максимальный выходной крутящий момент двигателя";
        }
        public override void Decode()
        {
            ProcessedData[0] = (((RawBytes[1] * 256) + RawBytes[0]) * 0.5 - 15000).ToString();
            ProcessedData[1] = (((RawBytes[3] * 256) + RawBytes[2]) * 0.1 - 3200).ToString();
            ProcessedData[2] = (RawBytes[4] * 0.5).ToString();
        }
    }

    internal class Device_18FF32F1 : Device
    {
        public Device_18FF32F1() : base("18FF32F1", 4)
        {
            headers[0] = "Напряжение шины постоянного тока";
            headers[1] = "Ток шины постоянного тока";
            headers[2] = "Температура двигателя";
            headers[3] = "Температура преобразователя";
        }
        public override void Decode()
        {
            ProcessedData[0] = (((RawBytes[1] * 256) + RawBytes[0]) * 0.2).ToString();
            ProcessedData[1] = (((RawBytes[4] * 256) + RawBytes[3]) * 0.4 - 800).ToString();
            ProcessedData[2] = (RawBytes[6] - 40).ToString();
            ProcessedData[3] = (RawBytes[7] - 40).ToString();
        }
    }

    internal class Device_18FF35F1 : Device
    {
        public Device_18FF35F1() : base("18FF35F1", 1)
        {
            headers[0] = "Сбои СУ двигателя";
        }
        public override void Decode()
        {
            ToBinaries(1);
            // 🟡 УЛУЧШЕНО: проверка длины перед Substring
            string bin = RawBinaries[1] ?? "00000000";
            if (bin.Length >= 6)
                ProcessedData[0] = Convert.ToInt32(bin.Substring(0, 6), 2).ToString();
            else
                ProcessedData[0] = "0";
        }
    }

    internal class Device_18FF41F1 : Device
    {
        public Device_18FF41F1() : base("18FF41F1", 3)
        {
            headers[0] = "Фактическая скорость вращения двигателя";
            headers[1] = "Фактический крутящий момент двигателя";
            headers[2] = "Максимальный выходной крутящий момент двигателя";
        }
        public override void Decode()
        {
            ProcessedData[0] = (((RawBytes[1] * 256) + RawBytes[0]) * 0.5 - 15000).ToString();
            ProcessedData[1] = (((RawBytes[3] * 256) + RawBytes[2]) * 0.1 - 3200).ToString();
            ProcessedData[2] = (RawBytes[4] * 0.5).ToString();
        }
    }

    internal class Device_18FF42F1 : Device
    {
        public Device_18FF42F1() : base("18FF42F1", 4)
        {
            headers[0] = "Напряжение шины постоянного тока";
            headers[1] = "Ток шины постоянного тока";
            headers[2] = "Температура двигателя";
            headers[3] = "Температура преобразователя";
        }
        public override void Decode()
        {
            ProcessedData[0] = (((RawBytes[1] * 256) + RawBytes[0]) * 0.2).ToString();
            ProcessedData[1] = (((RawBytes[4] * 256) + RawBytes[3]) * 0.4 - 800).ToString();
            ProcessedData[2] = (RawBytes[6] - 40).ToString();
            ProcessedData[3] = (RawBytes[7] - 40).ToString();
        }
    }

    internal class Device_18FF45F1 : Device
    {
        public Device_18FF45F1() : base("18FF45F1", 1)
        {
            headers[0] = "Сбои СУ двигателя";
        }
        public override void Decode()
        {
            ToBinaries(1);
            string bin = RawBinaries[1] ?? "00000000";
            if (bin.Length >= 6)
                ProcessedData[0] = Convert.ToInt32(bin.Substring(0, 6), 2).ToString();
            else
                ProcessedData[0] = "0";
        }
    }

    internal class Device_1FEEFF85 : Device
    {
        public Device_1FEEFF85() : base("1FEEFF85", 5)
        {
            headers[0] = "Минимальное напряжение на блоке, мВ";
            headers[1] = "Максимальное напряжение на блоке, мВ";
            headers[2] = "Минимальная температура ячейки";
            headers[3] = "Максимальная температура ячейки";
            headers[4] = "Состояние заряда SOC, %";
        }
        public override void Decode()
        {
            ProcessedData[0] = ((RawBytes[0] * 256) + RawBytes[1]).ToString();
            ProcessedData[1] = ((RawBytes[2] * 256) + RawBytes[3]).ToString();
            ProcessedData[2] = (RawBytes[4] - 40).ToString();
            ProcessedData[3] = (RawBytes[5] - 40).ToString();
            ProcessedData[4] = RawBytes[6].ToString();
        }
    }

    internal class Device_1FEEFF87 : Device
    {
        public Device_1FEEFF87() : base("1FEEFF87", 4)
        {
            headers[0] = "Напряжение на входе контакторов, В";
            headers[1] = "Напряжение на выходе контакторов, В";
            headers[2] = "Напряжение батареи, В";
            headers[3] = "Дисбаланс батареи, мВ";
        }
        public override void Decode()
        {
            ProcessedData[0] = ((RawBytes[1] * 256) + RawBytes[0]).ToString();
            ProcessedData[1] = ((RawBytes[3] * 256) + RawBytes[2]).ToString();
            ProcessedData[2] = ((RawBytes[5] * 256) + RawBytes[4]).ToString();
            ProcessedData[3] = ((RawBytes[7] * 256) + RawBytes[6]).ToString();
        }
    }

    internal class Device_1FEEFF88 : Device
    {
        public Device_1FEEFF88() : base("1FEEFF88", 5)
        {
            headers[0] = "Температура охлаждающей жидкости на входе";
            headers[1] = "Температура охлаждающей жидкости на выходе";
            headers[2] = "Сопротивление изоляции (текущее)";
            headers[3] = "Сопротивление изоляции (выключено)";
            headers[4] = "Счетчик измерений сопротивления изоляции";
        }
        public override void Decode()
        {
            // 🟡 ПРИМЕЧАНИЕ: здесь +40, у других устройств -40.
            // Оставлено как есть — уточните по документации на это устройство.
            ProcessedData[0] = (RawBytes[0] + 40).ToString();
            ProcessedData[1] = (RawBytes[1] + 40).ToString();
            ProcessedData[2] = ((RawBytes[3] * 256) + RawBytes[2]).ToString();
            ProcessedData[3] = ((RawBytes[5] * 256) + RawBytes[4]).ToString();
            ProcessedData[4] = ((RawBytes[7] * 256) + RawBytes[6]).ToString();
        }
    }

    public class Program
    {
        // Цвета для чередования групп устройств в заголовке
        public static readonly XLColor[] DeviceColors =
        {
            XLColor.FromArgb(198, 214, 240), // синий
            XLColor.FromArgb(198, 232, 210), // зелёный
            XLColor.FromArgb(255, 229, 190), // оранжевый
            XLColor.FromArgb(230, 210, 240), // фиолетовый
            XLColor.FromArgb(255, 210, 210), // красный
            XLColor.FromArgb(210, 245, 245), // голубой
            XLColor.FromArgb(255, 240, 180), // жёлтый
            XLColor.FromArgb(200, 240, 220), // мятный
            XLColor.FromArgb(240, 210, 200), // персиковый
            XLColor.FromArgb(220, 220, 240), // лавандовый
        };

        public static int BuildExcelHeaders(
            IXLWorksheet ws, List<Device> devices)
            => BuildExcelHeaders(ws, devices, null, null);

        public static int BuildExcelHeaders(
            IXLWorksheet ws, List<Device> devices,
            Dictionary<string, bool>? deviceEnabled,
            Dictionary<string, bool[]>? paramEnabled)
        {
            // Строка 1 — общий заголовок (Шаг, Время, затем ID устройства растянутый на его параметры)
            // Строка 2 — имена параметров

            int col = 1;
            int colorIdx = 0;

            // Шаг и Время — занимают обе строки
            StyleMergedHeader(ws, 1, col, XLColor.FromArgb(180, 180, 180));
            StyleMergedHeader(ws, 2, col, XLColor.FromArgb(180, 180, 180));
            ws.Cell(1, col).Value = "Шаг";
            col++;

            StyleMergedHeader(ws, 1, col, XLColor.FromArgb(180, 180, 180));
            StyleMergedHeader(ws, 2, col, XLColor.FromArgb(180, 180, 180));
            ws.Cell(1, col).Value = "Время";
            col++;

            foreach (var device in devices)
            {
                bool devOn = deviceEnabled == null || deviceEnabled.GetValueOrDefault(device.ID, true);
                if (!devOn) continue;

                // Собираем активные параметры
                var activeParams = new List<string>();
                for (int i = 0; i < device.headers.Length; i++)
                {
                    bool paramOn = paramEnabled == null
                        || !paramEnabled.TryGetValue(device.ID, out var arr)
                        || (i < arr.Length && arr[i]);
                    if (paramOn) activeParams.Add(device.headers[i]);
                }
                if (activeParams.Count == 0) continue;

                XLColor bg = DeviceColors[colorIdx % DeviceColors.Length];
                colorIdx++;

                // Строка 1 — ID устройства, растянутый на все его колонки
                int devStartCol = col;
                int devEndCol = col + activeParams.Count - 1;

                if (devStartCol == devEndCol)
                {
                    ws.Cell(1, devStartCol).Value = device.ID;
                }
                else
                {
                    ws.Range(1, devStartCol, 1, devEndCol).Merge();
                    ws.Cell(1, devStartCol).Value = device.ID;
                }
                StyleDeviceHeader(ws.Cell(1, devStartCol), bg);

                // Строка 2 — имена параметров
                foreach (var header in activeParams)
                {
                    var cell = ws.Cell(2, col);
                    cell.Value = header;
                    StyleParamHeader(cell, bg);
                    col++;
                }
            }

            // Заморозить первые две строки
            ws.SheetView.FreezeRows(2);

            return 3; // данные начинаются со строки 3
        }

        public static int BuildExcelRow(
            IXLWorksheet ws, int excelRow, int step, string time, List<Device> devices)
            => BuildExcelRow(ws, excelRow, step, time, devices, null, null);

        public static int BuildExcelRow(
            IXLWorksheet ws, int excelRow, int step, string time,
            List<Device> devices,
            Dictionary<string, bool>? deviceEnabled,
            Dictionary<string, bool[]>? paramEnabled)
        {
            int col = 1;
            ws.Cell(excelRow, col++).Value = step;
            ws.Cell(excelRow, col++).Value = time;

            foreach (var device in devices)
            {
                bool devOn = deviceEnabled == null || deviceEnabled.GetValueOrDefault(device.ID, true);
                if (!devOn) continue;

                for (int i = 0; i < device.ProcessedData.Length; i++)
                {
                    bool paramOn = paramEnabled == null
                        || !paramEnabled.TryGetValue(device.ID, out var arr)
                        || (i < arr.Length && arr[i]);
                    if (!paramOn) continue;

                    string val = device.ProcessedData[i];
                    if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                        ws.Cell(excelRow, col++).Value = d;
                    else
                        ws.Cell(excelRow, col++).Value = val;
                }
            }
            return excelRow + 1;
        }

        private static void StyleMergedHeader(IXLWorksheet ws, int row, int col, XLColor bg)
        {
            var cell = ws.Cell(row, col);
            cell.Style.Fill.BackgroundColor = bg;
            cell.Style.Font.Bold = true;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        private static void StyleDeviceHeader(IXLCell cell, XLColor bg)
        {
            var darker = XLColor.FromArgb(
                Math.Max(bg.Color.R - 30, 0),
                Math.Max(bg.Color.G - 30, 0),
                Math.Max(bg.Color.B - 30, 0));
            cell.Style.Fill.BackgroundColor = darker;
            cell.Style.Font.Bold = true;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        private static void StyleParamHeader(IXLCell cell, XLColor bg)
        {
            cell.Style.Fill.BackgroundColor = bg;
            cell.Style.Font.Bold = true;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Alignment.WrapText = true;
        }

        static double? GetCellNumber(IXLRow row, int col)
        {
            var cell = row.Cell(col);
            if (cell.IsEmpty()) return null;
            if (cell.TryGetValue(out double d) && !double.IsNaN(d)) return d;
            var s = cell.GetString()?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(s)) return null;
            return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed)
                ? parsed : null;
        }

        public static List<Device> LoadDevicesFromExcel(string excelPath, Action<string>? log = null)
        {
            var logger = log ?? Console.WriteLine;
            var dynamicDevices = new List<Device>();

            // 🟡 УЛУЧШЕНО: проверяем существование файла до открытия
            if (!File.Exists(excelPath))
                throw new FileNotFoundException($"Файл не найден: {excelPath}");

            try
            {
                using var workbook = new XLWorkbook(excelPath);
                var worksheet = workbook.Worksheet(1);
                var deviceGroups = new Dictionary<string, List<FieldInstruction>>();
                int rowCount = worksheet.LastRowUsed()?.RowNumber() ?? 0;

                for (int rowNum = 2; rowNum <= rowCount; rowNum++)
                {
                    var row = worksheet.Row(rowNum);
                    string deviceID = row.Cell(1).GetString().Trim();

                    if (string.IsNullOrWhiteSpace(deviceID))
                    {
                        logger($"Строка {rowNum}: отсутствует DeviceID — пропускаем.");
                        continue;
                    }

                    try
                    {
                        double? low = GetCellNumber(row, 4);
                        double? high = GetCellNumber(row, 5);
                        int? byteLow = low.HasValue ? (int?)low.Value : null;
                        int? byteHigh = high.HasValue ? (int?)high.Value : null;

                        if (!byteLow.HasValue && byteHigh.HasValue)
                        {
                            byteLow = byteHigh;
                            byteHigh = null;
                        }
                        if (!byteLow.HasValue)
                        {
                            logger($"Строка {rowNum}: не указан ByteLow — пропускаем.");
                            continue;
                        }

                        // 🔴 ИСПРАВЛЕНО: проверка что индексы байт в диапазоне 0-7
                        if (byteLow.Value < 0 || byteLow.Value > 7)
                        {
                            logger($"Строка {rowNum}: ByteLow={byteLow.Value} вне диапазона 0-7 — пропускаем.");
                            continue;
                        }
                        if (byteHigh.HasValue && (byteHigh.Value < 0 || byteHigh.Value > 7))
                        {
                            logger($"Строка {rowNum}: ByteHigh={byteHigh.Value} вне диапазона 0-7 — пропускаем.");
                            continue;
                        }

                        double? fieldIndexNum = GetCellNumber(row, 2);
                        int fieldIndex = fieldIndexNum.HasValue ? (int)fieldIndexNum.Value : 0;

                        // 🔴 ИСПРАВЛЕНО: проверка что FieldIndex не отрицательный
                        if (fieldIndex < 0)
                        {
                            logger($"Строка {rowNum}: FieldIndex={fieldIndex} отрицательный — пропускаем.");
                            continue;
                        }

                        double? startBitNum = GetCellNumber(row, 9);
                        double? lengthBitNum = GetCellNumber(row, 10);
                        int startBit = startBitNum.HasValue ? (int)startBitNum.Value : 0;
                        int lengthBit = lengthBitNum.HasValue ? (int)lengthBitNum.Value : 0;

                        string type = row.Cell(8).GetString().Trim();

                        // 🔴 ИСПРАВЛЕНО: для BIN проверяем что StartBit + LengthBit <= 8
                        if (type == "BIN" && startBit + lengthBit > 8)
                        {
                            logger($"Строка {rowNum}: StartBit({startBit})+LengthBit({lengthBit}) > 8 — пропускаем.");
                            continue;
                        }

                        double scale = GetCellNumber(row, 6) ?? 1;
                        double offset = GetCellNumber(row, 7) ?? 0;

                        var instruction = new FieldInstruction
                        {
                            FieldIndex = fieldIndex,
                            Header = row.Cell(3).GetString().Trim(),
                            ByteLow = byteLow.Value,
                            ByteHigh = byteHigh,
                            Scale = scale,
                            Offset = offset,
                            Type = type,
                            StartBit = startBit,
                            LenghtBit = lengthBit,
                        };

                        if (!deviceGroups.ContainsKey(deviceID))
                            deviceGroups[deviceID] = new List<FieldInstruction>();

                        deviceGroups[deviceID].Add(instruction);
                    }
                    catch (Exception ex)
                    {
                        logger($"Ошибка в строке {rowNum}: {ex.Message} — пропускаем.");
                    }
                }

                foreach (var kvp in deviceGroups)
                {
                    // Сортируем по FieldIndex, затем переназначаем индексы 0,1,2...
                    // Дублирующиеся FieldIndex допустимы — все параметры сохраняются
                    var sorted = kvp.Value.OrderBy(i => i.FieldIndex).ToList();
                    for (int i = 0; i < sorted.Count; i++)
                        sorted[i].FieldIndex = i;

                    dynamicDevices.Add(new DynamicDevice(kvp.Key, sorted));
                }

            }
            catch (Exception ex) when (ex is not FileNotFoundException)
            {
                throw new InvalidOperationException($"Ошибка загрузки устройств: {ex.Message}", ex);
            }

            return dynamicDevices;
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Используйте UI-версию приложения (logReader.UI).");
        }

    }
}
