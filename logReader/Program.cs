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
        public string[] headers;
        public int[] RawBytes = new int[8];
        public string[] ProcessedData;

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

    public class Program
    {
        public static int BuildExcelHeaders(
            IXLWorksheet ws, List<Device> devices,
            Dictionary<string, bool>? deviceEnabled,
            Dictionary<string, bool[]>? paramEnabled,
            bool includeDeviceIdRow = false)
            => ExcelLayoutBuilder.BuildStepLogHeaders(ws, devices, deviceEnabled, paramEnabled, includeDeviceIdRow);

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
            if (extension.Equals(".dbf", StringComparison.OrdinalIgnoreCase))
                return DbcDevicesLoader.LoadDevicesFromDbf(path, log);

            throw new NotSupportedException($"Поддерживаются только файлы .xlsx, .dbc и .dbf: {path}");
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

                if (d.ProcessedData != null)
                {
                    for (int i = 0; i < d.ProcessedData.Length; i++)
                        d.ProcessedData[i] = "0";
                }
            }
        }
    }
}
