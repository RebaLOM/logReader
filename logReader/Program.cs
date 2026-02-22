<<<<<<< Updated upstream
<<<<<<< Updated upstream
﻿using System.Text;
=======
>>>>>>> Stashed changes
=======
>>>>>>> Stashed changes
using ClosedXML.Excel;


namespace logReader
{
    // БАЗОВЫЙ КЛАСС 
    public class Device
    {
        public string ID;
        public string[] headers;
        public int[] RawBytes = new int[8];
        public string[] RawBinaries = new string[8];
        public string[] ProcessedData;
<<<<<<< Updated upstream
<<<<<<< Updated upstream
        // устройства с которыми мы не работаем
        string[] skip = new string[] { "1803D0EF", "1FEE0110", "1FEE1001", "1FEEFF84", "1FEEFF86" };
=======
=======
>>>>>>> Stashed changes
      

>>>>>>> Stashed changes
        public void ToBinaries(int index)
        {
            RawBinaries[index] = Convert.ToString(RawBytes[index], 2).PadLeft(8, '0');
        }
        public Device(string ID, int headersCount)
        {
            this.ID = ID;
            headers = new string[headersCount];
            ProcessedData = new string[headersCount];
            // Инициализируем пустыми значениями, чтобы в Excel не было пустоты при первом запуске
            for (int i = 0; i < headersCount; i++) ProcessedData[i] = "0";
        }

        public virtual void Decode() { }
    }

<<<<<<< Updated upstream


=======
    // СТРУКТУРА ДЛЯ ХРАНЕНИЯ ИНСТРУКЦИИ ДЕКОДИРОВАНИЯ ПОЛЯ
    public class FieldInstruction
    {
        public int FieldIndex;          // Индекс поля (0, 1, 2...)
        public string Header = "";      // Название заголовка
        public int ByteLow;             // Младший байт (ОБЯЗАТЕЛЬНОЕ поле, 0-7)
        public int? ByteHigh;           // Старший байт (ОПЦИОНАЛЬНОЕ поле, может быть null)
        public double Scale;            // Множитель
        public double Offset;           // Смещение
        public string Type = "";        // Тип: NUM (числовое), BIN (бинарное)
        public int StartBit;            // Начальный бит (только для BIN)
        public int LenghtBit;           // Длина строки битов от StartBit
    }

    // ДИНАМИЧЕСКОЕ УСТРОЙСТВО, ЗАГРУЖЕННОЕ ИЗ ФАЙЛА
    public class DynamicDevice : Device
    {
        private List<FieldInstruction> instructions;

        public DynamicDevice(string deviceID, List<FieldInstruction> fieldInstructions)
            : base(deviceID, fieldInstructions.Count)
        {
            instructions = fieldInstructions;

            // Заполняем заголовки
            foreach (var instr in instructions)
            {
                headers[instr.FieldIndex] = instr.Header;
            }
        }

        public override void Decode()
        {
            foreach (var instr in instructions)
            {
                if (instr.Type == "NUM")
                {
                    // Числовая обработка
                    int rawValue;

                    if (instr.ByteHigh.HasValue)
                    {
                        // Двухбайтовое значение (big-endian)
                        rawValue = (RawBytes[instr.ByteHigh.Value] * 256) + RawBytes[instr.ByteLow];
                    }
                    else
                    {
                        // Однобайтовое значение
                        rawValue = RawBytes[instr.ByteLow];
                    }

                    // Применяем формулу: (raw * scale) + offset
                    double physicalValue = (rawValue * instr.Scale) + instr.Offset;
                    ProcessedData[instr.FieldIndex] = physicalValue.ToString();
                }
                else if (instr.Type == "BIN")
                {
                    // Бинарная обработка
                    ToBinaries(instr.ByteLow);
                    string BinarBits = RawBinaries[instr.ByteLow].Substring(instr.StartBit, instr.LenghtBit);
                    ProcessedData[instr.FieldIndex] = Convert.ToInt32(BinarBits, 2).ToString();
                }
            }
        }
    }

    // СТАТИЧЕСКИЕ УСТРОЙСТВА (существующие в коде)
>>>>>>> Stashed changes
    internal class Device_180128D0 : Device
    {
        public Device_180128D0() : base("180128D0", 2)
        {
            headers[0] = "Текущий максимальный предел крутящего момента";
            headers[1] = "Целевая скорость";

        }

        public override void Decode()
        {
            // Текущий максимальный предел крутящего момента
            int rawTorque = (RawBytes[3] * 256) + RawBytes[2];
            double physicalTorque = rawTorque - 10000;
            ProcessedData[0] = physicalTorque.ToString();

            // Целевая скорость
            int rawSpeed = (RawBytes[5] * 256) + RawBytes[4];
            double physicalSpeed = (rawSpeed * 0.5) - 15000;
            ProcessedData[1] = physicalSpeed.ToString();
        }
    }// Генератор

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
            // Напряжение шины
            int BusVoltage = (RawBytes[3] * 256) + RawBytes[2];
            ProcessedData[0] = BusVoltage.ToString();

            // Температура контроллера мотора
            int ControllerTemp = RawBytes[4] - 40;
            ProcessedData[1] = ControllerTemp.ToString();

            // Температура мотора
            int MotorTemp = RawBytes[5] - 40;
            ProcessedData[2] = MotorTemp.ToString();

            // Ток шины
            int BusCurrent = (RawBytes[7] * 256) + RawBytes[6];
            double physicalBusCurrent = (BusCurrent * 0.1) - 20000;
            ProcessedData[3] = physicalBusCurrent.ToString();
        }
    } // Генератор

    internal class Device_1802D0EF : Device // Генератор
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
            // Трехфазный выходной ток
            int ThreePhaseCurrent = (RawBytes[1] * 256) + RawBytes[0];
            double physicalThreePhaseCurrent = (ThreePhaseCurrent * 0.1);
            ProcessedData[0] = physicalThreePhaseCurrent.ToString();

            // Частота вращения двигателя / текущий крутящий момент двигателя
            int MotorTorque = (RawBytes[3] * 256) + RawBytes[2];
            double physicalMotorTorque = MotorTorque - 30000;
            ProcessedData[1] = physicalMotorTorque.ToString();

            // Текущая скорость вращения, фактический крутящий момент
            int ActualTorque = (RawBytes[5] * 256) + RawBytes[4] - 10000;
            ProcessedData[2] = ActualTorque.ToString();

            // Текущая скорость вращения, верхний предел крутящего момента
            int MaxTorque = (RawBytes[7] * 256) + RawBytes[6] - 10000;
            ProcessedData[3] = MaxTorque.ToString();
        }
    }// Генератор

    internal class Device_18FF0101 : Device // Генератор
    {
        public Device_18FF0101() : base("18FF0101", 2)
        {
            headers[0] = "Команда управления скоростью";
            headers[1] = "Команда управления крутящим моментом";
        } // Мотор

        public override void Decode()
        {
            // команда управления скоростью V2M1_DrvSpdConCmd
            int rawSpeed = (RawBytes[1] * 256) + RawBytes[0];
            double physicalSpeed = (rawSpeed * 0.5) - 15000;
            ProcessedData[0] = physicalSpeed.ToString();

            // Команда управления крутящим моментом V2M1_DrvTorqConCmd    
            int rawTorque = (RawBytes[3] * 256) + RawBytes[2];
            double physicalTorque = (rawTorque * 0.1) - 3200;
            ProcessedData[1] = physicalTorque.ToString();
        }
    }// Мотор

    internal class Device_18FF0201 : Device // Генератор
    {
        public Device_18FF0201() : base("18FF0201", 2)
        {
            headers[0] = "Команда управления скоростью";
            headers[1] = "Команда управления крутящим моментом";

        } // Мотор

        public override void Decode()
        {
            // команда управления скоростью V2M1_DrvSpdConCmd
            int rawSpeed = (RawBytes[1] * 256) + RawBytes[0];
            double physicalSpeed = (rawSpeed * 0.5) - 15000;
            ProcessedData[0] = physicalSpeed.ToString();

            // Команда управления крутящим моментом V2M1_DrvTorqConCmd    
            int rawTorque = (RawBytes[3] * 256) + RawBytes[2];
            double physicalTorque = (rawTorque * 0.1) - 3200;
            ProcessedData[1] = physicalTorque.ToString();
        }
    }// Мотор

    internal class Device_18FF31F1 : Device
    {
        public Device_18FF31F1() : base("18FF31F1", 3)
        {
            headers[0] = "Фактическая скорость вращения двигателя";
            headers[1] = "Фактический крутящий момент двигателя";
            headers[2] = "Максимальный выходной крутящий момент двигателя";
        } // Мотор

        public override void Decode()
        {
            // Фактическая частота вращения двигателя MCU1_DrvMotorActSpd
            int rawSpeed = (RawBytes[1] * 256) + RawBytes[0];
            double physicalSpeed = (rawSpeed * 0.5) - 15000;
            ProcessedData[0] = physicalSpeed.ToString();

            // Фактический крутящий момент двигателя mcu1_drvmotoractorque
            int rawTorque = (RawBytes[3] * 256) + RawBytes[2];
            double physicalTorque = (rawTorque * 0.1) - 3200;
            ProcessedData[1] = physicalTorque.ToString();

            // Максимальный выходной крутящий момент двигателя CAN MCU1_DrvMotorMaxAvilTorqPec
            double MaxTorque = (RawBytes[4] * 0.5);
            ProcessedData[2] = MaxTorque.ToString();
        }
    }// Мотор

    internal class Device_18FF32F1 : Device
    {
        public Device_18FF32F1() : base("18FF32F1", 4)
        {
            headers[0] = "Напряжение шины постоянного тока";
            headers[1] = "Ток шины постоянного тока";
            headers[2] = "Температура двигателя";
            headers[3] = "Температура преобразователя";
        } // Мотор

        public override void Decode()
        {
            // Напряжение на шине постоянного тока MCU2_DrvDcVoltage
            int DcVoltage = (RawBytes[1] * 256) + RawBytes[0];
            double physicalDcVoltage = DcVoltage * 0.2;
            ProcessedData[0] = physicalDcVoltage.ToString();

            // Ток в шине постоянного тока MCU2_DrvDcCurrent
            int DcCurrent = (RawBytes[4] * 256) + RawBytes[3];
            double physicalDcCurrent = (DcCurrent * 0.4) - 800;
            ProcessedData[1] = physicalDcCurrent.ToString();

            // температура двигателя mcu2_drvмоторная температура
            int MotorTemperature = RawBytes[6] - 40;
            ProcessedData[2] = MotorTemperature.ToString();

            //Температура преобразователя MCU2_DrvMCUTemperature
            int InverterTemperature = RawBytes[7] - 40;
            ProcessedData[3] = InverterTemperature.ToString();
        }

    }// Мотор

    internal class Device_18FF35F1 : Device
    {
        public Device_18FF35F1() : base("18FF35F1", 1)
        {
            headers[0] = "Сбои СУ двигателя";
        } // Мотор

        public override void Decode()
        {
            // Общее количество сбоев в работе системы управления двигателем MCU3_DrvCurMotSysFltNum
            ToBinaries(1);
            string SystemFault = RawBinaries[1];
<<<<<<< Updated upstream
<<<<<<< Updated upstream
            string faultBits = SystemFault.Substring(2, 6); // Биты 0-5
=======
            string faultBits = SystemFault.Substring(0, 6);
>>>>>>> Stashed changes
=======
            string faultBits = SystemFault.Substring(0, 6);
>>>>>>> Stashed changes
            ProcessedData[0] = Convert.ToInt32(faultBits, 2).ToString();
        }

    }// Ошибки

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
            // Фактическая частота вращения двигателя MCU1_DrvMotorActSpd
            int rawSpeed = (RawBytes[1] * 256) + RawBytes[0];
            double physicalSpeed = (rawSpeed * 0.5) - 15000;
            ProcessedData[0] = physicalSpeed.ToString();

            // Фактический крутящий момент двигателя mcu1_drvmotoractorque
            int rawTorque = (RawBytes[3] * 256) + RawBytes[2];
            double physicalTorque = (rawTorque * 0.1) - 3200;
            ProcessedData[1] = physicalTorque.ToString();

            // Максимальный выходной крутящий момент двигателя CAN MCU1_DrvMotorMaxAvilTorqPec
            double MaxTorque = (RawBytes[4] * 0.5);
            ProcessedData[2] = MaxTorque.ToString();
        }
    }// Мотор

    internal class Device_18FF42F1 : Device
    {
        public Device_18FF42F1() : base("18FF42F1", 4)
        {
            headers[0] = "Напряжение шины постоянного тока";
            headers[1] = "Ток шины постоянного тока";
            headers[2] = "Температура двигателя";
            headers[3] = "Температура преобразователя";
        } // Мотор

        public override void Decode()
        {
            // Напряжение на шине постоянного тока MCU2_DrvDcVoltage
            int DcVoltage = (RawBytes[1] * 256) + RawBytes[0];
            double physicalDcVoltage = DcVoltage * 0.2;
            ProcessedData[0] = physicalDcVoltage.ToString();

            // Ток в шине постоянного тока MCU2_DrvDcCurrent
            int DcCurrent = (RawBytes[4] * 256) + RawBytes[3];
            double physicalDcCurrent = (DcCurrent * 0.4) - 800;
            ProcessedData[1] = physicalDcCurrent.ToString();

            // температура двигателя mcu2_drvмоторная температура
            int MotorTemperature = RawBytes[6] - 40;
            ProcessedData[2] = MotorTemperature.ToString();

            //Температура преобразователя MCU2_DrvMCUTemperature
            int InverterTemperature = RawBytes[7] - 40;
            ProcessedData[3] = InverterTemperature.ToString();
        }

    }// Мотор

    internal class Device_18FF45F1 : Device
    {
        public Device_18FF45F1() : base("18FF45F1", 1)
        {
            headers[0] = "Сбои СУ двигателя";

        } // Мотор

        public override void Decode()
        {
            // Общее количество сбоев в работе системы управления двигателем MCU3_DrvCurMotSysFltNum
            ToBinaries(1);
            string SystemFault = RawBinaries[1];
<<<<<<< Updated upstream
<<<<<<< Updated upstream
            string faultBits = SystemFault.Substring(2, 6); // Биты 0-5
=======
            string faultBits = SystemFault.Substring(0, 6);
>>>>>>> Stashed changes
=======
            string faultBits = SystemFault.Substring(0, 6);
>>>>>>> Stashed changes
            ProcessedData[0] = Convert.ToInt32(faultBits, 2).ToString();
        }

    }// Ошибки

    internal class Device_1FEEFF85 : Device
    {
        public Device_1FEEFF85() : base("1FEEFF85", 5)
        {
            headers[0] = "Минимальное напряжение на блоке, мВ";
            headers[1] = "Максимальное напряжение на блоке, мВ";
            headers[2] = "Минимальная температура ячейки";
            headers[3] = "Максимальная температура ячейки";
            headers[4] = "Состояние заряда SOC, %";
        } // Батарея

        public override void Decode()
        {
            // Минимальное напряжение на блоке в мВ
            int MinCellVoltage = (RawBytes[0] * 256) + RawBytes[1];
            ProcessedData[0] = MinCellVoltage.ToString();
            // Максимальное напряжение на блоке в мВ
            int MaxCellVoltage = (RawBytes[2] * 256) + RawBytes[3];
            ProcessedData[1] = MaxCellVoltage.ToString();
            // Минимальная температура ячейки
            int MinCellTemperature = RawBytes[4] + 40;
            ProcessedData[2] = MinCellTemperature.ToString();
            // Максимальная температура ячейки
            int MaxCellTemperature = RawBytes[5] + 40;
            ProcessedData[3] = MaxCellTemperature.ToString();
            // Состояние заряда SOC в процентах
            int StateOfCharge = RawBytes[6];
            ProcessedData[4] = StateOfCharge.ToString();
        }

    } // Батарея

    internal class Device_1FEEFF87 : Device
    {
        public Device_1FEEFF87() : base("1FEEFF87", 4)
        {
            headers[0] = "Напряжение на входе контакторов, В";
            headers[1] = "Напряжение на выходе контакторов, В";
            headers[2] = "Напряжение батареи, В";
            headers[3] = "Дисбаланс батареи, мВ";


        } // Батарея

        public override void Decode()
        {
            //Напряжение на входе контакторов в В
            int PackVoltage = (RawBytes[1] * 256) + RawBytes[0];
            ProcessedData[0] = PackVoltage.ToString();
            // Напряжение на выходе контакторов в В
            int OutputVoltage = (RawBytes[3] * 256) + RawBytes[2];
            ProcessedData[1] = OutputVoltage.ToString();
            // Напряжение батареи, вычисленное суммированием напряжений ячеек в В
            int BatteryVoltage = (RawBytes[5] * 256) + RawBytes[4];
            ProcessedData[2] = BatteryVoltage.ToString();
            // Дисбаланс батареи в мВ
            int BatteryImbalance = (RawBytes[7] * 256) + RawBytes[6];
            ProcessedData[3] = BatteryImbalance.ToString();
        }

    } // батарея

    internal class Device_1FEEFF88 : Device
    {
        public Device_1FEEFF88() : base("1FEEFF88", 5)
        {
            headers[0] = "Температура охлаждающей жидкости на входе";
            headers[1] = "Температура охлаждающей жидкости на выходе";
            headers[2] = "Сопротивление изоляции (текущее)";
            headers[3] = "Сопротивление изоляции (выключено)";
            headers[4] = "Счетчик измерений сопротивления изоляции";

        } // Батарея

        public override void Decode()
        {
            // Температура охлаждающей жидкости на входе системы
            int CoolantTempIn = RawBytes[0] + 40;
            ProcessedData[0] = CoolantTempIn.ToString();
            // Температура охлаждающей жидкости на выходе системы
            int CoolantTempOut = RawBytes[1] + 40;
            ProcessedData[1] = CoolantTempOut.ToString();
            // Сопротивление изоляции (текущее)
            int InsulationResistance = (RawBytes[3] * 256) + RawBytes[2];
            ProcessedData[2] = InsulationResistance.ToString();
            // Сопротивление изоляции при выключенном состоянии
            int InsulationResistanceOff = (RawBytes[5] * 256) + RawBytes[4];
            ProcessedData[3] = InsulationResistanceOff.ToString();
            // Счетчик измерений сопротивления изоляции
            int InsulationResistanceCount = (RawBytes[7] * 256) + RawBytes[6];
            ProcessedData[4] = InsulationResistanceCount.ToString();
        }

<<<<<<< Updated upstream
    } // батарея





<<<<<<< Updated upstream
    internal class Program
    {      
       
        static int BuildExcelRow(
=======
    public class Program
    {
        public static int BuildExcelRow(
>>>>>>> Stashed changes
=======
    public class Program
    {
        public static int BuildExcelRow(
>>>>>>> Stashed changes
            IXLWorksheet ws,
            int excelRow,
            int step,
            string time,
            List<Device> devices)
        {
            return BuildExcelRow(ws, excelRow, step, time, devices, null, null);
        }

        public static int BuildExcelRow(
            IXLWorksheet ws,
            int excelRow,
            int step,
            string time,
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
                    bool paramOn = paramEnabled == null || !paramEnabled.TryGetValue(device.ID, out var arr)
                        ? true
                        : (i < arr.Length && arr[i]);
                    if (paramOn)
                        ws.Cell(excelRow, col++).Value = device.ProcessedData[i];
                }
            }
<<<<<<< Updated upstream
            return excelRow+1;
        }   
        
        static int BuildExcelHeaders(IXLWorksheet ws, List<Device> devices)
=======
            return excelRow + 1;
        }

        public static int BuildExcelHeaders(IXLWorksheet ws, List<Device> devices)
        {
            return BuildExcelHeaders(ws, devices, null, null);
        }

        public static int BuildExcelHeaders(IXLWorksheet ws, List<Device> devices,
            Dictionary<string, bool>? deviceEnabled,
            Dictionary<string, bool[]>? paramEnabled)
<<<<<<< Updated upstream
>>>>>>> Stashed changes
=======
>>>>>>> Stashed changes
        {
            int col = 1;
            ws.Cell(1, col++).Value = "Шаг";
            ws.Cell(1, col++).Value = "Время";

            foreach (var device in devices)
            {
                bool devOn = deviceEnabled == null || deviceEnabled.GetValueOrDefault(device.ID, true);
                if (!devOn) continue;

                for (int i = 0; i < device.headers.Length; i++)
                {
                    bool paramOn = paramEnabled == null || !paramEnabled.TryGetValue(device.ID, out var arr)
                        ? true
                        : (i < arr.Length && arr[i]);
                    if (paramOn)
                        ws.Cell(1, col++).Value = device.headers[i];
                }
            }
            return 2;
        }

<<<<<<< Updated upstream
<<<<<<< Updated upstream
=======
=======
>>>>>>> Stashed changes
        // Чтение числа из ячейки Excel
        static double? GetCellNumber(IXLRow row, int col)
        {
            var cell = row.Cell(col);
            if (cell.IsEmpty()) return null;

            if (cell.TryGetValue(out double d) && !double.IsNaN(d))
                return d;

            var s = cell.GetString()?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(s)) return null;

            return double.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double parsed)
                ? parsed
                : null;
        }

        // МЕТОД ДЛЯ ЗАГРУЗКИ УСТРОЙСТВ ИЗ EXCEL ФАЙЛА
        public static List<Device> LoadDevicesFromExcel(string excelPath, Action<string>? log = null)
        {
            var dynamicDevices = new List<Device>();

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
                        (log ?? (Action<string>)Console.WriteLine)($"Строка {rowNum}: отсутствует DeviceID.");
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
                            (log ?? (Action<string>)Console.WriteLine)($"Строка {rowNum}: не указан ByteLow и ByteHigh.");
                            continue;
                        }
                        double? BitStart = GetCellNumber(row, 9);
                        double? BitLenght = GetCellNumber(row, 10);

                        int? startBit = BitStart.HasValue ? (int)BitStart.Value : 0;
                        int? lengthBit = BitLenght.HasValue ? (int)BitLenght.Value : 0;

                        double? fieldIndexNum = GetCellNumber(row, 2);
                        int fieldIndex = fieldIndexNum.HasValue ? (int)fieldIndexNum.Value : 0;

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
                            Type = row.Cell(8).GetString().Trim(),
                            StartBit = startBit ?? 0,
                            LenghtBit = lengthBit ?? 0,
                        };

                        if (!deviceGroups.ContainsKey(deviceID))
                        {
                            deviceGroups[deviceID] = new List<FieldInstruction>();
                        }

                        deviceGroups[deviceID].Add(instruction);
                    }
                    catch (Exception ex)
                    {
                        (log ?? (Action<string>)Console.WriteLine)($"Ошибка в строке {rowNum}: {ex.Message}");
                        continue;
                    }
                }

                foreach (var kvp in deviceGroups)
                {
                    var sortedInstructions = kvp.Value.OrderBy(i => i.FieldIndex).ToList();
                    for (int i = 0; i < sortedInstructions.Count; i++)
                        sortedInstructions[i].FieldIndex = i;
                    dynamicDevices.Add(new DynamicDevice(kvp.Key, sortedInstructions));
                }

                Console.WriteLine($"Загружено {dynamicDevices.Count} устройств из файла.");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Ошибка загрузки устройств: {ex.Message}", ex);
            }

            return dynamicDevices;
        }

>>>>>>> Stashed changes
        static void Main(string[] args)
        {
            List<Device> devices = new List<Device>
            {
<<<<<<< Updated upstream
<<<<<<< Updated upstream
                new Device_180128D0(),
                new Device_1801D0EF(),
                new Device_1802D0EF(),
                new Device_18FF0101(),
                new Device_18FF0201(),
                new Device_18FF31F1(),
                new Device_18FF32F1(),
                new Device_18FF35F1(),
                new Device_18FF41F1(),
                new Device_18FF42F1(),
                new Device_18FF45F1(),
                new Device_1FEEFF85(),
                new Device_1FEEFF87(),
                new Device_1FEEFF88()
            };

            Dictionary<string, Device> deviceByID = devices.ToDictionary(d => d.ID);
=======
                /*  new Device_180128D0(),
                  new Device_1801D0EF(),
                  new Device_1802D0EF(),
                  new Device_18FF0101(),
                  new Device_18FF0201(),
                  new Device_18FF31F1(),
                  new Device_18FF35F1(),
                  new Device_18FF42F1(),
                  new Device_18FF45F1(),
                  new Device_1FEEFF85(),
                  new Device_1FEEFF87(),
                  new Device_1FEEFF88() */
            };
=======
                /*  new Device_180128D0(),
                  new Device_1801D0EF(),
                  new Device_1802D0EF(),
                  new Device_18FF0101(),
                  new Device_18FF0201(),
                  new Device_18FF31F1(),
                  new Device_18FF35F1(),
                  new Device_18FF42F1(),
                  new Device_18FF45F1(),
                  new Device_1FEEFF85(),
                  new Device_1FEEFF87(),
                  new Device_1FEEFF88() */
            };
>>>>>>> Stashed changes
            Console.WriteLine("=== Программа чтения CAN логов ===\n");
            Console.WriteLine($"Базовых устройств загружено: {devices.Count}");

            // ПРЕДЛОЖЕНИЕ ЗАГРУЗИТЬ ДОПОЛНИТЕЛЬНЫЕ УСТРОЙСТВА
            Console.Write("\nЖелаете загрузить устройства из Excel файла? (да/нет) (по умолчанию: да): ");
            string answer = Console.ReadLine()?.Trim().ToLower() ?? "";

            if (answer == "да" || answer == "")
            {
                Console.Write("Введите полный путь к Excel файлу с устройствами: ");
                string excelPath = Console.ReadLine()?.Trim() ?? "";

                if (!string.IsNullOrWhiteSpace(excelPath) && File.Exists(excelPath))
                {
                    var additionalDevices = LoadDevicesFromExcel(excelPath);
                    devices.AddRange(additionalDevices);
                }
                else
                {
                    Console.WriteLine(" Файл не найден или путь некорректен. Продолжаем с базовыми устройствами.");
                }
            }

            Console.WriteLine($"\nВсего устройств для обработки: {devices.Count}\n");

            var deviceByID = new Dictionary<string, Device>();
            foreach (var d in devices)
                deviceByID[d.ID] = d;
>>>>>>> Stashed changes

            int currentStep = 0;
            string currentTime = "";
            bool firstStep = true;

            Console.WriteLine("Программа запущена");

            string[] lines = File.ReadAllLines(@"C:\Users\tv167\OneDrive\Рабочий стол\canlog.csv");
            string outputPath = @"C:\Users\tv167\OneDrive\Рабочий стол\result.xlsx";

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Log");

            int excelRow = BuildExcelHeaders(ws,devices);

            foreach (string line in lines)
            {
        
                string[] parts = line.Split(';');

                int priority;
                int.TryParse(parts[3], out priority);

                // ДЕКОД УСТРОЙСТВ 
                string? deviceKey = parts.Length > 2 ? parts[2] : null;
                if (priority == 1 && deviceKey != null && deviceByID.TryGetValue(deviceKey, out Device? currentDevice))
                {
                    for (int i = 0; i < 8; i++)
                        currentDevice.RawBytes[i] = Convert.ToInt32(parts[4 + i], 10);

                    currentDevice.Decode();
                }

                // ОБРАБОТКА ШАГОВ
                if (parts[0] != "") 
                {
                    int newStep = int.Parse(parts[0]);
                    string newTime = parts[1];

                    if (!firstStep) 
                    {
                        // Записываем результат ПРЕДЫДУЩЕГО шага
                        excelRow = BuildExcelRow(ws, excelRow, currentStep, currentTime, devices);
                    }

                    // обновляем текущие значения шага
                    currentStep = newStep;
                    currentTime = newTime;

                    firstStep = false;
                }
            }

            // ДОЗАПИСЫВАЕМ ПОСЛЕДНИЙ ШАГ
            excelRow = BuildExcelRow(ws, excelRow, currentStep, currentTime, devices);
            // Сохраняем файл .xlsx
            workbook.SaveAs(outputPath);
        }

    }
}
          