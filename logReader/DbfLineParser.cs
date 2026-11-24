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
            public DbfMessageDataFormat DataFormat { get; init; }
            public bool IsExtended { get; init; }
            public string Transmitter { get; init; }
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
            if (!int.TryParse(parts[3].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                return false;
            string dataFormat = parts[4].Trim();
            if (dataFormat is not ("0" or "1"))
                return false;
            string frameType = parts[5].Trim().ToUpperInvariant();
            if (frameType is not ("S" or "X"))
                return false;

            header = new MessageHeader
            {
                Name = parts[0].Trim(),
                Id = id,
                Dlc = dlc,
                DataFormat = dataFormat == "1" ? DbfMessageDataFormat.LittleEndian : DbfMessageDataFormat.BigEndian,
                IsExtended = frameType == "X",
                Transmitter = parts.Length > 6 ? parts[6].Trim() : ""
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
            if (!NumberParseHelper.TryParseDouble(parts[8], out double offset)
                || !NumberParseHelper.TryParseDouble(parts[9], out double scale))
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
                Offset = offset,
                Scale = scale,
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
                Dlc = header.Dlc,
                DbfDataFormat = header.DataFormat,
                Transmitter = header.Transmitter
            };
        }

        public static DbcSignal? ToDbcSignal(SignalFields fields, int dlc)
        {
            int payloadBytes = Math.Clamp(dlc, 0, Device.MaxDataLength);
            if (fields.ByteIndex < 1 || fields.ByteIndex > payloadBytes
                || fields.BitOffset is < 0 or > 7 || fields.Length is < 1 or > 64)
                return null;

            int startBit = (fields.ByteIndex - 1) * 8 + fields.BitOffset;
            if (!fields.IsLittleEndian)
            {
                // BUSMASTER stores the Motorola LSB. Walk towards the MSB in wire order.
                int msbPosition = (startBit ^ 7) - (fields.Length - 1);
                if (msbPosition < 0) return null;
                startBit = msbPosition ^ 7;
            }

            if (!BitMath.SignalFitsInDlc(startBit, fields.Length, fields.IsLittleEndian, payloadBytes * 8))
                return null;

            bool isSigned = fields.Type == "I";
            var (physMin, physMax) = DbcPhysicalValue.PhysicalBoundsFromRaw(
                fields.RawMin, fields.RawMax, fields.Scale, fields.Offset);

            return new DbcSignal
            {
                Name = fields.Name,
                StartBit = startBit,
                Length = fields.Length,
                IsLittleEndian = fields.IsLittleEndian,
                IsSigned = isSigned,
                Factor = fields.Scale,
                Offset = fields.Offset,
                Min = physMin,
                Max = physMax,
                Unit = fields.Unit ?? "",
                OriginName = fields.Name
            };
        }

        public static string FormatStartMsg(DbcMessage message, int signalCount)
        {
            string name = string.IsNullOrWhiteSpace(message.Name)
                ? "Msg_" + message.Id.ToString("X", CultureInfo.InvariantCulture)
                : message.Name;

            int dataFormat = message.DbfDataFormat == DbfMessageDataFormat.BigEndian ? 0 : 1;
            string frameType = message.IsExtended ? "X" : "S";
            string transmitter = message.Transmitter == "Vector__XXX" ? "" : message.Transmitter;
            return $"[START_MSG] {name},{message.Id},{message.Dlc},{signalCount},{dataFormat},{frameType},{transmitter}";
        }

        public static string FormatStartSignals(DbcSignal signal)
        {
            string type = signal.IsSigned ? "I" : (signal.Length == 1 ? "B" : "U");
            ComputeRawBounds(signal, out long rawMin, out long rawMax);

            int startBit = signal.IsLittleEndian
                ? signal.StartBit
                : ((signal.StartBit ^ 7) + signal.Length - 1) ^ 7;
            int byteIndex = startBit / 8 + 1;
            int bitOffset = startBit % 8;

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
