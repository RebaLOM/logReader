using System.Globalization;

namespace logReader
{
    // Разбор строк BUSMASTER .dbf — общая семантика с DBC, другой синтаксис.
    internal static class DbfLineParser
    {
        public readonly struct MessageHeader
        {
            public string Name { get; init; }
            public uint Id { get; init; }
            public int Dlc { get; init; }
            public int SignalCount { get; init; }
            public bool IsExtended { get; init; }
            public string FrameType { get; init; }
        }

        public readonly struct SignalFields
        {
            public string Name { get; init; }
            public int Length { get; init; }
            public int ByteIndex { get; init; }
            public int BitOffset { get; init; }
            public string Type { get; init; }
            public long RawMax { get; init; }
            public long RawMin { get; init; }
            public bool IsLittleEndian { get; init; }
            public double Offset { get; init; }
            public double Scale { get; init; }
            public string Unit { get; init; }
        }

        public static bool TryParseStartMsg(string line, out MessageHeader header)
        {
            header = default;
            if (!line.StartsWith("[START_MSG]", StringComparison.OrdinalIgnoreCase))
                return false;

            string payload = line.Substring("[START_MSG]".Length).Trim();
            if (payload.Length == 0) return false;

            string[] parts = payload.Split(',');
            if (parts.Length < 6) return false;

            if (!uint.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out uint id))
                return false;
            if (!int.TryParse(parts[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int dlc))
                return false;
            if (!int.TryParse(parts[3].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int sigCount))
                return false;
            if (!int.TryParse(parts[4].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int extFlag))
                return false;

            header = new MessageHeader
            {
                Name = parts[0].Trim(),
                Id = id,
                Dlc = dlc,
                SignalCount = sigCount,
                IsExtended = extFlag == 1,
                FrameType = parts[5].Trim()
            };
            return true;
        }

        public static bool TryParseStartSignals(string line, out SignalFields fields)
        {
            fields = default;
            if (!line.StartsWith("[START_SIGNALS]", StringComparison.OrdinalIgnoreCase))
                return false;

            string payload = line.Substring("[START_SIGNALS]".Length).Trim();
            if (payload.Length == 0) return false;

            string[] parts = payload.Split(',');
            if (parts.Length < 11) return false;

            if (!int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int length))
                return false;
            if (!int.TryParse(parts[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int byteIndex))
                return false;
            if (!int.TryParse(parts[3].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int bitOffset))
                return false;
            if (!long.TryParse(parts[5].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long rawMax))
                return false;
            if (!long.TryParse(parts[6].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long rawMin))
                return false;
            if (!int.TryParse(parts[7].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int byteOrderFlag))
                return false;

            fields = new SignalFields
            {
                Name = parts[0].Trim(),
                Length = length,
                ByteIndex = byteIndex,
                BitOffset = bitOffset,
                Type = parts[4].Trim().ToUpperInvariant(),
                RawMax = rawMax,
                RawMin = rawMin,
                IsLittleEndian = byteOrderFlag != 0,
                Offset = NumberParseHelper.ParseDoubleInvariant(parts[8].Trim()),
                Scale = NumberParseHelper.ParseDoubleInvariant(parts[9].Trim()),
                Unit = parts[10].Trim()
            };
            return true;
        }

        public static DbcMessage ToDbcMessage(MessageHeader header)
        {
            return new DbcMessage
            {
                Name = header.Name,
                Id = header.Id,
                IsExtended = header.IsExtended,
                Dlc = header.Dlc
            };
        }

        public static DbcSignal ToDbcSignal(SignalFields fields)
        {
            bool isSigned = fields.Type == "I";
            var (physMin, physMax) = DbcPhysicalValue.PhysicalBoundsFromRaw(
                fields.RawMin, fields.RawMax, fields.Scale, fields.Offset);

            return new DbcSignal
            {
                Name = fields.Name,
                StartBit = (fields.ByteIndex - 1) * 8 + fields.BitOffset,
                Length = fields.Length,
                IsLittleEndian = fields.IsLittleEndian,
                IsSigned = isSigned,
                Factor = fields.Scale,
                Offset = fields.Offset,
                Min = physMin,
                Max = physMax,
                Unit = fields.Unit ?? ""
            };
        }

        public static string FormatStartMsg(DbcMessage message)
        {
            string name = string.IsNullOrWhiteSpace(message.Name)
                ? "Msg_" + message.Id.ToString("X", CultureInfo.InvariantCulture)
                : message.Name;

            return $"[START_MSG] {name},{message.Id},{message.Dlc},{message.Signals.Count},{(message.IsExtended ? 1 : 0)},X,";
        }

        public static string FormatStartSignals(DbcSignal signal)
        {
            string type = signal.IsSigned ? "I" : (signal.Length == 1 ? "B" : "U");
            ComputeRawBounds(signal, out long rawMin, out long rawMax);

            int byteIndex = signal.StartBit / 8 + 1;
            int bitOffset = signal.StartBit % 8;

            return string.Join(",",
                signal.Name,
                signal.Length.ToString(CultureInfo.InvariantCulture),
                byteIndex.ToString(CultureInfo.InvariantCulture),
                bitOffset.ToString(CultureInfo.InvariantCulture),
                type,
                rawMax.ToString(CultureInfo.InvariantCulture),
                rawMin.ToString(CultureInfo.InvariantCulture),
                signal.IsLittleEndian ? "1" : "0",
                FormatDbfNumber(signal.Offset),
                FormatDbfNumber(signal.Factor),
                signal.Unit ?? "",
                "",
                "") + ",";
        }

        private static void ComputeRawBounds(DbcSignal signal, out long rawMin, out long rawMax)
        {
            if (signal.Factor != 0
                && !double.IsNaN(signal.Min) && !double.IsInfinity(signal.Min)
                && !double.IsNaN(signal.Max) && !double.IsInfinity(signal.Max))
            {
                rawMin = (long)Math.Round((signal.Min - signal.Offset) / signal.Factor, MidpointRounding.AwayFromZero);
                rawMax = (long)Math.Round((signal.Max - signal.Offset) / signal.Factor, MidpointRounding.AwayFromZero);
                if (rawMin > rawMax)
                    (rawMin, rawMax) = (rawMax, rawMin);
                return;
            }

            BitMath.ComputeRawRange(signal.Length, signal.IsSigned, out rawMin, out rawMax);
        }

        private static string FormatDbfNumber(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return value.ToString(CultureInfo.InvariantCulture);

            double r = DbcPhysicalValue.RoundPhysical(value);
            if (Math.Abs(r - Math.Round(r)) < 1e-9)
                return ((long)Math.Round(r)).ToString(CultureInfo.InvariantCulture);
            return r.ToString("0.######", CultureInfo.InvariantCulture);
        }
    }
}
