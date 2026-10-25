namespace logReader.UI;

internal enum DtcInclusionMode
{
    Auto,
    Always,
    Never
}

internal sealed record DisplaySettings
{
    public DtcDisplayFormat DtcFormat { get; init; } = DtcDisplayFormat.Compact;
    public DtcInclusionMode DtcInclusion { get; init; } = DtcInclusionMode.Auto;

    internal DisplaySettings Normalized()
        => this with
        {
            DtcFormat = Enum.IsDefined(DtcFormat) ? DtcFormat : DtcDisplayFormat.Compact,
            DtcInclusion = Enum.IsDefined(DtcInclusion) ? DtcInclusion : DtcInclusionMode.Auto
        };
}
