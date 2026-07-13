namespace logReader
{
    public sealed class DstConnectOptions
    {
        public const double MinBlockCoverage = 0.7;

        // Период блока посылок и интервал фиксации строк CSV (одно значение).
        public int BlockPeriodMs { get; set; } = 20;
        // 0 = авто; иначе Message Number из .trc (1), 2), …).
        public int BlockStartIndex { get; set; }
        // 0 = авто: max(0.5, BlockPeriodMs * 0.15).
        public int JitterToleranceMs { get; set; }
    }
}
