namespace logReader.Processing
{
    public readonly record struct ProcessingProgress(string? Stage, double Fraction);

    // Журнал, прогресс и отмена для длительных операций; дочерний контекст отображает
    // прогресс своей части (например, одного файла пакета) в общий диапазон.
    public sealed class ProcessingContext
    {
        private readonly Action<string>? _log;
        private readonly IProgress<ProcessingProgress>? _progress;
        private readonly double _from;
        private readonly double _span;
        private readonly string? _stage;

        public ProcessingContext(
            Action<string>? log = null,
            IProgress<ProcessingProgress>? progress = null,
            CancellationToken cancellationToken = default)
            : this(log, progress, cancellationToken, 0, 1, null)
        {
        }

        private ProcessingContext(
            Action<string>? log,
            IProgress<ProcessingProgress>? progress,
            CancellationToken cancellationToken,
            double from,
            double span,
            string? stage)
        {
            _log = log;
            _progress = progress;
            CancellationToken = cancellationToken;
            _from = from;
            _span = span;
            _stage = stage;
        }

        public CancellationToken CancellationToken { get; }

        public void Log(string message) => _log?.Invoke(message);

        public ProcessingResult Fail(string error)
        {
            Log(error);
            return ProcessingResult.Fail(error);
        }

        public void ThrowIfCancellationRequested() => CancellationToken.ThrowIfCancellationRequested();

        public void ReportProgress(double fraction)
            => _progress?.Report(new ProcessingProgress(_stage, _from + _span * Math.Clamp(fraction, 0, 1)));

        // Часть [from, from + span] текущего диапазона прогресса.
        public ProcessingContext Slice(double from, double span, string? stage = null)
            => new(_log, _progress, CancellationToken, _from + _span * from, _span * span, stage ?? _stage);
    }

    public sealed record ProcessingResult(bool Success, string? OutputPath, long RowsWritten, string? Error)
    {
        public static ProcessingResult Ok(string outputPath, long rows) => new(true, outputPath, rows, null);

        public static ProcessingResult Fail(string error) => new(false, null, 0, error);
    }

    public sealed class OutputSettings
    {
        public OutputFormat Format { get; init; } = OutputFormat.Csv;
        public OutputFilter Filter { get; init; } = OutputFilter.All;
        public CompositeRuntime? Composites { get; init; }
        public bool IncludeDeviceIdHeaderRow { get; init; }
        public DstConnectOptions DstConnect { get; init; } = new();

        public bool HasComposites => Composites != null && !Composites.IsEmpty;
    }
}
