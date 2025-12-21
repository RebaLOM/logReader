using logReader;
using System;
using System.IO;

namespace logReader
{
    internal class Device
    {
        public string Id;
        public int[] RawBytes = new int[8];
        public string[] binarbytes = new string[8];
        public string[] ProcessedData = new string[8];

        public void GetBinaryString()
        {
            for (int i = 0; i < RawBytes.Length; i++)
            {
                binarbytes[i] = Convert.ToString(RawBytes[i], 2).PadLeft(8, '0');
            }
        }

        public virtual void Decode()
        {
        }
    }
    internal class Device_180128D0 : Device
    {
        public Device_180128D0()
        {
            Id = "180128D0";
        }
        public override void Decode()
        {
            // 1. Расчет Current maximum torque limit (Байты 2 и 3)
            // Согласно инструкции: Big-endian (Байт 2 - High, Байт 3 - Low)
            // Формула: Raw - 10000 (Resolution: 1 Nm/bit, Offset: -10000)
            int rawTorque = (RawBytes[2] * 256) + RawBytes[3];
            double physicalTorque = rawTorque - 10000;
            ProcessedData[0] = physicalTorque.ToString();

            // 2. Расчет Target speed (Байты 4 и 5)
            // Согласно инструкции: Big-endian (Байт 4 - High, Байт 5 - Low)
            // Формула: (Raw * 0.5) - 15000 (Resolution: 0.5 rpm/bit, Offset: -15000)
            int rawSpeed = (RawBytes[4] * 256) + RawBytes[5];
            double physicalSpeed = (rawSpeed * 0.5) - 15000;
            ProcessedData[1] = physicalSpeed.ToString();
        }
    }
    internal class Program
    {
        static void Main()
        {
            var devices = new Dictionary<string, Device>();
            string filepath = @"C:\Users\tv167\OneDrive\Рабочий стол\canlog.csv";

            int currentStep;
            string currentTime;

            using (StreamReader reader = new StreamReader(filepath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string[] parts = line.Split(';');
                    

                    currentStep = int.Parse(parts[0]);
                    currentTime = parts[1];
                    string currentId = parts[2];

                    // 1. Создание (Фабрика)
                    if (!devices.ContainsKey(currentId))
                    {
                        if (currentId == "180128D0")
                            devices.Add(currentId, new Device_180128D0());
                        else
                            devices.Add(currentId, new Device());
                    }

                    // 2. Обновление данных
                    Device currentDevice = devices[currentId];
                    for (int i = 0; i < 8; i++)
                    {
                        // Байты начинаются с 3-го индекса
                        currentDevice.RawBytes[i] = int.Parse(parts[i + 3]);
                    }

                    // 3. Расчет
                    currentDevice.Decode();

                    // 4. ВЫВОД: Здесь мы печатаем состояние ВСЕХ устройств на текущем шаге
                    PrintAllDevices(currentStep, currentTime, devices);
                }
            }
        }

        static void PrintAllDevices(int step, string time, Dictionary<string, Device> devices)
        {
            // Пока просто выведем в консоль для проверки
            Console.Write($"Step: {step} | Time: {time} ");
            foreach (var dev in devices.Values)
            {
                // Выводим ID и его обработанные данные (например, первый параметр)
                Console.Write($"| {dev.Id}: {dev.ProcessedData[0]} ");
            }
            Console.WriteLine();
        }
    }



}


     

