using System.Globalization;

namespace logReader
{
    // Посылка с набором параметров. Values[i] — физическое значение (NaN — значения ещё нет),
    // FieldErrors[i] — описание поля не применимо к кадру (выводится как «ERR»).
    public class Device
    {
        public const int MaxDataLength = 64;

        public string ID { get; }
        public string[] Headers { get; }
        public int[] RawBytes { get; } = new int[MaxDataLength];
        public double[] Values { get; }
        public bool[] FieldErrors { get; }

        protected Device(string id, int headersCount)
        {
            ID = id;
            Headers = new string[headersCount];
            for (int i = 0; i < headersCount; i++) Headers[i] = "";
            Values = new double[headersCount];
            FieldErrors = new bool[headersCount];
            Array.Fill(Values, double.NaN);
        }

        public virtual void Decode() { }

        public void SetPayload(ReadOnlySpan<int> bytes)
        {
            int n = Math.Min(bytes.Length, MaxDataLength);
            bytes[..n].CopyTo(RawBytes);
            Array.Clear(RawBytes, n, MaxDataLength - n);
        }

        public virtual void ResetState()
        {
            Array.Clear(RawBytes);
            Array.Fill(Values, double.NaN);
            Array.Clear(FieldErrors);
        }

        public string FormatValue(int index)
            => FieldErrors[index] ? "ERR" : ValueFormatter.FormatInvariant(Values[index]);
    }

    public static class ValueFormatter
    {
        public static string FormatInvariant(double value)
            => double.IsNaN(value) ? "" : value.ToString(CultureInfo.InvariantCulture);
    }

    public enum SignalValueType
    {
        Integer,
        Float32,
        Float64
    }

    public class FieldInstruction
    {
        public int FieldIndex;
        public string Header = "";
        public string Type = "";
        public int ByteLow;
        public int StartBit;
        public int LengthBit;
        public bool IsLittleEndian = true;
        public bool SignedRaw;
        public double Scale = 1;
        public double Offset;
        public double Min;
        public double Max;
        public string Unit = "";
        public SignalValueType ValueType = SignalValueType.Integer;

        // DBC-мультиплексирование: значение сигнала обновляется, только когда мультиплексор равен MuxValue.
        public bool IsMultiplexor;
        public int? MuxValue;
    }

    public class DynamicDevice : Device
    {
        private readonly List<FieldInstruction> _instructions;
        private readonly FieldInstruction? _multiplexor;

        public IReadOnlyList<FieldInstruction> Instructions => _instructions;

        public DynamicDevice(string deviceID, List<FieldInstruction> fieldInstructions)
            : base(deviceID, fieldInstructions.Count)
        {
            _instructions = fieldInstructions;
            foreach (var instr in _instructions)
                Headers[instr.FieldIndex] = instr.Header;
            _multiplexor = _instructions.FirstOrDefault(i => i.IsMultiplexor);
        }

        public override void Decode()
        {
            ulong low = 0;
            for (int i = 0; i < 8; i++)
                low |= (ulong)(byte)RawBytes[i] << (8 * i);

            long? muxValue = null;
            if (_multiplexor != null
                && BitExtractor.TryExtract(RawBytes, low, _multiplexor.StartBit, _multiplexor.LengthBit,
                    _multiplexor.IsLittleEndian, out ulong muxRaw))
            {
                muxValue = (long)muxRaw;
            }

            foreach (var instr in _instructions)
            {
                int idx = instr.FieldIndex;
                if (idx < 0 || idx >= Values.Length) continue;

                if (instr.MuxValue.HasValue && muxValue != instr.MuxValue.Value)
                    continue;

                if (instr.Type == "BIN")
                    DecodeBin(instr, idx);
                else
                    DecodeNum(instr, low, idx);
            }
        }

        private void DecodeNum(FieldInstruction instr, ulong low, int idx)
        {
            if (!BitExtractor.TryExtract(RawBytes, low, instr.StartBit, instr.LengthBit, instr.IsLittleEndian, out ulong raw))
            {
                SetError(idx);
                return;
            }

            double rawNumeric;
            switch (instr.ValueType)
            {
                case SignalValueType.Float32 when instr.LengthBit == 32:
                    rawNumeric = BitConverter.Int32BitsToSingle(unchecked((int)(uint)raw));
                    break;
                case SignalValueType.Float64 when instr.LengthBit == 64:
                    rawNumeric = BitConverter.Int64BitsToDouble(unchecked((long)raw));
                    break;
                default:
                    rawNumeric = instr.SignedRaw
                        ? BitExtractor.SignExtend(raw, instr.LengthBit)
                        : raw;
                    break;
            }

            Values[idx] = rawNumeric * instr.Scale + instr.Offset;
            FieldErrors[idx] = false;
        }

        private void DecodeBin(FieldInstruction instr, int idx)
        {
            if (instr.ByteLow < 0 || instr.ByteLow >= RawBytes.Length
                || instr.StartBit < 0 || instr.LengthBit <= 0
                || instr.StartBit + instr.LengthBit > 8)
            {
                SetError(idx);
                return;
            }

            int b = RawBytes[instr.ByteLow] & 0xFF;
            int mask = (1 << instr.LengthBit) - 1;
            // StartBit для BIN — LSB поля внутри байта (как BitStart в xlsx и Composite).
            Values[idx] = (b >> instr.StartBit) & mask;
            FieldErrors[idx] = false;
        }

        private void SetError(int idx)
        {
            Values[idx] = double.NaN;
            FieldErrors[idx] = true;
        }
    }

    // Извлечение сигнала из полезной нагрузки до 64 байт (Intel / Motorola, как в DBC).
    public static class BitExtractor
    {
        public const int MaxPayloadBits = Device.MaxDataLength * 8;

        public static bool TryExtract(int[] bytes, ulong firstEightBytes, int startBit, int length, bool littleEndian, out ulong raw)
        {
            raw = 0;
            if (length <= 0 || length > 64 || startBit < 0)
                return false;

            if (littleEndian)
            {
                if (startBit + length > MaxPayloadBits) return false;

                if (startBit + length <= 64)
                {
                    raw = length == 64
                        ? firstEightBytes >> startBit
                        : (firstEightBytes >> startBit) & ((1UL << length) - 1);
                    return true;
                }

                for (int k = 0; k < length; k++)
                {
                    int bit = startBit + k;
                    raw |= (ulong)(uint)((bytes[bit >> 3] >> (bit & 7)) & 1) << k;
                }
                return true;
            }

            // Motorola: startBit — MSB; внутри байта вниз, затем MSB следующего байта (+15).
            int current = startBit;
            for (int k = 0; k < length; k++)
            {
                if (current < 0 || current >= MaxPayloadBits) return false;
                raw = (raw << 1) | (ulong)(uint)((bytes[current >> 3] >> (current & 7)) & 1);
                current = (current % 8 == 0) ? current + 15 : current - 1;
            }
            return true;
        }

        public static long SignExtend(ulong raw, int length)
        {
            if (length >= 64) return unchecked((long)raw);
            ulong signBit = 1UL << (length - 1);
            if ((raw & signBit) != 0)
                raw |= ~((1UL << length) - 1);
            return unchecked((long)raw);
        }
    }
}
