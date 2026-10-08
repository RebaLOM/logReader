using logReader.Processing;
using System.Text;

namespace logReader.Tests;

// Compatibility scenarios identified before LOGER 2.0's presentation-layer changes.
public class CompatibilityRegressionTests
{
    private static List<Device> Devices(TempDir dir)
        => DeviceFiles.LoadDevices(dir.Write("devices.dbc", TestData.TwoMessageDbc));

    [Fact]
    public void Legacy_rejected_and_malformed_payloads_keep_previous_value()
    {
        using var dir = new TempDir();
        string input = dir.Write("accepted.csv",
            "1;10:00:00;0CFF0008;1;16;0;0;0;0;0;0;0\n" +
            "2;10:00:01;0CFF0008;0;255;0;0;0;0;0;0;0\n" +
            "3;10:00:02;0CFF0008;1;BAD;0;0;0;0;0;0;0\n" +
            "4;10:00:03;0CFF0008;1;32;0;0;0;0;0;0;0\n");
        string output = dir.File("result.csv");
        var result = new LogProcessingService().ProcessSingleFile(input, output, Devices(dir),
            new OutputSettings(), new ProcessingContext());

        Assert.True(result.Success);
        Assert.Equal(new[] { "Шаг;Время;A1", "1;10:00:00;16", "2;10:00:01;16", "3;10:00:02;16", "4;10:00:03;32" },
            File.ReadAllLines(output));
    }

    [Fact]
    public void Repeated_processing_resets_cached_device_state()
    {
        using var dir = new TempDir();
        var devices = Devices(dir);
        string first = dir.Write("first.csv", "1;10:00:00;0CFF0009;1;42;0;0;0;0;0;0;0\n");
        string second = dir.Write("second.csv", "1;10:00:00;0CFF0008;1;5;0;0;0;0;0;0;0\n");
        var service = new LogProcessingService();
        Assert.True(service.ProcessSingleFile(first, dir.File("first-result.csv"), devices,
            new OutputSettings(), new ProcessingContext()).Success);
        Assert.Equal(42, devices[1].Values[0]);

        Assert.True(service.ProcessSingleFile(second, dir.File("second-result.csv"), devices,
            new OutputSettings(), new ProcessingContext()).Success);
        Assert.True(double.IsNaN(devices[1].Values[0]));
        Assert.Equal(new[] { "Шаг;Время;A1", "1;10:00:00;5" }, File.ReadAllLines(dir.File("second-result.csv")));
    }

    [Fact]
    public void Matrix_empty_and_invalid_cells_hold_values_and_duplicate_time_advances_twenty_ms()
    {
        using var dir = new TempDir();
        string input = dir.Write("matrix.csv", ";0CFF0008;0CFF0009\n" +
            "10:00:00;1000000000000000;2000000000000000\n" +
            "10:00:00;;INVALID\n" +
            "10:00:00;3000000000000000;\n");
        string output = dir.File("result.csv");
        Assert.True(new LogProcessingService().ProcessSingleFile(input, output, Devices(dir),
            new OutputSettings(), new ProcessingContext()).Success);
        Assert.Equal(new[] { "Шаг;Время;A1;B1", "1;10:00:00.000;16;32", "2;10:00:00.020;16;32", "3;10:00:00.040;48;32" },
            File.ReadAllLines(output));
    }

    [Fact]
    public void Canfox_txt_is_detected_processed_and_incomplete_rows_skipped()
    {
        using var dir = new TempDir();
        string input = dir.Write("CAN.txt", "Date Time - ID Len Data\n" +
            "2026.10.09 10:00:00.125 - 0CFF0008 8 11 00 00 00 00 00 00 00\n" +
            "2026.10.09 10:00:01.125 - 0CFF0008 8 99\n" +
            "2026.10.09 10:00:02.500 - CFF0008 1 22\n");
        string output = dir.File("result.csv");
        Assert.Equal(LogFormatKind.CanfoxTxt, LogFormatDetector.Detect(input));
        Assert.True(new LogProcessingService().ProcessSingleFile(input, output, Devices(dir),
            new OutputSettings(), new ProcessingContext()).Success);
        Assert.Equal(new[] { "Время;A1", "10:00:00.125;17", "10:00:02.500;34" }, File.ReadAllLines(output));
    }

    [Fact]
    public void Composite_sources_still_update_when_source_devices_are_disabled()
    {
        using var dir = new TempDir();
        var composite = CompositeRuntime.Build(new[]
        {
            new CompositeSignal
            {
                Block = "GROUP", Param = "Combined", Scale = .5, Offset = -1, TriggerId = "0CFF0009",
                Pieces = new() { new("0CFF0008", 0, 0, 8), new("0CFF0009", 0, 0, 8) }
            }
        });
        var filter = OutputFilter.From(new Dictionary<string, bool> { ["0CFF0008"] = false, ["0CFF0009"] = false }, null);
        string input = dir.Write("composite.trc",
            "1) 1.0 Rx 0CFF0009 1 02\n2) 2.0 Rx 0CFF0008 1 01\n3) 3.0 Rx 0CFF0009 1 03\n");
        string output = dir.File("result.csv");
        Assert.True(new LogProcessingService().ProcessSingleFile(input, output, Devices(dir),
            new OutputSettings { Filter = filter, Composites = composite }, new ProcessingContext()).Success);
        // The existing time-series path emits on every ready source, including
        // a source other than TriggerId. Preserve that behavior during redesign.
        Assert.Equal(new[] { "Время;Combined", "2;128", "3;128,5" }, File.ReadAllLines(output));
    }

    [Fact]
    public void Unknown_device_scan_counts_valid_frames_and_excludes_result_csv()
    {
        using var dir = new TempDir();
        string folder = Directory.CreateDirectory(Path.Combine(dir.Path, "inputs")).FullName;
        File.WriteAllText(Path.Combine(folder, "log.trc"),
            "1) 1.0 Rx 0CFF0008 1 11\n2) 2.0 Rx 0CFF000A 1 22\n3) 3.0 Rx 0CFF000A 8 33\n");
        File.WriteAllText(Path.Combine(folder, "result.csv"), "Шаг;Время;A1\n1;10:00:00;17\n");
        var result = UnknownDevicesScanner.ScanLogDevices(folder, Devices(dir));
        Assert.Equal(new[] { "0CFF0008" }, result.MatchedInDevices);
        Assert.Equal(new[] { "0CFF000A" }, result.MissingInDevices);
        var counts = UnknownDevicesScanner.CollectLogIds(folder);
        Assert.Equal(1, counts["0CFF000A"]);
        Assert.Equal(2, counts.Count);
    }

    [Fact]
    public void Dst_export_rejects_non_trc_without_overwriting_existing_result()
    {
        using var dir = new TempDir();
        string input = dir.Write("log.asc", "date Fri Oct 9 10:00:00 2026\nbase hex timestamps absolute\n");
        string output = dir.Write("old.csv", "KEEP");
        var result = new LogProcessingService().ProcessSingleFile(input, output, Devices(dir),
            new OutputSettings { Format = OutputFormat.CsvDstConnect }, new ProcessingContext());
        Assert.False(result.Success);
        Assert.Equal("KEEP", File.ReadAllText(output));
    }

    [Fact]
    public void Bin_signed_scaled_and_invalid_instructions_keep_distinct_output_states()
    {
        var device = new DynamicDevice("100", new List<FieldInstruction>
        {
            new() { FieldIndex = 0, Header = "Flags", Type = "BIN", ByteLow = 0, StartBit = 2, LengthBit = 3 },
            new() { FieldIndex = 1, Header = "Signed", Type = "NUM", StartBit = 8, LengthBit = 8,
                SignedRaw = true, Scale = .5, Offset = 10 },
            new() { FieldIndex = 2, Header = "Invalid", Type = "NUM", StartBit = 511, LengthBit = 2 }
        });
        Assert.Equal("", device.FormatValue(0));
        device.SetPayload(new[] { 0b00010100, 0xFC });
        device.Decode();
        Assert.Equal(5, device.Values[0]);
        Assert.Equal(8, device.Values[1]);
        Assert.Equal("ERR", device.FormatValue(2));
        Assert.False(device.FieldErrors[0]);
        device.ResetState();
        Assert.Equal("", device.FormatValue(2));
        Assert.All(device.FieldErrors, error => Assert.False(error));
    }

    [Fact]
    public void Dbc_float64_signal_applies_factor_and_offset()
    {
        using var dir = new TempDir();
        string dbc = dir.Write("float64.dbc", "BO_ 2365521928 Double: 8 X\n" +
            " SG_ Value : 0|64@1+ (2,-1) [-100|100] \"\" X\n" +
            "SIG_VALTYPE_ 2365521928 Value : 2;\n");
        var device = DeviceFiles.LoadDevices(dbc).Single();
        device.SetPayload(BitConverter.GetBytes(12.25).Select(b => (int)b).ToArray());
        device.Decode();
        Assert.Equal(23.5, device.Values[0]);
    }

    [Theory]
    [InlineData("utf8")]
    [InlineData("utf16le")]
    [InlineData("utf16be")]
    [InlineData("cp1251")]
    public void Log_encoding_and_shared_writer_handle_preserve_processing(string kind)
    {
        using var dir = new TempDir();
        Encoding encoding = kind switch
        {
            "utf8" => new UTF8Encoding(true),
            "utf16le" => Encoding.Unicode,
            "utf16be" => Encoding.BigEndianUnicode,
            _ => LogFileEncoding.Windows1251
        };
        string input = dir.File("encoding.trc");
        File.WriteAllText(input, "; Комментарий с кириллицей\n1) 1.0 Rx 0CFF0008 1 2A\n", encoding);
        using var liveWriter = new FileStream(input, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);
        string output = dir.File("result.csv");
        Assert.True(new LogProcessingService().ProcessSingleFile(input, output, Devices(dir),
            new OutputSettings(), new ProcessingContext()).Success);
        Assert.Equal(new[] { "Время;A1", "1;42" }, File.ReadAllLines(output));
    }
}
