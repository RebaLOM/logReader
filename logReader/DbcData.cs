namespace logReader
{
    public sealed class DbcSignal
    {
        public string Name { get; set; } = "";
        public int StartBit { get; set; }
        public int Length { get; set; } = 8;
        public bool IsLittleEndian { get; set; } = true;
        public bool IsSigned { get; set; }
        public double Factor { get; set; } = 1.0;
        public double Offset { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }
        public string Unit { get; set; } = "";
        public string Receiver { get; set; } = "Vector__XXX";
    }

    public sealed class DbcMessage
    {
        public string Name { get; set; } = "";
        public uint Id { get; set; }
        public bool IsExtended { get; set; } = true;
        public int Dlc { get; set; } = 8;
        public string Transmitter { get; set; } = "Vector__XXX";
        public List<DbcSignal> Signals { get; set; } = new();

        public string IdHex => Id.ToString("X");
    }
}
