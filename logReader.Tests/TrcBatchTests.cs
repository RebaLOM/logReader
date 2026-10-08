using logReader.Processing;

namespace logReader.Tests;

public class TrcBatchTests
{
    private static string Trc(string? startTime, params (double Ms, int Value)[] frames)
    {
        var lines = new List<string>();
        if (startTime != null) lines.Add($";   Start time: {startTime}");
        int n = 1;
        foreach (var (ms, value) in frames)
            lines.Add(FormattableString.Invariant($"{n++,6})  {ms,10:F1}  Rx     0CFF0008  8  {value:X2} 00 00 00 00 00 00 00"));
        return string.Join("\n", lines) + "\n";
    }

    private static (string Input, string Output, List<Device> Devices, string Dbc) Setup(TempDir dir)
    {
        string input = Directory.CreateDirectory(Path.Combine(dir.Path, "logs")).FullName;
        string output = Directory.CreateDirectory(Path.Combine(dir.Path, "out")).FullName;
        string dbc = dir.Write("devices.dbc", TestData.TwoMessageDbc);
        return (input, output, DeviceFiles.LoadDevices(dbc), dbc);
    }

    private static string[] RunBatch(string input, string output, List<Device> devices, string dbc, BatchOutputMode mode, string resultName)
    {
        var files = Directory.GetFiles(input).OrderBy(f => f).ToList();
        var outcome = new LogProcessingService().ProcessFolderBatch(files, output, dbc, mode, devices,
            new OutputSettings { Format = OutputFormat.Csv }, new ProcessingContext());
        Assert.True(outcome.Created >= 1);
        return File.ReadAllLines(Path.Combine(output, resultName));
    }

    [Fact]
    public void Merged_trc_uses_absolute_time_ordered_by_start_time()
    {
        using var dir = new TempDir();
        var (input, output, devices, dbc) = Setup(dir);
        File.WriteAllText(Path.Combine(input, "a.trc"), Trc("26.04.2023 11:00:00.000.0", (10, 3), (20, 4)));
        File.WriteAllText(Path.Combine(input, "b.trc"), Trc("26.04.2023 10:00:00.000.0", (10, 1), (20, 2)));

        var lines = RunBatch(input, output, devices, dbc, BatchOutputMode.MergeToSingleFile, "result_trc_merged.csv");

        Assert.Equal(new[]
        {
            "Время;A1",
            "2023-04-26 10:00:00.010;1",
            "2023-04-26 10:00:00.020;2",
            "2023-04-26 11:00:00.010;3",
            "2023-04-26 11:00:00.020;4",
        }, lines);
    }

    [Fact]
    public void Merged_trc_without_start_time_is_sequential_not_reset_to_zero()
    {
        using var dir = new TempDir();
        var (input, output, devices, dbc) = Setup(dir);
        File.WriteAllText(Path.Combine(input, "a.trc"), Trc(null, (10, 1), (20, 2)));
        File.WriteAllText(Path.Combine(input, "b.trc"), Trc(null, (10, 3), (20, 4)));

        var lines = RunBatch(input, output, devices, dbc, BatchOutputMode.MergeToSingleFile, "result_trc_merged.csv");

        Assert.Equal(new[] { "Время;A1", "10;1", "20;2", "30;3", "40;4" }, lines);
    }

    [Fact]
    public void Split_by_date_writes_absolute_time_per_day()
    {
        using var dir = new TempDir();
        var (input, output, devices, dbc) = Setup(dir);
        File.WriteAllText(Path.Combine(input, "a.trc"), Trc("26.04.2023 23:59:59.990.0", (5, 1), (20, 2)));

        var day1 = RunBatch(input, output, devices, dbc, BatchOutputMode.SplitTrcByDate, "result_2023-04-26.csv");
        var day2 = File.ReadAllLines(Path.Combine(output, "result_2023-04-27.csv"));

        Assert.Equal("2023-04-26 23:59:59.995;1", day1[1]);
        Assert.Equal("2023-04-27 00:00:00.010;2", day2[1]);
    }
}
