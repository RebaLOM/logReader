using System.Globalization;
using System.Security.Cryptography;
using ClosedXML.Excel;
using logReader.Processing;

namespace logReader.Tests;

public sealed class DtcDisplaySettingsTests
{
    public static IEnumerable<object[]> Formats()
    {
        foreach (string kind in new[] { "trc", "asc", "canfox", "matrix", "step" })
            foreach (OutputFormat output in new[] { OutputFormat.Csv, OutputFormat.Xlsx })
                foreach (DtcDisplayFormat display in Enum.GetValues<DtcDisplayFormat>())
                    yield return [kind, output, display];
    }

    [Fact]
    public void Defaults_enable_automatic_compact_dtc_without_forcing_empty_columns()
    {
        var settings = new OutputSettings();
        Assert.True(settings.AutoIncludeDtc);
        Assert.False(settings.IncludeDtc);
        Assert.Equal(DtcDisplayFormat.Compact, settings.DtcDisplayFormat);
    }

    [Theory]
    [InlineData("ru-RU")]
    [InlineData("en-US")]
    public void Code_formatter_is_text_with_a_literal_point_and_preserves_fmi_zero_and_ten(string culture)
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            Assert.Equal("32770.0", new DtcCode(32770, 0).ToDisplayString(DtcDisplayFormat.Compact));
            Assert.Equal("100.10", new DtcCode(100, 10).ToDisplayString(DtcDisplayFormat.Compact));
            Assert.Equal("SPN 100 / FMI 10", new DtcCode(100, 10).ToDisplayString(DtcDisplayFormat.Detailed));
            Assert.Equal("Н/Д", DtcSlot.Unknown.ToDisplayString(DtcDisplayFormat.Compact));
            Assert.Equal("", DtcSlot.Empty.ToDisplayString(DtcDisplayFormat.Detailed));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void Late_errors_add_exactly_six_columns_in_both_text_formats_and_all_can_formats(
        string kind, OutputFormat outputFormat, DtcDisplayFormat display)
    {
        using var dir = new TempDir();
        string input = WriteInput(dir, kind, active: true);
        string output = dir.File(outputFormat == OutputFormat.Csv ? "out.csv" : "out.xlsx");
        var result = new LogProcessingService().ProcessSingleFile(input, output, Devices(dir),
            new OutputSettings { Format = outputFormat, DtcDisplayFormat = display }, new ProcessingContext());
        Assert.True(result.Success, result.Error);
        string[] expected = [new DtcCode(32770, 0).ToDisplayString(display), new DtcCode(100, 10).ToDisplayString(display)];
        if (outputFormat == OutputFormat.Xlsx)
        {
            using var book = new XLWorkbook(output);
            var sheet = book.Worksheet(1);
            Assert.Equal(6, sheet.Row(1).CellsUsed().Count(c => DtcOutput.Headers.Contains(c.GetString())));
            foreach (string text in expected)
            {
                var cells = sheet.CellsUsed().Where(c => c.GetString() == text).ToArray();
                Assert.NotEmpty(cells);
                Assert.All(cells, cell => Assert.Equal(XLDataType.Text, cell.DataType));
            }
        }
        else
        {
            string text = File.ReadAllText(output);
            Assert.Equal(6, File.ReadLines(output).First().Split(';').Count(DtcOutput.Headers.Contains));
            Assert.All(expected, value => Assert.Contains(value, text));
            Assert.DoesNotContain("Н/Д", text);
        }
    }

    [Theory]
    [InlineData("trc")]
    [InlineData("asc")]
    [InlineData("canfox")]
    [InlineData("matrix")]
    [InlineData("step")]
    [InlineData("dst")]
    public void Empty_or_count_only_diagnostic_frames_keep_original_numeric_csv_bytes(string kind)
    {
        using var dir = new TempDir();
        string input = WriteInput(dir, kind == "dst" ? "trc" : kind, active: false, reportedCount: 7);
        string off = dir.File("off.csv"), automatic = dir.File("automatic.csv");
        var format = kind == "dst" ? OutputFormat.CsvDstConnect : OutputFormat.Csv;
        var service = new LogProcessingService();
        Assert.True(service.ProcessSingleFile(input, off, Devices(dir),
            new OutputSettings { Format = format, AutoIncludeDtc = false }, new ProcessingContext()).Success);
        Assert.True(service.ProcessSingleFile(input, automatic, Devices(dir),
            new OutputSettings { Format = format }, new ProcessingContext()).Success);
        Assert.Equal(SHA256.HashData(File.ReadAllBytes(off)), SHA256.HashData(File.ReadAllBytes(automatic)));
        Assert.DoesNotContain("DTC", File.ReadAllText(automatic));
    }

    [Theory]
    [InlineData("trc")]
    [InlineData("asc")]
    [InlineData("canfox")]
    [InlineData("matrix")]
    [InlineData("step")]
    public void Empty_dtc_only_input_cannot_publish_an_automatic_blank_result(string kind)
    {
        using var dir = new TempDir();
        string input = WriteInput(dir, kind, active: false);
        string output = dir.Write("out.csv", "PREVIOUS");
        var result = new LogProcessingService().ProcessSingleFile(input, output, [], new OutputSettings(), new ProcessingContext());
        Assert.False(result.Success);
        Assert.Equal("PREVIOUS", File.ReadAllText(output));
        Assert.Empty(Directory.GetFiles(dir.Path, "*.tmp*"));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Manual_force_and_automatic_switch_have_distinct_effects(bool automatic, bool force)
    {
        using var dir = new TempDir();
        string input = WriteInput(dir, "matrix", active: false);
        string output = dir.File("out.csv");
        Assert.True(new MatrixCsvLogProcessor().Process(input, Devices(dir), output,
            new OutputSettings { AutoIncludeDtc = automatic, IncludeDtc = force }, new ProcessingContext()).Success);
        Assert.Equal(force, File.ReadLines(output).First().Contains("DTC1"));
    }

    [Fact]
    public void Disabling_automatic_mode_omits_dtc_even_when_errors_exist()
    {
        using var dir = new TempDir();
        string input = WriteInput(dir, "trc", active: true);
        string output = dir.File("out.csv");
        Assert.True(new PCanLogProcessor().Process(input, Devices(dir), output,
            new OutputSettings { AutoIncludeDtc = false }, new ProcessingContext()).Success);
        Assert.DoesNotContain("DTC", File.ReadAllText(output));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Automatic_step_csv_preserves_multiline_quoted_headers_and_id_row(bool idRow)
    {
        using var dir = new TempDir();
        string input = WriteInput(dir, "step", active: true);
        var devices = Devices(dir);
        devices[0].Headers[0] = "A; \"quoted\"\nsecond line";
        string automatic = dir.File("automatic.csv"), forced = dir.File("forced.csv");
        var processor = new CanLogProcessor();
        Assert.True(processor.Process(input, devices, automatic,
            new OutputSettings { IncludeDeviceIdHeaderRow = idRow }, new ProcessingContext()).Success);
        Assert.True(processor.Process(input, devices, forced,
            new OutputSettings { IncludeDtc = true, IncludeDeviceIdHeaderRow = idRow }, new ProcessingContext()).Success);
        Assert.Equal(File.ReadAllBytes(forced), File.ReadAllBytes(automatic));
    }

    [Fact]
    public void Cancellation_after_spooling_dtc_cleans_both_temps_and_preserves_existing_output()
    {
        using var dir = new TempDir();
        string output = dir.Write("out.csv", "PREVIOUS");
        using var cancellation = new CancellationTokenSource();
        using (var writer = new StepOutputWriter(output, OutputFormat.Csv, [], false, autoIncludeDtc: true,
            context: new ProcessingContext(cancellationToken: cancellation.Token)))
        {
            var snapshot = new DtcSnapshot(0, [DtcSlot.Present(new DtcCode(12345, 10))], 1, false, "");
            writer.WriteRow(1, "10:00:00", snapshot);
            cancellation.Cancel();
            Assert.Throws<OperationCanceledException>(writer.Complete);
        }
        Assert.Equal("PREVIOUS", File.ReadAllText(output));
        Assert.Empty(Directory.GetFiles(dir.Path, "*.tmp*"));
    }

    [Theory]
    [InlineData(BatchOutputMode.PerInputFile, OutputFormat.Csv)]
    [InlineData(BatchOutputMode.MergeToSingleFile, OutputFormat.Csv)]
    [InlineData(BatchOutputMode.SplitTrcByDate, OutputFormat.Csv)]
    [InlineData(BatchOutputMode.PerInputFile, OutputFormat.Xlsx)]
    [InlineData(BatchOutputMode.MergeToSingleFile, OutputFormat.Xlsx)]
    [InlineData(BatchOutputMode.SplitTrcByDate, OutputFormat.Xlsx)]
    public void Batch_auto_detection_applies_to_each_result_and_resets_between_files(BatchOutputMode mode, OutputFormat format)
    {
        using var dir = new TempDir();
        string first = WriteInput(dir, "trc", false, name: "first", day: 25);
        string second = WriteInput(dir, "trc", true, name: "second", day: 26);
        string outputDir = Directory.CreateDirectory(dir.File("results")).FullName;
        string dbc = dir.Write("devices.dbc", TestData.TwoMessageDbc);
        var result = new LogProcessingService().ProcessFolderBatch([first, second], outputDir, dbc, mode,
            DeviceFiles.LoadDevices(dbc), new OutputSettings { Format = format }, new ProcessingContext());
        Assert.Equal(0, result.Failed);
        int expected = mode == BatchOutputMode.MergeToSingleFile ? 1 : 2;
        Assert.Equal(expected, result.Created);
        var files = Directory.GetFiles(outputDir);
        Assert.Equal(expected, files.Length);
        int withDtc = files.Count(path => HasDtcHeader(path, format));
        Assert.Equal(1, withDtc);
    }

    [Fact]
    public void Dst_detects_short_confirmed_error_even_if_all_block_end_states_are_empty()
    {
        using var dir = new TempDir();
        var active = LocarusDtcTests.Frames([new DtcCode(32770, 0)], 1);
        var empty = LocarusDtcTests.Frames([], 0);
        var lines = new List<string> { Frame(1, 0, "0CFF0008", [17, 0, 0, 0, 0, 0, 0, 0]) };
        for (int i = 0; i < 3; i++) lines.Add(Frame(i + 2, i + 1, active[i].Id, active[i].Bytes));
        for (int i = 0; i < 3; i++) lines.Add(Frame(i + 5, i + 4, empty[i].Id, empty[i].Bytes));
        lines.Add(Frame(8, 20, "0CFF0008", [18, 0, 0, 0, 0, 0, 0, 0]));
        string input = dir.Write("short.trc", string.Join('\n', lines));
        string output = dir.File("out.csv");
        Assert.True(new DstConnectTrcProcessor().Process(input, Devices(dir), output,
            new OutputSettings { Format = OutputFormat.CsvDstConnect }, new ProcessingContext()).Success);
        Assert.Contains("DTC1", File.ReadLines(output).First());
    }

    [Fact]
    public void Provided_dst_csv_automatic_mode_includes_only_counterrotation_and_preserves_all_nine_sources()
    {
        string? root = Environment.GetEnvironmentVariable("LOGER_DTC_TEST_DATA_DIR");
        if (string.IsNullOrWhiteSpace(root)) return;
        Assert.True(Directory.Exists(root), "LOGER_DTC_TEST_DATA_DIR должен указывать непосредственно на папку DST.");
        string[] sources = Directory.GetFiles(root, "*.trc", SearchOption.TopDirectoryOnly).Order().ToArray();
        Assert.Equal(9, sources.Length);
        var sourceHashes = sources.ToDictionary(path => path, FileHash);
        using var dir = new TempDir();
        var service = new LogProcessingService();
        int withDtc = 0;
        for (int i = 0; i < sources.Length; i++)
        {
            string source = sources[i];
            string automatic = dir.File($"automatic-{i}.csv"), off = dir.File($"off-{i}.csv");
            var devices = RealTraceDevices(dir, source);
            var result = service.ProcessSingleFile(source, automatic, devices, new OutputSettings(), new ProcessingContext());
            Assert.True(result.Success, result.Error);
            Assert.True(service.ProcessSingleFile(source, off, devices,
                new OutputSettings { AutoIncludeDtc = false }, new ProcessingContext()).Success);
            string[] rows = File.ReadAllLines(automatic);
            bool expected = Path.GetFileName(source) == "контрвращение_085008.trc";
            Assert.Equal(expected, rows[0].Contains("DTC1"));
            if (expected)
            {
                withDtc++;
                Assert.Equal(6, rows[0].Split(';').Count(DtcOutput.Headers.Contains));
                Assert.Contains(rows.Skip(1).SelectMany(row => row.Split(';')), cell => cell == "32770.0");
                Assert.DoesNotContain("SPN ", File.ReadAllText(automatic));
                Assert.Equal(File.ReadAllLines(off).Skip(1), rows.Skip(1).Select(row => row.Split(';'))
                    .Where(row => row[0].Length > 0).Select(row => string.Join(';', row.Take(2))));
            }
            else Assert.Equal(FileHash(off), FileHash(automatic));
        }
        Assert.Equal(1, withDtc);
        foreach (string source in sources) Assert.Equal(sourceHashes[source], FileHash(source));
    }

    [Theory]
    [InlineData("контрвращение_085008.trc", true)]
    [InlineData("trace_2025-10-16T06_45_04_085033.trc", false)]
    public void Provided_dst_xlsx_automatic_mode_uses_compact_text_or_preserves_the_numeric_sheet(string file, bool hasErrors)
    {
        string? root = Environment.GetEnvironmentVariable("LOGER_DTC_TEST_DATA_DIR");
        if (string.IsNullOrWhiteSpace(root)) return;
        string source = Path.Combine(root, file);
        string originalHash = FileHash(source);
        using var dir = new TempDir();
        var devices = RealTraceDevices(dir, source);
        string automatic = dir.File("automatic.xlsx"), off = dir.File("off.xlsx");
        var service = new LogProcessingService();
        Assert.True(service.ProcessSingleFile(source, automatic, devices,
            new OutputSettings { Format = OutputFormat.Xlsx }, new ProcessingContext()).Success);
        using var book = new XLWorkbook(automatic);
        var sheet = book.Worksheet(1);
        Assert.Equal(hasErrors, sheet.Row(1).CellsUsed().Any(c => c.GetString() == "DTC1"));
        if (hasErrors)
        {
            var values = sheet.CellsUsed().Where(c => c.GetString() == "32770.0").ToArray();
            Assert.NotEmpty(values);
            Assert.All(values, cell => Assert.Equal(XLDataType.Text, cell.DataType));
            Assert.Equal(6, sheet.Row(1).CellsUsed().Count(c => DtcOutput.Headers.Contains(c.GetString())));
        }
        else
        {
            Assert.True(service.ProcessSingleFile(source, off, devices,
                new OutputSettings { Format = OutputFormat.Xlsx, AutoIncludeDtc = false }, new ProcessingContext()).Success);
            using var reference = new XLWorkbook(off);
            static IEnumerable<string> Cells(IXLWorksheet ws)
                => ws.CellsUsed().Select(cell => $"{cell.Address}|{cell.DataType}|{cell.Value}");
            Assert.Equal(Cells(reference.Worksheet(1)), Cells(sheet));
        }
        Assert.Equal(originalHash, FileHash(source));
    }

    private static List<Device> RealTraceDevices(TempDir dir, string path)
    {
        int[] bytes = new int[Device.MaxDataLength];
        foreach (string line in File.ReadLines(path))
        {
            if (!TrcLogParser.TryParseTrcFrameLine(line, out _, out _, out string id, out _, bytes, out int count)
                || count == 0 || LocarusDtcDecoder.IsDiagnosticId(id)) continue;
            uint raw = uint.Parse(id, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            uint dbcId = raw > 0x7ff ? raw | 0x80000000u : raw;
            string dbc = dir.Write("real-device.dbc", FormattableString.Invariant(
                $"BO_ {dbcId} Probe: 8 X\n SG_ Raw : 0|8@1+ (1,0) [0|255] \"\" X\n"));
            return DeviceFiles.LoadDevices(dbc);
        }
        throw new InvalidDataException("В реальном TRC не найден числовой CAN-параметр для проверки.");
    }

    private static string FileHash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static bool HasDtcHeader(string path, OutputFormat format)
    {
        if (format == OutputFormat.Csv) return File.ReadLines(path).First().Contains("DTC1");
        using var book = new XLWorkbook(path);
        return book.Worksheet(1).Row(1).CellsUsed().Any(c => c.GetString() == "DTC1");
    }

    private static List<Device> Devices(TempDir dir)
        => DeviceFiles.LoadDevices(dir.Write("devices.dbc", TestData.TwoMessageDbc));

    private static string WriteInput(TempDir dir, string kind, bool active, int reportedCount = 0, string name = "input", int day = 25)
    {
        var empty = LocarusDtcTests.Frames([], reportedCount);
        var last = active ? LocarusDtcTests.Frames([new DtcCode(32770, 0), new DtcCode(100, 10)], 2) : empty;
        if (kind == "matrix")
            return dir.Write(name + ".csv", ";0CFF0008;18FE2503;18FE2603;18FE2703\n" +
                "10:00:00;1100000000000000;" + string.Join(';', empty.Select(f => Hex(f.Bytes, ""))) + "\n" +
                "10:00:00.100;1200000000000000;" + string.Join(';', last.Select(f => Hex(f.Bytes, ""))) + "\n");
        if (kind == "step")
        {
            string Row(int step, int value, (string Id, int[] Bytes)[] frames)
                => $"{step};10:00:00.{step:000};0CFF0008;1;{value};0;0;0;0;0;0;0\n" +
                    string.Concat(frames.Select(f => $";;{f.Id};1;{string.Join(';', f.Bytes)}\n"));
            return dir.Write(name + ".csv", Row(1, 17, empty) + Row(2, 18, last));
        }
        var wire = new List<(int Ms, string Id, int[] Bytes)> { (0, "0CFF0008", [17, 0, 0, 0, 0, 0, 0, 0]) };
        wire.AddRange(empty.Select((f, i) => (i + 1, f.Id, f.Bytes)));
        wire.AddRange(last.Select((f, i) => (i + 101, f.Id, f.Bytes)));
        wire.Add((200, "0CFF0008", [18, 0, 0, 0, 0, 0, 0, 0]));
        if (kind == "asc")
            return dir.Write(name + ".asc", "base hex timestamps absolute\n" + string.Join('\n', wire.Select(f =>
                FormattableString.Invariant($"{f.Ms / 1000d:F3} 1 {f.Id}x Rx d 8 {Hex(f.Bytes, " ")}"))) + "\n");
        if (kind == "canfox")
            return dir.Write(name + ".txt", "Date Time ID Len Data\n" + string.Join('\n', wire.Select(f =>
                $"2026.10.{day} 10:00:00.{f.Ms:000} - {f.Id} 8 {Hex(f.Bytes, " ")}")) + "\n");
        return dir.Write(name + ".trc", $"; Start time: {day}.10.2026 10:00:00.000\n" +
            string.Join('\n', wire.Select((f, i) => Frame(i + 1, f.Ms, f.Id, f.Bytes))) + "\n");
    }

    private static string Frame(int index, int ms, string id, int[] bytes)
        => $"{index}) {ms} Rx {id} 8 {Hex(bytes, " ")}";

    private static string Hex(int[] bytes, string separator)
        => string.Join(separator, bytes.Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));
}
