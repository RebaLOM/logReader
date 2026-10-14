using logReader.Processing;

namespace logReader.Tests;

public sealed class DtcMalformedOutputTests
{
    [Theory]
    [InlineData("trc", false)]
    [InlineData("asc", false)]
    [InlineData("asc", true)]
    [InlineData("txt", false)]
    public void Corrupt_new_cycle_cannot_complete_previous_header_or_change_numeric_results(string format, bool relative)
    {
        using var dir = new TempDir();
        string input = WriteInput(dir, format, relative);
        string off = dir.File("off.csv"), on = dir.File("on.csv");
        var devices = DeviceFiles.LoadDevices(dir.Write("devices.dbc", TestData.TwoMessageDbc));
        var service = new LogProcessingService();
        Assert.True(service.ProcessSingleFile(input, off, devices, new OutputSettings(), new ProcessingContext()).Success);
        Assert.True(service.ProcessSingleFile(input, on, devices, new OutputSettings { IncludeDtc = true }, new ProcessingContext()).Success);
        string[][] rows = File.ReadAllLines(on).Select(l => l.Split(';')).ToArray();
        var codes = rows.Skip(1).Select(r => r[3]).Where(s => s.StartsWith("SPN ")).ToArray();
        Assert.NotEmpty(codes);
        Assert.All(codes, value => Assert.Equal("SPN 523055 / FMI 19", value));
        Assert.Contains(rows.Skip(1), r => r[3] == "");
        Assert.Equal(File.ReadAllLines(off).Skip(1), rows.Skip(1).Where(r => r[0] != "").Select(r => string.Join(';', r.Take(2))));
    }

    [Fact]
    public void Corrupt_trc_boundary_invalidates_merge_date_split_and_dst()
    {
        using var dir = new TempDir();
        string input = WriteInput(dir, "trc", false);
        var settings = new OutputSettings { IncludeDtc = true, DstConnect = new DstConnectOptions { BlockPeriodMs = 100, BlockStartIndex = 1 } };
        Assert.True(TrcBatchAggregator.TryBuildMergedAggregate([input], [], settings, new ProcessingContext(), out var merged).Success);
        AssertCodes(merged!.DtcRows!.Select(r => r.Snapshot));
        Assert.True(TrcBatchAggregator.TryBuildAggregatesByDate([input], [], settings, new ProcessingContext(), out var dates).Success);
        AssertCodes(dates.Values.SelectMany(d => d.DtcRows!.Select(r => r.Snapshot)));
        string output = dir.File("dst.csv");
        Assert.True(new DstConnectTrcProcessor().Process(input, [], output, settings, new ProcessingContext()).Success);
        string[] cells = File.ReadAllLines(output).Skip(1).Select(l => l.Split(';')[2]).ToArray();
        Assert.Contains("SPN 523055 / FMI 19", cells);
        Assert.All(cells.Where(s => s.StartsWith("SPN ")), s => Assert.Equal("SPN 523055 / FMI 19", s));
    }

    private static void AssertCodes(IEnumerable<DtcSnapshot> snapshots)
    {
        var codes = snapshots.SelectMany(s => s.Slots).Where(s => s.Code != null).Select(s => s.Code!.Value).ToArray();
        Assert.NotEmpty(codes);
        Assert.All(codes, code => Assert.Equal(new DtcCode(523055, 19), code));
    }

    private static string WriteInput(TempDir dir, string format, bool relative)
    {
        var old = LocarusDtcTests.Frames([new DtcCode(32770, 0)], 1);
        var fresh = LocarusDtcTests.Frames([new DtcCode(523055, 19)], 1);
        (int Ms, string Id, string Data)[] frames =
        [
            (0, old[0].Id, Hex(old[0].Bytes)),
            (100, fresh[0].Id, "00 00 00 01 00 00 2F ZZ"),
            (101, fresh[1].Id, Hex(fresh[1].Bytes)),
            (110, "0CFF0008", "11 00 00 00 00 00 00 00"),
            (200, fresh[0].Id, Hex(fresh[0].Bytes)),
            (201, fresh[1].Id, Hex(fresh[1].Bytes)),
            (210, "0CFF0008", "12 00 00 00 00 00 00 00")
        ];
        int previousMs = 0;
        string[] lines = frames.Select((f, i) =>
        {
            int offset = relative ? f.Ms - previousMs : f.Ms;
            previousMs = f.Ms;
            return format switch
            {
                "trc" => $"{i + 1}) {f.Ms} Rx {f.Id} 8 {f.Data}",
                "txt" => $"2026.10.14 12:00:00.{f.Ms:000} - {f.Id} 8 {f.Data}",
                _ => FormattableString.Invariant($"{offset / 1000d:F3} 1 {f.Id}x Rx d 8 {f.Data}")
            };
        }).ToArray();
        string header = format == "trc" ? "; Start time: 14.10.2026 12:00:00.000\n"
            : format == "asc" ? $"base hex timestamps {(relative ? "relative" : "absolute")}\n" : "";
        return dir.Write("input." + format, header + string.Join('\n', lines) + "\n");
    }

    private static string Hex(IEnumerable<int> data) => string.Join(' ', data.Select(b => b.ToString("X2")));
}
