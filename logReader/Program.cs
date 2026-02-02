using System.Text;
using ClosedXML.Excel;


namespace logReader
{
    // БАЗОВЫЙ КЛАСС 
    internal class Device
    {
        public string ID;
        public string[] headers;
        public int[] RawBytes = new int[8];
        public string[] RawBinaries = new string[8];
        public string[] ProcessedData;
        // устройства с которыми мы не работаем
        string[] skip = new string[] { "1803D0EF", "1FEE0110", "1FEE1001", "1FEEFF84", "1FEEFF86" };
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
            string faultBits = SystemFault.Substring(2, 6); // Биты 0-5
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
            string faultBits = SystemFault.Substring(2, 6); // Биты 0-5
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

    } // батарея





    internal class Program
    {      
       
        static int BuildExcelRow(
            IXLWorksheet ws,
            int excelRow,
            int step,
            string time,
            List<Device> devices)
        {
            int col = 1;
            ws.Cell(excelRow, col++).Value = step;
            ws.Cell(excelRow, col++).Value = time;
            foreach (var device in devices)
            {
                foreach (var data in device.ProcessedData)
                {
                    ws.Cell(excelRow, col++).Value = data;
                }
            }
            return excelRow+1;
        }   
        
        static int BuildExcelHeaders(IXLWorksheet ws, List<Device> devices)
        {
            int col = 1;
            ws.Cell(1, col++).Value = "Шаг";
            ws.Cell(1, col++).Value = "Время";

            foreach (var device in devices)
            {
                foreach (var header in device.headers)
                {
                    ws.Cell(1, col++).Value = header;
                }
            }
            return 2;
        }

        static void Main(string[] args)
        {
            List<Device> devices = new List<Device>
            {
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
                if (priority == 1 && deviceByID.TryGetValue(parts[2], out Device currentDevice))
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
          