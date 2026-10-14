using ClosedXML.Excel;

namespace logReader.Processing;

// DTC is text beside the existing numeric parameters; it never enters Device.Values.
internal static class DtcOutput
{
    internal static readonly string[] Headers =
        ["DTC1", "DTC2", "DTC3", "DTC4", "DTC5", "DTC6"];

    internal static IEnumerable<string> Fields(DtcSnapshot? snapshot)
    {
        snapshot ??= DtcSnapshot.Unknown(0);
        for (int i = 0; i < 6; i++)
            yield return i < snapshot.Slots.Count && snapshot.Slots[i].IsKnown
                ? snapshot.Slots[i].Code?.ToString() ?? "" : "";
    }

    internal static void WriteHeaders(IXLWorksheet sheet, int startColumn, bool includeDeviceId, bool timeColumn)
    {
        int headerRow = ExcelLayoutBuilder.HeaderRowCount(includeDeviceId);
        int width = Headers.Length + (timeColumn ? 1 : 0);
        var color = ExcelLayoutBuilder.DeviceColors[0];
        if (includeDeviceId)
        {
            sheet.Range(1, startColumn, 1, startColumn + width - 1).Merge();
            sheet.Cell(1, startColumn).Value = "DTC";
            ExcelLayoutBuilder.ApplyDeviceIdHeaderStyle(sheet.Cell(1, startColumn), color);
        }
        var headers = timeColumn ? new[] { "Время DTC" }.Concat(Headers) : Headers;
        foreach (string header in headers)
        {
            sheet.Cell(headerRow, startColumn).Value = header;
            ExcelLayoutBuilder.ApplyParamHeaderStyle(sheet.Cell(headerRow, startColumn++), color);
        }
    }

    internal static void WriteFields(IXLWorksheet sheet, int row, int startColumn, DtcSnapshot? snapshot)
    {
        foreach (string text in Fields(snapshot))
        {
            // Explicit string assignment keeps SPN/FMI out of Excel's numeric/date conversion.
            sheet.Cell(row, startColumn++).Value = text;
        }
    }

    internal static double Milliseconds(double time, TimeAxisKind kind)
        => kind == TimeAxisKind.Milliseconds ? time : time * (TimeSpan.TicksPerDay / (double)TimeSpan.TicksPerMillisecond);
}

// One independent diagnostic time axis for each input file, without a second input pass.
internal sealed class DtcOutputTimeline
{
    private readonly LocarusDtcDecoder _decoder;
    private bool _knownState;
    private bool _started;
    internal List<(double Time, DtcSnapshot Snapshot)> Rows { get; } = new();

    internal DtcOutputTimeline(double timeoutMs)
    {
        _decoder = new LocarusDtcDecoder(timeoutMs);
    }

    internal void Observe(string id, ReadOnlySpan<int> payload, double time, TimeAxisKind kind)
    {
        double timeMs = DtcOutput.Milliseconds(time, kind);
        var current = _decoder.SnapshotAt(timeMs);
        bool known = current.ReportedCount != null;
        if (!_started || (_knownState && !known))
            Rows.Add((time, current));
        _started = true;
        _knownState = known;

        if (_decoder.OnFrame(id, payload, timeMs) is { } snapshot)
        {
            Rows.Add((time, snapshot));
            _knownState = snapshot.ReportedCount != null;
        }
    }

    internal DtcSnapshot CurrentAt(double time, TimeAxisKind kind)
        => _decoder.SnapshotAt(DtcOutput.Milliseconds(time, kind));
}

// Matrix columns and frames within a legacy CSV step form one logical snapshot.
// Feed the three protocol parts in their wire order even if columns are rearranged.
internal sealed class DtcRowCollector
{
    private readonly LocarusDtcDecoder _decoder;
    private readonly Dictionary<string, int[]> _parts = new(StringComparer.OrdinalIgnoreCase);
    private double _lastClockMs = double.NaN;
    private double _dayOffsetMs;
    internal DtcRowCollector(double timeoutMs)
    {
        _decoder = new LocarusDtcDecoder(timeoutMs);
    }
    internal void Add(string id, ReadOnlySpan<int> payload)
    {
        if (id is "18FE2503" or "18FE2603" or "18FE2703") _parts[id] = payload.ToArray();
    }
    internal DtcSnapshot Finish(double timeMs)
    {
        if (!double.IsNaN(_lastClockMs) && timeMs - _lastClockMs < -43200000d)
            _dayOffsetMs += 86400000d;
        _lastClockMs = timeMs;
        timeMs += _dayOffsetMs;
        // Pending wire fragments must not leak into the next logical row/step.
        // A completed last-known state can still be used until its timeout.
        _decoder.ResetCycle();
        foreach (string id in new[] { "18FE2503", "18FE2603", "18FE2703" })
            if (_parts.TryGetValue(id, out var payload)) _decoder.OnFrame(id, payload, timeMs);
        _parts.Clear();
        return _decoder.SnapshotAt(timeMs);
    }
}
