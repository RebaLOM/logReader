using ClosedXML.Excel;
using logReader.Processing;

namespace logReader.Tests;

public class ProcessingPipelineTests
{
    private const string TwoFramesTrc =
        "     1)        10.0  Rx     0CFF0008  8  11 00 00 00 00 00 00 00\n" +
        "     2)        20.0  Rx     0CFF0009  8  22 00 00 00 00 00 00 00\n";

    private static List<Device> Devices(TempDir dir, string dbc = TestData.TwoMessageDbc)
        => DeviceFiles.LoadDevices(dir.Write("devices.dbc", dbc));

    private static OutputSettings Settings(OutputFormat format, OutputFilter? filter = null)
        => new() { Format = format, Filter = filter ?? OutputFilter.All };

    [Fact]
    public void Xlsx_time_series_headers_match_data_when_device_has_no_active_params()
    {
        using var dir = new TempDir();
        string trc = dir.Write("two.trc", TwoFramesTrc);
        var filter = OutputFilter.From(null, new Dictionary<string, bool[]> { ["0CFF0008"] = new[] { false } });
        string output = dir.File("two.xlsx");

        var result = new PCanLogProcessor().Process(trc, Devices(dir), output, Settings(OutputFormat.Xlsx, filter), new ProcessingContext());

        Assert.True(result.Success);
        using var wb = new XLWorkbook(output);
        var ws = wb.Worksheet(1);
        Assert.Equal("Время", ws.Cell(1, 1).GetString());
        Assert.Equal("B1", ws.Cell(1, 2).GetString());
        Assert.Equal(20, ws.Cell(2, 1).GetDouble());
        Assert.Equal(0x22, ws.Cell(2, 2).GetDouble());
        Assert.True(ws.Cell(1, 3).IsEmpty());
    }

    [Fact]
    public void Failed_processing_is_not_reported_as_success_and_keeps_previous_output()
    {
        using var dir = new TempDir();
        string trc = dir.Write("nomatch.trc", "     1)        10.0  Rx     00000123  8  11 00 00 00 00 00 00 00\n");
        string output = dir.Write("result.csv", "OLD");

        var result = new LogProcessingService().ProcessSingleFile(trc, output, Devices(dir), Settings(OutputFormat.Csv), new ProcessingContext());

        Assert.False(result.Success);
        Assert.Equal("OLD", File.ReadAllText(output));
    }

    [Fact]
    public void Matrix_csv_without_data_leaves_no_temp_files()
    {
        using var dir = new TempDir();
        string csv = dir.Write("matrix.csv", ";0CFF0008;0CFF0009\n");
        var devices = Devices(dir);

        var result = new MatrixCsvLogProcessor().Process(csv, devices, dir.File("out.csv"), Settings(OutputFormat.Csv), new ProcessingContext());

        Assert.False(result.Success);
        Assert.Empty(Directory.GetFiles(dir.Path, "out*"));
    }

    [Fact]
    public void Legacy_csv_id_without_leading_zero_matches_extended_device()
    {
        using var dir = new TempDir();
        string csv = dir.Write("legacy.csv",
            "1;10:15:30;CFF0008;1;12;0;0;0;0;0;0;0\n2;10:15:31;CFF0008;1;13;0;0;0;0;0;0;0\n");
        string output = dir.File("legacy_result.csv");

        var result = new CanLogProcessor().Process(csv, Devices(dir), output, Settings(OutputFormat.Csv), new ProcessingContext());

        Assert.True(result.Success);
        Assert.Equal(new[] { "Шаг;Время;A1", "1;10:15:30;12", "2;10:15:31;13" }, File.ReadAllLines(output));
    }

    [Fact]
    public void Asc_time_column_is_written_as_time_of_day()
    {
        using var dir = new TempDir();
        string asc = dir.Write("log.asc",
            "date Wed Apr 26 10:00:00.000 2023\nbase hex  timestamps absolute\n" +
            "   1.234600 1  0CFF0008x       Rx   d 8 10 00 00 00 00 00 00 00\n");
        string output = dir.File("asc.csv");

        var result = new AscLogProcessor().Process(asc, Devices(dir), output, Settings(OutputFormat.Csv), new ProcessingContext());

        Assert.True(result.Success);
        Assert.Equal("10:00:01.235;16", File.ReadAllLines(output)[1]);
    }

    [Fact]
    public void Time_of_day_formatting_does_not_lose_milliseconds()
    {
        for (int ms = 0; ms < 86_400_000; ms += 997)
        {
            var expected = TimeSpan.FromMilliseconds(ms);
            string text = TimeAxisFormat.FormatTimeOfDay(expected.TotalDays);
            Assert.Equal(expected.ToString(@"hh\:mm\:ss\.fff"), text);
        }
    }

    [Fact]
    public void Csv_text_that_looks_like_a_formula_is_neutralized()
    {
        Assert.Equal("'=HYPERLINK(\"x\")", CsvOutput.Text("=HYPERLINK(\"x\")"));
        Assert.Equal("-12.5", CsvOutput.Text("-12.5"));
        Assert.Equal("Скорость", CsvOutput.Text("Скорость"));
    }

    [Fact]
    public void Xlsx_output_over_excel_row_limit_fails_before_writing()
    {
        using var dir = new TempDir();
        var devices = Devices(dir);
        var collector = new TimeSeriesCollector(devices, OutputFilter.All, TimeAxisKind.Milliseconds);
        devices[0].SetPayload(new int[8]);
        devices[0].Decode();
        for (int i = 0; i < ExcelLayoutBuilder.MaxRows; i++)
            collector.Record(devices[0], i);
        string output = dir.File("big.xlsx");

        var result = TimeSeriesOutputWriter.Write(collector, output, Settings(OutputFormat.Xlsx), "Log", new ProcessingContext());

        Assert.False(result.Success);
        Assert.Contains("CSV", result.Error);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void Cancellation_stops_processing_without_output()
    {
        using var dir = new TempDir();
        var lines = Enumerable.Range(1, 20_000)
            .Select(i => $"{i,6})  {i}.0  Rx     0CFF0008  8  11 00 00 00 00 00 00 00");
        string trc = dir.Write("long.trc", string.Join("\n", lines));
        string output = dir.File("long.csv");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() =>
            new PCanLogProcessor().Process(trc, Devices(dir), output, Settings(OutputFormat.Csv), new ProcessingContext(cancellationToken: cts.Token)));
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void Batch_continues_after_a_file_without_matches()
    {
        using var dir = new TempDir();
        string input = Directory.CreateDirectory(Path.Combine(dir.Path, "logs")).FullName;
        string output = Directory.CreateDirectory(Path.Combine(dir.Path, "out")).FullName;
        File.WriteAllText(Path.Combine(input, "a.trc"), "     1)        10.0  Rx     00000123  8  11 00 00 00 00 00 00 00\n");
        File.WriteAllText(Path.Combine(input, "b.trc"), TwoFramesTrc);
        var devices = Devices(dir);
        var files = Directory.GetFiles(input).OrderBy(f => f).ToList();

        var outcome = new LogProcessingService().ProcessFolderBatch(
            files, output, dir.File("devices.dbc"), BatchOutputMode.PerInputFile, devices, Settings(OutputFormat.Csv), new ProcessingContext());

        Assert.Equal(2, outcome.Expected);
        Assert.Equal(1, outcome.Created);
        Assert.Equal(1, outcome.Failed);
        Assert.True(File.Exists(Path.Combine(output, "b_trc_result.csv")));
    }

    [Fact]
    public void Loger_result_csv_is_not_detected_as_input_log()
    {
        using var dir = new TempDir();
        string result = dir.Write("x_csv_result.csv", "Шаг;Время;A1\n1;10:15:30;12\n");
        string legacy = dir.Write("legacy.csv", "1;10:15:30;0CFF0008;1;12;0;0;0;0;0;0;0\n");

        Assert.Equal(LogFormatKind.None, LogFormatDetector.Detect(result));
        Assert.Equal(LogFormatKind.StepCsv, LogFormatDetector.Detect(legacy));
    }

    [Fact]
    public void Filter_snapshot_is_not_affected_by_later_changes()
    {
        var deviceEnabled = new Dictionary<string, bool> { ["0CFF0008"] = true };
        var paramEnabled = new Dictionary<string, bool[]> { ["0CFF0008"] = new[] { true } };
        var filter = OutputFilter.From(deviceEnabled, paramEnabled);

        deviceEnabled["0CFF0008"] = false;
        paramEnabled["0CFF0008"][0] = false;

        Assert.True(filter.IsDeviceEnabled("0CFF0008"));
        Assert.True(filter.IsParamEnabled("0CFF0008", 0));
    }
}
