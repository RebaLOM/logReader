using ClosedXML.Excel;
using logReader.Processing;

namespace logReader.Tests;

[Collection("Excel sheet limit")]
public sealed class AuditRegressionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Export_protects_the_composite_workbook(bool batch)
    {
        using var dir = new TempDir();
        string log = dir.Write("sample.trc", "1) 10.0 Rx 0CFF0008 8 11 00 00 00 00 00 00 00\n");
        string dbc = dir.Write("devices.dbc", TestData.TwoMessageDbc);
        string book = dir.File("sample_trc_result.xlsx");
        CompositeExcelFile.WriteAll(book, [new CompositeSignal
        {
            Param = "Original", Pieces = [new CompositePiece("0CFF0008", 0, 0, 8)]
        }]);
        byte[] original = File.ReadAllBytes(book);
        var devices = DeviceFiles.LoadDevices(dbc);
        var settings = new OutputSettings
        {
            Format = OutputFormat.Xlsx, Composites = DeviceFiles.LoadComposites(book), CompositesSourcePath = book
        };
        var service = new LogProcessingService();
        var context = new ProcessingContext();
        if (batch)
            Assert.Equal(new LogProcessingService.BatchOutcome(0, 1, 1),
                service.ProcessFolderBatch([log], dir.Path, dbc, BatchOutputMode.PerInputFile, devices, settings, context));
        else
            Assert.False(service.ProcessSingleFile(log, book, devices, settings, context).Success);
        Assert.Equal(original, File.ReadAllBytes(book));
    }

    [Fact]
    public void Short_payload_marks_the_absent_byte_and_recovers_on_the_next_frame()
    {
        using var dir = new TempDir();
        string dbc = dir.Write("length.dbc", """
            BO_ 256 Packet: 8 ECU
             SG_ First : 0|8@1+ (1,0) [0|255] "" ECU
             SG_ Second : 8|8@1+ (1,0) [0|255] "" ECU
            """);
        string log = dir.Write("short.trc", "1) 10.0 Rx 100 1 11\n2) 20.0 Rx 100 2 22 33\n");
        var service = new LogProcessingService();
        var devices = DeviceFiles.LoadDevices(dbc);
        string csv = dir.File("out.csv"), xlsx = dir.File("out.xlsx");
        Assert.True(service.ProcessSingleFile(log, csv, devices, new OutputSettings(), new ProcessingContext()).Success);
        Assert.Equal(["10;17;ERR", "20;34;51"], File.ReadAllLines(csv).Skip(1));
        Assert.True(service.ProcessSingleFile(log, xlsx, devices,
            new OutputSettings { Format = OutputFormat.Xlsx }, new ProcessingContext()).Success);
        using var workbook = new XLWorkbook(xlsx);
        Assert.Equal("ERR", workbook.Worksheet(1).Cell(2, 3).GetString());
        Assert.Equal(51d, workbook.Worksheet(1).Cell(3, 3).GetDouble());
    }

    [Fact]
    public void Asc_preserves_the_standard_and_extended_variants_of_the_same_number()
    {
        using var dir = new TempDir();
        string dbc = dir.Write("variants.dbc", """
            BO_ 256 Std: 1 ECU
             SG_ Standard : 0|8@1+ (1,0) [0|255] "" ECU
            BO_ 2147483904 Ext: 1 ECU
             SG_ Extended : 0|8@1+ (1,0) [0|255] "" ECU
            """);
        string asc = dir.Write("sample.asc", "base hex timestamps absolute\n0.1 1 100 Rx d 1 12\n0.2 1 100x Rx d 1 34\n");
        string csv = dir.File("out.csv"), xlsx = dir.File("out.xlsx");
        var service = new LogProcessingService();
        var devices = DeviceFiles.LoadDevices(dbc);
        Assert.Equal(["100", "100x"], UnknownDevicesScanner.CollectLogIds(asc).Keys.Order());
        Assert.True(service.ProcessSingleFile(asc, csv, devices,
            new OutputSettings { IncludeDeviceIdHeaderRow = true }, new ProcessingContext()).Success);
        Assert.Equal(["100", "", "100x", ""], File.ReadLines(csv).First().Split(';'));
        Assert.Equal(["00:00:00.100", "18", "00:00:00.200", "52"], File.ReadLines(csv).Last().Split(';'));
        Assert.True(service.ProcessSingleFile(asc, xlsx, devices,
            new OutputSettings { Format = OutputFormat.Xlsx, IncludeDeviceIdHeaderRow = true },
            new ProcessingContext()).Success);
        using var workbook = new XLWorkbook(xlsx);
        Assert.Equal(18d, workbook.Worksheet(1).Cell(3, 2).GetDouble());
        Assert.Equal(52d, workbook.Worksheet(1).Cell(3, 4).GetDouble());
    }

    [Fact]
    public void Composite_source_preserves_extended_id_and_reports_an_absent_byte()
    {
        using var dir = new TempDir();
        string book = dir.File("composites.xlsx");
        CompositeExcelFile.WriteAll(book, [new CompositeSignal
        {
            Param = "Second byte", Pieces = [new CompositePiece("100x", 1, 0, 8)]
        }]);
        string asc = dir.Write("sample.asc", """
            base hex timestamps absolute
            0.1 1 100 Rx d 2 11 22
            0.2 1 100x Rx d 1 33
            0.3 1 100x Rx d 2 44 55
            """);
        string result = dir.File("out.csv");
        Assert.True(new LogProcessingService().ProcessSingleFile(asc, result, [],
            new OutputSettings { Composites = DeviceFiles.LoadComposites(book) }, new ProcessingContext()).Success);
        Assert.Equal(["00:00:00.200;ERR", "00:00:00.300;85"], File.ReadLines(result).Skip(1));
    }

    [Fact]
    public void Cancel_after_building_xlsx_sheets_preserves_the_previous_file()
    {
        using var dir = new TempDir();
        using var cancellation = new CancellationTokenSource();
        string dbc = dir.Write("devices.dbc", TestData.TwoMessageDbc);
        string log = dir.Write("sample.trc", string.Join('\n', Enumerable.Range(1, 4)
            .Select(i => $"{i}) {i} Rx 0CFF0008 8 11 00 00 00 00 00 00 00")));
        string target = dir.Write("out.xlsx", "previous contents");
        var context = new ProcessingContext(message =>
        {
            if (message.StartsWith("XLSX: данные разбиты", StringComparison.Ordinal)) cancellation.Cancel();
        }, cancellationToken: cancellation.Token);
        int previous = ExcelLayoutBuilder.RowsPerSheet;
        try
        {
            ExcelLayoutBuilder.RowsPerSheet = 3;
            Assert.Throws<OperationCanceledException>(() => new LogProcessingService().ProcessSingleFile(log, target,
                DeviceFiles.LoadDevices(dbc), new OutputSettings { Format = OutputFormat.Xlsx }, context));
        }
        finally { ExcelLayoutBuilder.RowsPerSheet = previous; }
        Assert.Equal("previous contents", File.ReadAllText(target));
        Assert.Empty(Directory.GetFiles(dir.Path, "*.tmp*"));
    }

    [Fact]
    public void Canceled_dst_csv_writer_preserves_the_previous_file()
    {
        using var dir = new TempDir();
        string path = dir.Write("out.csv", "previous");
        var context = new ProcessingContext(cancellationToken: new CancellationToken(canceled: true));
        Assert.Throws<OperationCanceledException>(() => DstConnectCsvWriter.Write(path, null,
            [], [new DstConnectSnapshotRow(20, [])], false, context));
        Assert.Equal("previous", File.ReadAllText(path));
        Assert.Empty(Directory.GetFiles(dir.Path, "*.tmp"));
    }

    [Fact]
    public void Matrix_batch_keeps_only_log_rows_when_notes_and_a_previous_result_are_selected()
    {
        using var dir = new TempDir();
        string notes = dir.Write("notes.csv", "owner;description;revision\n;0CFF0008;C1\n");
        string output = dir.Write("result.csv", ";0CFF0008;0CFF0009\nВремя;A;B\n10:00:00;17;22\n");
        string log = dir.Write("log.csv", ";0CFF0008;0CFF0009\n10:00:00;1100000000000000;\n");
        string dbc = dir.Write("devices.dbc", TestData.TwoMessageDbc);
        Assert.Equal(LogFormatKind.None, LogFormatDetector.Detect(notes));
        Assert.Equal(LogFormatKind.None, LogFormatDetector.Detect(output));
        Assert.Equal(new LogProcessingService.BatchOutcome(1, 3, 2),
            new LogProcessingService().ProcessFolderBatch([notes, output, log], dir.Path, dbc,
                BatchOutputMode.MergeToSingleFile, DeviceFiles.LoadDevices(dbc), new OutputSettings(), new ProcessingContext()));
        Assert.Equal(["Шаг;Время;A1;B1", "1;10:00:00.000;17;"],
            File.ReadAllLines(dir.File("result_matrix_csv_merged.csv")));
    }

    [Fact]
    public void Format_detection_and_folder_scans_honor_cancellation()
    {
        using var dir = new TempDir();
        string log = dir.Write("log.csv", ";0CFF0008;0CFF0009\n10:00:00;1100000000000000;\n");
        var canceled = new CancellationToken(canceled: true);
        Assert.Throws<OperationCanceledException>(() => LogFormatDetector.Detect(log, cancellationToken: canceled));
        Assert.Throws<OperationCanceledException>(() => LogFolderScanner.Scan(dir.Path, canceled));
    }

    [Theory]
    [InlineData(BatchOutputMode.MergeToSingleFile, "result_trc_merged.csv")]
    [InlineData(BatchOutputMode.SplitTrcByDate, "result_2023-04-26.csv")]
    public void Overlapping_trc_rows_are_ordered_with_their_values(BatchOutputMode mode, string result)
    {
        using var dir = new TempDir();
        string dbc = dir.Write("devices.dbc", TestData.TwoMessageDbc);
        string a = dir.Write("a.trc", "; Start time: 26.04.2023 10:00:00.000.0\n1) 5.0 Rx 0CFF0008 1 11\n2) 100.0 Rx 0CFF0008 1 22\n");
        string b = dir.Write("b.trc", "; Start time: 26.04.2023 10:00:00.050.0\n1) 5.0 Rx 0CFF0008 1 33\n2) 20.0 Rx 0CFF0008 1 44\n");
        Assert.Equal(new LogProcessingService.BatchOutcome(1, 1, 0),
            new LogProcessingService().ProcessFolderBatch([a, b], dir.Path, dbc, mode,
                DeviceFiles.LoadDevices(dbc), new OutputSettings(), new ProcessingContext()));
        Assert.Equal(["17", "51", "68", "34"], File.ReadLines(dir.File(result)).Skip(1).Select(line => line.Split(';')[1]));
    }

    [Fact]
    public void Canfox_contents_in_trc_match_txt_in_diagnostics_and_packet_counts()
    {
        using var dir = new TempDir();
        var frames = LocarusDtcTests.Frames([new DtcCode(32770, 0)], 1);
        string contents = string.Join('\n', frames.Select((frame, i) =>
            $"2026.07.06 00:00:00.10{i} - {frame.Id} 8 {string.Join(' ', frame.Bytes.Select(b => b.ToString("X2")))}"));
        string trc = dir.Write("sample.trc", contents);
        string txt = dir.Write("sample.txt", contents);
        Assert.Equal(LogFormatKind.CanfoxTxt, LogFormatDetector.Detect(trc));
        Assert.Equal(new DtcCode(32770, 0), Assert.Single(DtcAnalysisService.Analyze(trc).Summaries).Code);
        Assert.Equal(UnknownDevicesScanner.CollectLogIds(txt).OrderBy(p => p.Key),
            UnknownDevicesScanner.CollectLogIds(trc).OrderBy(p => p.Key));
        var settings = new OutputSettings { IncludeDtc = true };
        Assert.Equal(new LogProcessingService.BatchOutcome(2, 2, 0),
            new LogProcessingService().ProcessFolderBatch([trc, txt], dir.Path, "", BatchOutputMode.MergeToSingleFile,
                [], settings, new ProcessingContext()));
        Assert.Equal(File.ReadAllBytes(dir.File("sample_txt_result.csv")), File.ReadAllBytes(dir.File("sample_trc_result.csv")));
    }

    [Fact]
    public void Dbf_recovers_the_next_message_when_the_previous_end_is_missing()
    {
        using var dir = new TempDir();
        string dbf = dir.Write("truncated.dbf", """
            [START_MSG] First,256,1,1,1,S,
            [START_SIGNALS] FirstSignal,8,1,0,U,255,0,1,0,1,,,
            [START_MSG] Second,257,1,1,1,S,
            [START_SIGNALS] SecondSignal,8,1,0,U,255,0,1,0,1,,,
            [END_MSG]
            """);
        var messages = DbfFile.Read(dbf);
        Assert.Equal([256u, 257u], messages.Select(m => m.Id));
        Assert.Equal(["FirstSignal", "SecondSignal"], messages.Select(m => Assert.Single(m.Signals).Name));
    }

    [Fact]
    public void Matrix_to_asc_timestamps_continue_after_midnight()
    {
        using var dir = new TempDir();
        string input = dir.Write("midnight.csv", ";00000100;00000101\n23:59:59.980;11;\n00:00:00.000;22;\n");
        string asc = dir.File("out.asc");
        Assert.Equal(2, new MatrixCsvToAscConverter().Convert(input, asc, new ProcessingContext()).RowsWritten);
        Span<int> bytes = stackalloc int[Device.MaxDataLength];
        var times = new List<long>();
        foreach (string line in File.ReadLines(asc))
            if (AscLogParser.TryParseFrameLine(line, out long ticks, out _, bytes, out _)) times.Add(ticks);
        Assert.Equal([0L, TimeSpan.FromMilliseconds(20).Ticks], times);
    }
}
