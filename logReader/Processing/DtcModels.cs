namespace logReader.Processing;

public enum DtcDisplayFormat
{
    Compact,
    Detailed
}

internal readonly record struct DtcCode(int Spn, int Fmi)
{
    public override string ToString() => $"SPN {Spn} / FMI {Fmi}";

    public string ToDisplayString(DtcDisplayFormat format)
        => format == DtcDisplayFormat.Detailed
            ? FormattableString.Invariant($"SPN {Spn} / FMI {Fmi}")
            : FormattableString.Invariant($"{Spn}.{Fmi}");
}

internal readonly record struct DtcSlot(DtcCode? Code, bool IsKnown)
{
    public static DtcSlot Unknown => new(null, false);
    public static DtcSlot Empty => new(null, true);
    public static DtcSlot Present(DtcCode code) => new(code, true);
    public string Text => !IsKnown ? "Н/Д" : Code?.ToString() ?? "";
    public string ToDisplayString(DtcDisplayFormat format)
        => !IsKnown ? "Н/Д" : Code?.ToDisplayString(format) ?? "";
}

internal sealed record DtcSnapshot(double TimeMs, IReadOnlyList<DtcSlot> Slots,
    int? ReportedCount, bool IsComplete, string RawData)
{
    public static DtcSnapshot Unknown(double timeMs, string rawData = "")
        => new(timeMs, Enumerable.Repeat(DtcSlot.Unknown, 6).ToArray(), null, false, rawData);
}

internal sealed record DtcObservation(string SourceFile, string TimeText, DtcSnapshot Snapshot);
internal sealed record DtcEpisode(string SourceFile, DtcCode Code, string FirstSeen, string LastSeen,
    string? FirstAbsent, string EndReason);
internal sealed record DtcSummary(string SourceFile, DtcCode Code, string FirstSeen, string LastSeen,
    int IntervalCount, string State);
internal sealed record DtcAnalysisResult(IReadOnlyList<DtcObservation> Observations,
    IReadOnlyList<DtcEpisode> Episodes, IReadOnlyList<DtcSummary> Summaries, IReadOnlyList<string> Warnings);
