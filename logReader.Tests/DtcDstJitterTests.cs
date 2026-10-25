using System.Globalization;
using System.Text;
using logReader.Processing;

namespace logReader.Tests;

public class DtcDstJitterTests
{
    private static readonly int[][] Parts =
        [[0, 0, 0, 1, 0, 0, 2, 128], new int[8], new int[8]];
    private static readonly string[] Ids = ["18FE2503", "18FE2603", "18FE2703"];

    [Fact]
    public void Projected_complete_state_does_not_advance_wire_clock_or_hide_real_backwards_time()
    {
        var decoder = new LocarusDtcDecoder();
        for (int part = 0; part < 3; part++) decoder.OnFrame(Ids[part], Parts[part], 14 + part);
        var projection = decoder.ProjectSnapshotAt(20);
        Assert.Equal(20, projection.TimeMs);
        Assert.True(projection.IsComplete);
        Assert.Equal(1, projection.ReportedCount);
        Assert.Equal("SPN 32770 / FMI 0", projection.Slots[0].Text);

        decoder.OnFrame("0CFF0008", new int[8], 17);
        Assert.True(decoder.SnapshotAt(17).IsComplete);
        decoder.OnFrame("0CFF0008", new int[8], 15);
        Assert.Null(decoder.SnapshotAt(15).ReportedCount);
    }

    [Fact]
    public void Expired_projection_does_not_discard_pending_parts_at_the_actual_wire_time()
    {
        var decoder = new LocarusDtcDecoder(10);
        decoder.OnFrame(Ids[0], Parts[0], 0);
        decoder.OnFrame(Ids[1], Parts[1], 1);
        Assert.Equal("SPN 32770 / FMI 0", decoder.ProjectSnapshotAt(11).Slots[0].Text);
        var expired = decoder.ProjectSnapshotAt(11.001);
        Assert.Null(expired.ReportedCount);
        Assert.All(expired.Slots, slot => Assert.False(slot.IsKnown));

        var completed = decoder.OnFrame(Ids[2], Parts[2], 2);
        Assert.NotNull(completed);
        Assert.True(completed.IsComplete);
        Assert.Equal("SPN 32770 / FMI 0", completed.Slots[0].Text);
        decoder.OnFrame("0CFF0008", new int[8], 12.001);
        Assert.Null(decoder.SnapshotAt(12.001).ReportedCount);
    }

    [Fact]
    public void Projection_preserves_incomplete_count_quality()
    {
        var decoder = new LocarusDtcDecoder();
        var header = Parts[0].ToArray();
        header[3] = 7;
        decoder.OnFrame(Ids[0], header, 0);
        decoder.OnFrame(Ids[1], Parts[1], 1);
        decoder.OnFrame(Ids[2], Parts[2], 2);
        var projection = decoder.ProjectSnapshotAt(20);
        Assert.Equal(7, projection.ReportedCount);
        Assert.False(projection.IsComplete);
        Assert.Equal("SPN 32770 / FMI 0", projection.Slots[0].Text);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-1)]
    public void Invalid_or_earlier_projection_does_not_invalidate_the_current_wire_state(double time)
    {
        var decoder = new LocarusDtcDecoder();
        for (int part = 0; part < 3; part++) decoder.OnFrame(Ids[part], Parts[part], part);
        Assert.Null(decoder.ProjectSnapshotAt(time).ReportedCount);
        Assert.True(decoder.SnapshotAt(2).IsComplete);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Fallback_row_at_twenty_ms_does_not_reset_live_frames_at_seventeen_ms(bool splitCycle)
    {
        using var dir = new TempDir();
        var frames = new List<(int MessageIndex, double TimeMs, string Id)>();
        var text = new StringBuilder();
        int cycleStart = splitCycle ? 16 : 14;
        for (int ms = 0; ms <= 25; ms++)
        {
            int part = ms - cycleStart;
            string id = part is >= 0 and < 3 ? Ids[part] : "0CFF0008";
            int[] bytes = part is >= 0 and < 3 ? Parts[part] : new int[8];
            frames.Add((ms + 1, ms, id));
            string hex = string.Join(' ', bytes.Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));
            text.AppendLine(FormattableString.Invariant($"{ms + 1}) {ms:F3} Rx {id} 8 {hex}"));
        }

        // No reference recurrence or inter-block pauses: force the actual fallback
        // grid, whose 3 ms tolerance starts slot 1 at 17 ms but stamps slot 0 at 20.
        var detection = TrcBlockDetector.Detect(frames, 0, 20, new HashSet<string>());
        Assert.True(detection.UsedGapFallback);
        Assert.Equal(0, detection.BlockOriginTimeMs);
        Assert.Equal(1, TrcBlockDetector.ComputeSlot(17, 0, 20, detection.JitterToleranceMs));

        string input = dir.Write("jitter.trc", text.ToString());
        string output = dir.File("jitter.csv");
        var result = new DstConnectTrcProcessor().Process(input, [], output,
            new OutputSettings
            {
                Format = OutputFormat.CsvDstConnect, IncludeDtc = true, DtcDisplayFormat = DtcDisplayFormat.Detailed,
                DstConnect = new DstConnectOptions { BlockPeriodMs = 20 }
            }, new ProcessingContext());
        Assert.True(result.Success, result.Error);
        var rows = File.ReadAllLines(output).Select(line => line.Split(';')).ToArray();
        Assert.Equal(3, rows.Length);
        Assert.Equal("20", rows[1][1]);
        Assert.Equal("40", rows[2][1]);
        Assert.Equal(splitCycle ? "" : "SPN 32770 / FMI 0", rows[1][2]);
        Assert.Equal("SPN 32770 / FMI 0", rows[2][2]);
        Assert.All(rows, row => Assert.Equal(8, row.Length));
    }

    [Theory]
    [InlineData(19, false)]
    [InlineData(19, true)]
    [InlineData(25, false)]
    [InlineData(25, true)]
    public void Fallback_last_jitter_block_keeps_numeric_and_dtc_values_at_end_of_file(int lastTimeMs, bool includeDtc)
    {
        using var dir = new TempDir();
        var devices = DeviceFiles.LoadDevices(dir.Write("devices.dbc", TestData.TwoMessageDbc));
        var text = new StringBuilder();
        for (int ms = 0; ms <= lastTimeMs; ms++)
        {
            int part = ms - 17;
            string id = part is >= 0 and < 3 ? Ids[part] : "0CFF0008";
            int[] bytes = part is >= 0 and < 3 ? Parts[part] : [ms, 0, 0, 0, 0, 0, 0, 0];
            string hex = string.Join(' ', bytes.Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));
            text.AppendLine(FormattableString.Invariant($"{ms + 1}) {ms:F3} Rx {id} 8 {hex}"));
        }
        // A distinct numeric value in the final slot makes a lost row visible
        // even with diagnostic output disabled. The DTC cycle completes at 19 ms.
        text.AppendLine(FormattableString.Invariant($"{lastTimeMs + 2}) {lastTimeMs:F3} Rx 0CFF0008 8 63 00 00 00 00 00 00 00"));
        string input = dir.Write("jitter-eof.trc", text.ToString());
        string output = dir.File("jitter-eof.csv");

        var result = new DstConnectTrcProcessor().Process(input, devices, output,
            new OutputSettings
            {
                Format = OutputFormat.CsvDstConnect, IncludeDtc = includeDtc, AutoIncludeDtc = false,
                DtcDisplayFormat = DtcDisplayFormat.Detailed,
                DstConnect = new DstConnectOptions { BlockPeriodMs = 20 }
            }, new ProcessingContext());

        Assert.True(result.Success, result.Error);
        var rows = File.ReadAllLines(output).Select(line => line.Split(';')).ToArray();
        Assert.Equal(3, rows.Length);
        Assert.Equal("20", rows[1][1]);
        Assert.Equal("40", rows[2][1]);
        Assert.Equal("16", rows[1][2]);
        Assert.Equal("99", rows[2][2]);
        if (includeDtc)
        {
            Assert.Equal("", rows[1][3]);
            Assert.Equal("SPN 32770 / FMI 0", rows[2][3]);
        }
        Assert.All(rows, row => Assert.Equal(includeDtc ? 9 : 3, row.Length));
    }
}
