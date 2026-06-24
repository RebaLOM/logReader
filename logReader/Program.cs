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
        public string Name = "Unknown";
        public int DLC = 8;
        public bool IsExtendedId = false;
        public string[] headers;
        public int[] RawBytes = new int[8];
        public string[] RawBinaries = new string[8];
        public string[] ProcessedData;

        public void ToBinaries(int index)
        {
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
        public int LengthBit;
        public bool UseBitExtraction;
        public bool IsLittleEndian = true;
        public bool SignedRaw;
        public double Min;
        public double Max;
        public string Unit = "";
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
            ulong payload = BuildPayload();

            foreach (var instr in instructions)
            {
                if (instr.Type == "NUM")
                {
                    DecodeNum(instr, payload);
                }
                else if (instr.Type == "BIN")
                {
                    DecodeBin(instr);
                }
            }
        }

        private void DecodeNum(FieldInstruction instr, ulong payload)
        {
            if (instr.FieldIndex < 0 || instr.FieldIndex >= ProcessedData.Length) return;

            double rawNumericValue;
            if (instr.UseBitExtraction)
            {
                if (!TryExtractBitField(instr, payload, out long bitFieldValue))
                {
                    ProcessedData[instr.FieldIndex] = "ERR";
                    return;
                }
                rawNumericValue = bitFieldValue;
            }
            else
            {
                if (instr.ByteLow < 0 || instr.ByteLow >= RawBytes.Length)
                {
                    ProcessedData[instr.FieldIndex] = "ERR";
                    return;
                }

                long raw;
                int bits;
                if (instr.ByteHigh.HasValue)
                {
                    int hi = instr.ByteHigh.Value;
                    if (hi < 0 || hi >= RawBytes.Length)
                    {
                        ProcessedData[instr.FieldIndex] = "ERR";
                        return;
                    }
                    raw = (RawBytes[hi] * 256) + RawBytes[instr.ByteLow];
                    bits = 16;
                }
                else
                {
                    raw = RawBytes[instr.ByteLow];
                    bits = 8;
                }

                if (instr.SignedRaw && bits < 64)
                {
                    long signBit = 1L << (bits - 1);
                    if ((raw & signBit) != 0) raw -= (1L << bits);
                }
                rawNumericValue = raw;
            }

            double physicalValue = (rawNumericValue * instr.Scale) + instr.Offset;
            ProcessedData[instr.FieldIndex] = physicalValue.ToString(CultureInfo.InvariantCulture);
        }

        private void DecodeBin(FieldInstruction instr)
        {
            if (instr.FieldIndex < 0 || instr.FieldIndex >= ProcessedData.Length) return;

            if (instr.ByteLow < 0 || instr.ByteLow >= RawBytes.Length
                || instr.StartBit < 0 || instr.LengthBit <= 0
                || instr.StartBit + instr.LengthBit > 8)
            {
                ProcessedData[instr.FieldIndex] = "ERR";
                return;
            }

            int b = RawBytes[instr.ByteLow] & 0xFF;
            int mask = (1 << instr.LengthBit) - 1;
            // StartBit для BIN — LSB поля внутри байта (как BitStart в xlsx и Composite).
            int raw = (b >> instr.StartBit) & mask;
            ProcessedData[instr.FieldIndex] = raw.ToString(CultureInfo.InvariantCulture);
        }

        private bool TryExtractBitField(FieldInstruction instr, ulong payload, out long value)
        {
            value = 0;

            if (instr.LengthBit <= 0 || instr.LengthBit > 64 || instr.StartBit < 0)
                return false;

            ulong rawValue;

            if (instr.IsLittleEndian)
            {
                if (instr.StartBit + instr.LengthBit > 64) return false;

                rawValue = instr.LengthBit == 64
                    ? payload >> instr.StartBit
                    : (payload >> instr.StartBit) & ((1UL << instr.LengthBit) - 1);
            }
            else
            {
                // Motorola: StartBit — MSB; внутри байта вниз, затем MSB следующего байта.
                if (!TryReadMotorolaField(payload, instr.StartBit, instr.LengthBit, out rawValue))
                    return false;
            }

            if (!instr.SignedRaw)
            {
                value = (long)rawValue;
                return true;
            }

            if (instr.LengthBit == 64)
            {
                value = unchecked((long)rawValue);
                return true;
            }

            ulong signBit = 1UL << (instr.LengthBit - 1);
            if ((rawValue & signBit) != 0)
            {
                ulong extensionMask = ~((1UL << instr.LengthBit) - 1);
                rawValue |= extensionMask;
            }

            value = unchecked((long)rawValue);
            return true;
        }

        private ulong BuildPayload()
        {
            ulong payload = 0;
            for (int i = 0; i < RawBytes.Length; i++)
                payload |= ((ulong)(byte)RawBytes[i]) << (8 * i);
            return payload;
        }

        // Motorola: startBit — MSB; далее −1 по байту, на границе байта +15.
        private static bool TryReadMotorolaField(ulong payload, int startBit, int length, out ulong result)
        {
            result = 0;
            int bit = startBit;
            for (int i = 0; i < length; i++)
            {
                if (bit < 0 || bit >= 64) return false;

                ulong b = (payload >> bit) & 1UL;
                result = (result << 1) | b;

                if ((bit % 8) == 0)
                    bit += 15;
                else
                    bit -= 1;
            }
            return true;
        }
    }

    // Жёстко закодированные устройства (legacy CSV-описания).
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
            ProcessedData[0] = (rawTorque - 10000).ToString(CultureInfo.InvariantCulture);
            int rawSpeed = (RawBytes[5] * 256) + RawBytes[4];
            ProcessedData[1] = ((rawSpeed * 0.5) - 15000).ToString(CultureInfo.InvariantCulture);
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
            ProcessedData[0] = ((RawBytes[3] * 256) + RawBytes[2]).ToString(CultureInfo.InvariantCulture);
            ProcessedData[1] = (RawBytes[4] - 40).ToString(CultureInfo.InvariantCulture);
            ProcessedData[2] = (RawBytes[5] - 40).ToString(CultureInfo.InvariantCulture);
            double busCurrent = ((RawBytes[7] * 256) + RawBytes[6]) * 0.1 - 20000;
            ProcessedData[3] = busCurrent.ToString(CultureInfo.InvariantCulture);
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
            ProcessedData[0] = (((RawBytes[1] * 256) + RawBytes[0]) * 0.1).ToString(CultureInfo.InvariantCulture);
            ProcessedData[1] = ((RawBytes[3] * 256) + RawBytes[2] - 30000).ToString(CultureInfo.InvariantCulture);
            ProcessedData[2] = ((RawBytes[5] * 256) + RawBytes[4] - 10000).ToString(CultureInfo.InvariantCulture);
            ProcessedData[3] = ((RawBytes[7] * 256) + RawBytes[6] - 10000).ToString(CultureInfo.InvariantCulture);
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
            ProcessedData[0] = (((RawBytes[1] * 256) + RawBytes[0]) * 0.5 - 15000).ToString(CultureInfo.InvariantCulture);
            ProcessedData[1] = (((RawBytes[3] * 256) + RawBytes[2]) * 0.1 - 3200).ToString(CultureInfo.InvariantCulture);
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
            ProcessedData[0] = (((RawBytes[1] * 256) + RawBytes[0]) * 0.5 - 15000).ToString(CultureInfo.InvariantCulture);
            ProcessedData[1] = (((RawBytes[3] * 256) + RawBytes[2]) * 0.1 - 3200).ToString(CultureInfo.InvariantCulture);
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
            ProcessedData[0] = (((RawBytes[1] * 256) + RawBytes[0]) * 0.5 - 15000).ToString(CultureInfo.InvariantCulture);
            ProcessedData[1] = (((RawBytes[3] * 256) + RawBytes[2]) * 0.1 - 3200).ToString(CultureInfo.InvariantCulture);
            ProcessedData[2] = (RawBytes[4] * 0.5).ToString(CultureInfo.InvariantCulture);
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
            ProcessedData[0] = (((RawBytes[1] * 256) + RawBytes[0]) * 0.2).ToString(CultureInfo.InvariantCulture);
            ProcessedData[1] = (((RawBytes[4] * 256) + RawBytes[3]) * 0.4 - 800).ToString(CultureInfo.InvariantCulture);
            ProcessedData[2] = (RawBytes[6] - 40).ToString(CultureInfo.InvariantCulture);
            ProcessedData[3] = (RawBytes[7] - 40).ToString(CultureInfo.InvariantCulture);
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
            string bin = RawBinaries[1] ?? "00000000";
            if (bin.Length >= 6)
                ProcessedData[0] = Convert.ToInt32(bin.Substring(0, 6), 2).ToString(CultureInfo.InvariantCulture);
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
            ProcessedData[0] = (((RawBytes[1] * 256) + RawBytes[0]) * 0.5 - 15000).ToString(CultureInfo.InvariantCulture);
            ProcessedData[1] = (((RawBytes[3] * 256) + RawBytes[2]) * 0.1 - 3200).ToString(CultureInfo.InvariantCulture);
            ProcessedData[2] = (RawBytes[4] * 0.5).ToString(CultureInfo.InvariantCulture);
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
            ProcessedData[0] = (((RawBytes[1] * 256) + RawBytes[0]) * 0.2).ToString(CultureInfo.InvariantCulture);
            ProcessedData[1] = (((RawBytes[4] * 256) + RawBytes[3]) * 0.4 - 800).ToString(CultureInfo.InvariantCulture);
            ProcessedData[2] = (RawBytes[6] - 40).ToString(CultureInfo.InvariantCulture);
            ProcessedData[3] = (RawBytes[7] - 40).ToString(CultureInfo.InvariantCulture);
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
                ProcessedData[0] = Convert.ToInt32(bin.Substring(0, 6), 2).ToString(CultureInfo.InvariantCulture);
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
            ProcessedData[0] = ((RawBytes[0] * 256) + RawBytes[1]).ToString(CultureInfo.InvariantCulture);
            ProcessedData[1] = ((RawBytes[2] * 256) + RawBytes[3]).ToString(CultureInfo.InvariantCulture);
            ProcessedData[2] = (RawBytes[4] - 40).ToString(CultureInfo.InvariantCulture);
            ProcessedData[3] = (RawBytes[5] - 40).ToString(CultureInfo.InvariantCulture);
            ProcessedData[4] = RawBytes[6].ToString(CultureInfo.InvariantCulture);
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
            ProcessedData[0] = ((RawBytes[1] * 256) + RawBytes[0]).ToString(CultureInfo.InvariantCulture);
            ProcessedData[1] = ((RawBytes[3] * 256) + RawBytes[2]).ToString(CultureInfo.InvariantCulture);
            ProcessedData[2] = ((RawBytes[5] * 256) + RawBytes[4]).ToString(CultureInfo.InvariantCulture);
            ProcessedData[3] = ((RawBytes[7] * 256) + RawBytes[6]).ToString(CultureInfo.InvariantCulture);
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
            
            ProcessedData[0] = (RawBytes[0] + 40).ToString(CultureInfo.InvariantCulture);
            ProcessedData[1] = (RawBytes[1] + 40).ToString(CultureInfo.InvariantCulture);
            ProcessedData[2] = ((RawBytes[3] * 256) + RawBytes[2]).ToString(CultureInfo.InvariantCulture);
            ProcessedData[3] = ((RawBytes[5] * 256) + RawBytes[4]).ToString(CultureInfo.InvariantCulture);
            ProcessedData[4] = ((RawBytes[7] * 256) + RawBytes[6]).ToString(CultureInfo.InvariantCulture);
        }
    }

    public class Program
    {
        public static readonly XLColor[] DeviceColors = ExcelLayoutBuilder.DeviceColors;

        public static int BuildExcelHeaders(
            IXLWorksheet ws, List<Device> devices)
            => BuildExcelHeaders(ws, devices, null, null);

        public static int BuildExcelHeaders(
            IXLWorksheet ws, List<Device> devices,
            Dictionary<string, bool>? deviceEnabled,
            Dictionary<string, bool[]>? paramEnabled)
            => ExcelLayoutBuilder.BuildStepLogHeaders(ws, devices, deviceEnabled, paramEnabled);

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

        public static List<Device> LoadDevicesFromExcel(string excelPath, Action<string>? log = null)
        {
            var logger = log ?? Console.WriteLine;
            var dynamicDevices = new List<Device>();

            if (!File.Exists(excelPath))
                throw new FileNotFoundException($"Файл не найден: {excelPath}");

            try
            {
                var definitions = DeviceExcelFile.ReadAllDevices(excelPath, logger);
                foreach (var def in definitions)
                {
                    var instructions = new List<FieldInstruction>();

                    foreach (var row in def.Rows)
                    {
                        if (string.IsNullOrWhiteSpace(row.Header))
                        {
                            logger($"Устройство {def.DeviceId}: пустой Header — пропускаем.");
                            continue;
                        }

                        string type = (row.Type ?? "").Trim().ToUpperInvariant();

                        if (type == "NUM")
                        {
                            if (row.Length <= 0 || row.Length > 64)
                            {
                                logger($"Устройство {def.DeviceId}, '{row.Header}': Length вне диапазона 1..64 — пропускаем.");
                                continue;
                            }

                            if (row.StartBit < 0 || row.StartBit + row.Length > 64)
                            {
                                logger($"Устройство {def.DeviceId}, '{row.Header}': StartBit/Length выходят за пределы 64 бит полезной нагрузки — пропускаем.");
                                continue;
                            }

                            instructions.Add(new FieldInstruction
                            {
                                FieldIndex = row.FieldIndex,
                                Header = row.Header,
                                Type = "NUM",
                                UseBitExtraction = true,
                                StartBit = row.StartBit,
                                LengthBit = row.Length,
                                Scale = row.Scale,
                                Offset = row.Offset,
                                IsLittleEndian = row.IsLittleEndian,
                                SignedRaw = row.SignedRaw,
                                Unit = row.Unit ?? "",
                                Min = row.MinPhys ?? 0,
                                Max = row.MaxPhys ?? 0,
                            });
                        }
                        else if (type == "BIN")
                        {
                            int lowByte = row.StartBit;
                            if (lowByte < 0 || lowByte > 7)
                            {
                                logger($"Устройство {def.DeviceId}, '{row.Header}': для BIN колонка StartBit (байт данных) должна быть 0..7 — пропускаем.");
                                continue;
                            }

                            int bitStart = row.BitStart ?? 0;
                            int len = row.Length;
                            if (len <= 0 || len > 8)
                            {
                                logger($"Устройство {def.DeviceId}, '{row.Header}': для BIN Length должен быть 1..8 — пропускаем.");
                                continue;
                            }

                            if (bitStart + len > 8)
                            {
                                logger($"Устройство {def.DeviceId}, '{row.Header}': для BIN BitStart+Length не должны превышать 8 — пропускаем.");
                                continue;
                            }

                            instructions.Add(new FieldInstruction
                            {
                                FieldIndex = row.FieldIndex,
                                Header = row.Header,
                                Type = "BIN",
                                ByteLow = lowByte,
                                ByteHigh = null,
                                Scale = 1,
                                Offset = 0,
                                UseBitExtraction = false,
                                StartBit = bitStart,
                                LengthBit = len,
                                IsLittleEndian = true,
                                SignedRaw = false,
                            });
                        }
                        else
                        {
                            logger($"Устройство {def.DeviceId}, '{row.Header}': неизвестный Type '{type}' — пропускаем.");
                        }
                    }

                    var sorted = instructions.OrderBy(i => i.FieldIndex).ToList();
                    for (int i = 0; i < sorted.Count; i++)
                        sorted[i].FieldIndex = i;

                    if (sorted.Count > 0)
                        dynamicDevices.Add(new DynamicDevice(def.DeviceId, sorted));
                }
            }
            catch (Exception ex) when (ex is not FileNotFoundException)
            {
                throw new InvalidOperationException($"Ошибка загрузки устройств: {ex.Message}", ex);
            }

            return dynamicDevices;
        }

        public static List<Device> LoadDevicesFromFile(string path, Action<string>? log = null)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Путь к файлу устройств не задан.", nameof(path));

            string extension = Path.GetExtension(path);
            if (extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                return LoadDevicesFromExcel(path, log);
            if (extension.Equals(".dbc", StringComparison.OrdinalIgnoreCase))
                return DbcDevicesLoader.LoadDevicesFromDbc(path, log);

            throw new NotSupportedException($"Поддерживаются только файлы .xlsx и .dbc: {path}");
        }

        public static CompositeRuntime LoadCompositesFromFile(string? path, Action<string>? log = null)
        {
            var logger = log ?? Console.WriteLine;

            if (string.IsNullOrWhiteSpace(path))
                return CompositeRuntime.Build(Array.Empty<CompositeSignal>());

            string extension = Path.GetExtension(path);
            if (!extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException($"Файл составных параметров должен быть .xlsx: {path}");

            var signals = CompositeExcelFile.ReadAll(path, logger);
            return CompositeRuntime.Build(signals);
        }

        public static void ResetDevicesState(IEnumerable<Device> devices)
        {
            if (devices == null) return;

            foreach (var d in devices)
            {
                if (d == null) continue;

                if (d.RawBytes != null)
                {
                    for (int i = 0; i < d.RawBytes.Length; i++)
                        d.RawBytes[i] = 0;
                }

                if (d.RawBinaries != null)
                {
                    for (int i = 0; i < d.RawBinaries.Length; i++)
                        d.RawBinaries[i] = "";
                }

                if (d.ProcessedData != null)
                {
                    for (int i = 0; i < d.ProcessedData.Length; i++)
                        d.ProcessedData[i] = "0";
                }
            }
        }
    }
}
