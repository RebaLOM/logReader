namespace logReader
{
    // Кусок составного параметра: биты Byte источника SourceId (BitStart от LSB байта).
    public sealed record CompositePiece(string SourceId, int Byte, int BitStart, int BitLen);

    // Составной параметр: куски склеиваются в raw (Pieces[0] — старшие биты), затем scale/offset.
    // Значение фиксируется при приходе триггерной посылки (по умолчанию — источник последнего куска).
    public sealed class CompositeSignal
    {
        public string Block { get; set; } = CompositeDefaults.BlockName;
        public string Param { get; set; } = "";
        public List<CompositePiece> Pieces { get; set; } = new();
        public string TriggerId { get; set; } = "";
        public double Scale { get; set; } = 1.0;
        public double Offset { get; set; } = 0.0;
        public bool Signed { get; set; }
        public string Unit { get; set; } = "";
        public double? Min { get; set; }
        public double? Max { get; set; }

        public string ResolveDefaultTriggerId()
            => Pieces.Count > 0 ? Pieces[^1].SourceId : "";
    }

    public static class CompositeDefaults
    {
        public const string BlockName = "COMPOSITE";
    }
}
