using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ClosedXML.Excel;
using logReader.Processing;

namespace logReader.Tests;

public class DtcOutputTests
{
    private static readonly string[] Ids = ["18FE2503", "18FE2603", "18FE2703"];

    private static int[][] Pack(int firstSpn, int count = 6)
    {
        var bytes = new int[18];
        for (int slot = 0; slot < Math.Min(count, 6); slot++)
        {
            int spn = firstSpn + slot;
            bytes[slot * 3] = spn & 255;
            bytes[slot * 3 + 1] = (spn >> 8) & 255;
            bytes[slot * 3 + 2] = ((spn >> 11) & 0xe0) | slot;
        }
        return [[0, 0, 0, count, 0, 0, bytes[0], bytes[1]], bytes[2..10], bytes[10..18]];
    }

    private static string Hex(IEnumerable<int> values, string separator = "")
        => string.Join(separator, values.Select(v => v.ToString("X2", CultureInfo.InvariantCulture)));

    private static string TrcFrame(int index, double time, string id, IEnumerable<int> bytes)
        => FormattableString.Invariant($"{index}) {time:F3} Rx {id} 8 {Hex(bytes, " ")}\n");

    private static string CycleTrc(int firstSpn = 100, double time = 0)
    {
        var parts = Pack(firstSpn);
        return string.Concat(Enumerable.Range(0, 3).Select(i => TrcFrame(i + 1, time + i, Ids[i], parts[i])));
    }

    private static List<Device> Devices(TempDir dir)
        => DeviceFiles.LoadDevices(dir.Write("devices.dbc", TestData.TwoMessageDbc));

    private static OutputSettings Settings(OutputFormat format = OutputFormat.Csv, bool includeDtc = true, bool idRow = false)
        => new() { Format = format, IncludeDtc = includeDtc, AutoIncludeDtc = false,
            DtcDisplayFormat = DtcDisplayFormat.Detailed, IncludeDeviceIdHeaderRow = idRow };

    private static int[][] PackSlots((int Spn, int Fmi)?[] codes, int count)
    {
        var bytes = new int[18];
        for (int slot = 0; slot < 6; slot++)
        {
            if (codes[slot] is not { } code) continue;
            bytes[slot * 3] = code.Spn & 255;
            bytes[slot * 3 + 1] = (code.Spn >> 8) & 255;
            bytes[slot * 3 + 2] = ((code.Spn >> 11) & 0xe0) | code.Fmi;
        }
        return [[0, 0, 0, count, 0, 0, bytes[0], bytes[1]], bytes[2..10], bytes[10..18]];
    }

    private static string WriteSnapshots(TempDir dir, string kind, IReadOnlyList<int[][]> snapshots)
    {
        string Clock(int ms) => TimeSpan.FromHours(10).Add(TimeSpan.FromMilliseconds(ms)).ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);
        var text = new StringBuilder(kind switch
        {
            "matrix" => ";18FE2703;18FE2503;18FE2603\n",
            "asc" => "date Wed Oct 14 10:00:00.000 2026\nbase hex timestamps absolute\n",
            "trc" => "; Start time: 14.10.2026 10:00:00.000\n",
            _ => ""
        });
        for (int row = 0; row < snapshots.Count; row++)
        {
            var parts = snapshots[row];
            if (kind == "matrix")
            {
                text.AppendLine($"{Clock(row * 200)};{Hex(parts[2])};{Hex(parts[0])};{Hex(parts[1])}");
                continue;
            }
            for (int part = 0; part < 3; part++)
            {
                int ms = row * 200 + part;
                string line = kind switch
                {
                    "trc" => TrcFrame(row * 3 + part + 1, ms, Ids[part], parts[part]).TrimEnd(),
                    "asc" => FormattableString.Invariant($"{ms / 1000d:F3} 1 {Ids[part]}x Rx d 8 {Hex(parts[part], " ")}"),
                    "canfox" => $"2026.10.14 {Clock(ms)} - {Ids[part]} 8 {Hex(parts[part], " ")}",
                    _ => $"{(part == 0 ? (row + 1).ToString(CultureInfo.InvariantCulture) + ";" + Clock(row * 200) : ";")};{Ids[part]};1;{string.Join(';', parts[part])}"
                };
                text.AppendLine(line);
            }
        }
        string extension = kind switch { "matrix" or "step" => ".csv", "canfox" => ".txt", _ => "." + kind };
        return dir.Write("snapshots-" + kind + extension, text.ToString());
    }

    private static string[][] ReadOutput(string path, OutputFormat format, int width)
    {
        if (format == OutputFormat.Csv) return File.ReadAllLines(path).Select(line => line.Split(';')).ToArray();
        using var workbook = new XLWorkbook(path);
        Assert.NotEmpty(workbook.Worksheets);
        string baseName = workbook.Worksheet(1).Name;
        Assert.Contains(baseName, new[] { "Log", "pCAN Log", "ASC Log" });
        int firstSlotColumn = width == 8 ? 3 : 2;
        var rows = new List<string[]>();
        int part = 0;
        foreach (var sheet in workbook.Worksheets)
        {
            // Ordinary XLSX rollover repeats the same headers on Log_2, Log_3…;
            // it must retain exactly six slot columns and every chronological row.
            Assert.Equal(ExcelLayoutBuilder.SheetNameForPart(baseName, ++part), sheet.Name);
            Assert.Equal(width, sheet.LastColumnUsed()!.ColumnNumber());
            string[] header = Enumerable.Range(1, width).Select(column => sheet.Cell(1, column).GetString()).ToArray();
            Assert.Equal(new[] { "DTC1", "DTC2", "DTC3", "DTC4", "DTC5", "DTC6" }, header[(firstSlotColumn - 1)..(firstSlotColumn + 5)]);
            if (rows.Count == 0) rows.Add(header);
            else Assert.Equal(rows[0], header);
            foreach (var row in sheet.RowsUsed().Skip(1))
            {
                for (int slot = 0; slot < 6; slot++)
                    if (row.Cell(firstSlotColumn + slot).GetString().Length > 0)
                        Assert.Equal(XLDataType.Text, row.Cell(firstSlotColumn + slot).DataType);
                rows.Add(Enumerable.Range(1, width).Select(column => row.Cell(column).GetString()).ToArray());
            }
        }
        return rows.ToArray();
    }

    [Fact]
    public void Disabled_dtc_keeps_existing_csv_bytes_and_headers()
    {
        using var dir = new TempDir();
        string input = dir.Write("log.trc", CycleTrc() + TrcFrame(4, 10, "0CFF0008", [17, 0, 0, 0, 0, 0, 0, 0]));
        string output = dir.File("result.csv");
        var result = new LogProcessingService().ProcessSingleFile(input, output, Devices(dir), Settings(includeDtc: false), new ProcessingContext());
        Assert.True(result.Success);
        byte[] expected = CsvOutput.Encoding.GetPreamble().Concat(CsvOutput.Encoding.GetBytes("Время;A1" + Environment.NewLine + "10;17" + Environment.NewLine)).ToArray();
        Assert.Equal(SHA256.HashData(expected), SHA256.HashData(File.ReadAllBytes(output)));
    }

    [Fact]
    public void Matrix_uses_six_fixed_slots_for_more_than_six_unique_codes_and_reordered_columns()
    {
        using var dir = new TempDir();
        var first = Pack(100);
        var second = Pack(200);
        string input = dir.Write("matrix.csv", ";18FE2703;18FE2603;18FE2503;0CFF0008\n" +
            $"10:00:00;{Hex(first[2])};{Hex(first[1])};{Hex(first[0])};1100000000000000\n" +
            $"10:00:01;{Hex(second[2])};{Hex(second[1])};{Hex(second[0])};1200000000000000\n");
        string output = dir.File("out.csv");
        var result = new MatrixCsvLogProcessor().Process(input, Devices(dir), output, Settings(), new ProcessingContext());
        Assert.True(result.Success);
        string[][] rows = File.ReadAllLines(output).Select(l => l.Split(';')).ToArray();
        Assert.Equal(new[] { "DTC1", "DTC2", "DTC3", "DTC4", "DTC5", "DTC6" }, rows[0][3..9]);
        Assert.Equal(9, rows[0].Length);
        Assert.Equal("SPN 100 / FMI 0", rows[1][3]);
        Assert.Equal("SPN 105 / FMI 5", rows[1][8]);
        Assert.Equal("SPN 200 / FMI 0", rows[2][3]);
        Assert.Equal("SPN 205 / FMI 5", rows[2][8]);
        Assert.Equal("17", rows[1][2]);
        Assert.Equal("18", rows[2][2]);
    }

    [Theory]
    [InlineData("matrix", OutputFormat.Csv)]
    [InlineData("matrix", OutputFormat.Xlsx)]
    [InlineData("step", OutputFormat.Csv)]
    [InlineData("step", OutputFormat.Xlsx)]
    [InlineData("trc", OutputFormat.Csv)]
    [InlineData("trc", OutputFormat.Xlsx)]
    [InlineData("asc", OutputFormat.Csv)]
    [InlineData("asc", OutputFormat.Xlsx)]
    [InlineData("canfox", OutputFormat.Csv)]
    [InlineData("canfox", OutputFormat.Xlsx)]
    public void Every_dtc_column_is_reused_by_its_current_wire_slot_when_codes_move(string kind, OutputFormat format)
    {
        using var dir = new TempDir();
        var snapshots = new List<int[][]>();
        var expected = new List<string[]>();
        for (int snapshot = 0; snapshot < 6; snapshot++)
        {
            var bytes = new int[18];
            var fields = new string[6];
            for (int slot = 0; slot < 6; slot++)
            {
                // One unchanged pair visits every slot. Every other pair is new,
                // so the same six columns also have to accommodate >6 unique codes.
                int spn = slot == snapshot ? 32770 : 1000 + snapshot * 10 + slot;
                int fmi = slot == snapshot ? 0 : (snapshot + slot) % 32;
                bytes[slot * 3] = spn & 255;
                bytes[slot * 3 + 1] = (spn >> 8) & 255;
                bytes[slot * 3 + 2] = ((spn >> 11) & 0xe0) | fmi;
                fields[slot] = $"SPN {spn} / FMI {fmi}";
            }
            int[] first = [0, 0, 0, 6, 0, 0, bytes[0], bytes[1]];
            snapshots.Add([first, bytes[2..10], bytes[10..18]]);
            expected.Add(fields);
        }
        string source = WriteSnapshots(dir, kind, snapshots);
        string output = dir.File(format == OutputFormat.Csv ? "result.csv" : "result.xlsx");
        Assert.True(new LogProcessingService().ProcessSingleFile(source, output, [], Settings(format), new ProcessingContext()).Success);
        int firstSlot = kind is "matrix" or "step" ? 2 : 1;
        var rows = ReadOutput(output, format, firstSlot + 6);
        var complete = (kind is "matrix" or "step" ? rows.Skip(1)
            : rows.Skip(1).Where((row, index) => index > 0 && index % 3 == 0)).ToArray();
        Assert.Equal(new[] { "DTC1", "DTC2", "DTC3", "DTC4", "DTC5", "DTC6" }, rows[0][firstSlot..(firstSlot + 6)]);
        Assert.Equal(6, complete.Length);
        for (int snapshot = 0; snapshot < 6; snapshot++)
        {
            Assert.Equal(expected[snapshot], complete[snapshot][firstSlot..(firstSlot + 6)]);
            Assert.Equal("SPN 32770 / FMI 0", complete[snapshot][snapshot + firstSlot]);
        }
        Assert.Equal(31, complete.SelectMany(row => row[firstSlot..(firstSlot + 6)]).Distinct().Count());
        Assert.All(rows, row => Assert.Equal(firstSlot + 6, row.Length));
    }

    [Theory]
    [InlineData("matrix", OutputFormat.Csv)]
    [InlineData("matrix", OutputFormat.Xlsx)]
    [InlineData("step", OutputFormat.Csv)]
    [InlineData("step", OutputFormat.Xlsx)]
    [InlineData("trc", OutputFormat.Csv)]
    [InlineData("trc", OutputFormat.Xlsx)]
    [InlineData("asc", OutputFormat.Csv)]
    [InlineData("asc", OutputFormat.Xlsx)]
    [InlineData("canfox", OutputFormat.Csv)]
    [InlineData("canfox", OutputFormat.Xlsx)]
    public void Sparse_slots_are_not_compacted_or_hidden_by_count_and_can_be_refilled(string kind, OutputFormat format)
    {
        using var dir = new TempDir();
        var snapshots = new List<int[][]>();
        var expected = new List<string[]>();
        for (int slot = 0; slot < 6; slot++)
        {
            var codes = new (int Spn, int Fmi)?[6];
            codes[slot] = (500000 + slot, slot);
            snapshots.Add(PackSlots(codes, 1));
            var fields = new string[6];
            Array.Fill(fields, "");
            fields[slot] = $"SPN {500000 + slot} / FMI {slot}";
            expected.Add(fields);
        }
        snapshots.Add(PackSlots(new (int Spn, int Fmi)?[6], 0));
        expected.Add(["", "", "", "", "", ""]);
        var refill = new (int Spn, int Fmi)?[6];
        refill[5] = (500123, 31);
        snapshots.Add(PackSlots(refill, 1));
        expected.Add(["", "", "", "", "", "SPN 500123 / FMI 31"]);
        string source = WriteSnapshots(dir, kind, snapshots);
        string output = dir.File(format == OutputFormat.Csv ? "result.csv" : "result.xlsx");
        Assert.True(new LogProcessingService().ProcessSingleFile(source, output, [], Settings(format), new ProcessingContext()).Success);
        int firstSlot = kind is "matrix" or "step" ? 2 : 1;
        var rows = ReadOutput(output, format, firstSlot + 6);
        var complete = (kind is "matrix" or "step" ? rows.Skip(1)
            : rows.Skip(1).Where((row, index) => index > 0 && index % 3 == 0)).ToArray();
        Assert.Equal(8, complete.Length);
        for (int snapshot = 0; snapshot < expected.Count; snapshot++)
        {
            Assert.Equal(expected[snapshot], complete[snapshot][firstSlot..(firstSlot + 6)]);
        }
        Assert.Equal(7, complete.SelectMany(row => row[firstSlot..(firstSlot + 6)]).Where(value => value.Length > 0).Distinct().Count());
        Assert.All(rows, row => Assert.Equal(firstSlot + 6, row.Length));
    }

    [Fact]
    public void Matrix_dtc_xlsx_is_text_and_no_extra_report_or_sheet_is_created()
    {
        using var dir = new TempDir();
        var parts = Pack(32770, 1);
        string input = dir.Write("matrix.csv", ";18FE2503;18FE2603\n" + $"10:00:00;{Hex(parts[0])};{Hex(parts[1])}\n");
        string output = dir.File("out.xlsx");
        var result = new MatrixCsvLogProcessor().Process(input, [], output, Settings(OutputFormat.Xlsx, idRow: true), new ProcessingContext());
        Assert.True(result.Success);
        using var workbook = new XLWorkbook(output);
        Assert.Single(workbook.Worksheets);
        var sheet = workbook.Worksheet(1);
        Assert.Equal("DTC", sheet.Cell(1, 3).GetString());
        Assert.Equal("DTC1", sheet.Cell(2, 3).GetString());
        Assert.Equal(XLDataType.Text, sheet.Cell(3, 3).DataType);
        Assert.Equal("SPN 32770 / FMI 0", sheet.Cell(3, 3).GetString());
        Assert.Equal("", sheet.Cell(3, 4).GetString());
        Assert.Equal(new[] { "matrix.csv", "out.xlsx" }, Directory.GetFiles(dir.Path).Select(Path.GetFileName).OrderBy(s => s));
    }

    [Fact]
    public void Matrix_rows_expire_and_fragments_do_not_cross_row_boundaries()
    {
        using var dir = new TempDir();
        var parts = Pack(100, 1);
        string input = dir.Write("matrix.csv", ";18FE2503;18FE2603\n" +
            $"10:00:00;{Hex(parts[0])};{Hex(parts[1])}\n" +
            "10:00:01;;\n10:00:03;;\n" +
            $"10:00:04;{Hex(parts[0])};\n10:00:05;;{Hex(parts[1])}\n");
        string output = dir.File("out.csv");
        Assert.True(new MatrixCsvLogProcessor().Process(input, [], output, Settings(), new ProcessingContext()).Success);
        var rows = File.ReadAllLines(output).Select(l => l.Split(';')).ToArray();
        Assert.Equal("SPN 100 / FMI 0", rows[1][2]);
        Assert.Equal("SPN 100 / FMI 0", rows[2][2]);
        Assert.Equal("", rows[3][2]);
        Assert.Equal("", rows[4][2]);
        Assert.Equal("", rows[5][2]);
    }

    [Fact]
    public void Shared_row_clock_keeps_state_through_midnight_then_expires()
    {
        var row = new DtcRowCollector(2000);
        var parts = Pack(100, 1);
        row.Add(Ids[0], parts[0]);
        row.Add(Ids[1], parts[1]);
        Assert.Equal("SPN 100 / FMI 0", row.Finish(86399500).Slots[0].Text);
        Assert.Equal("SPN 100 / FMI 0", row.Finish(500).Slots[0].Text);
        Assert.Equal("Н/Д", row.Finish(2500).Slots[0].Text);
    }

    [Fact]
    public void Legacy_step_groups_reordered_parts_at_the_step_time()
    {
        using var dir = new TempDir();
        var parts = Pack(100);
        string input = dir.Write("step.csv", string.Concat(new[] { 2, 1, 0 }.Select((part, i) =>
            $"{(i == 0 ? "1;10:00:00" : ";")};{Ids[part]};1;{string.Join(';', parts[part])}\n")) +
            "2;10:00:03;0CFF0008;1;18;0;0;0;0;0;0;0\n");
        string output = dir.File("out.csv");
        Assert.True(new CanLogProcessor().Process(input, Devices(dir), output, Settings(), new ProcessingContext()).Success);
        var rows = File.ReadAllLines(output).Select(l => l.Split(';')).ToArray();
        Assert.Equal("SPN 100 / FMI 0", rows[1][3]);
        Assert.Equal("SPN 105 / FMI 5", rows[1][8]);
        Assert.Equal("", rows[2][3]);
        Assert.Equal("18", rows[2][2]);
    }

    [Fact]
    public void Invalid_legacy_clock_does_not_reuse_a_previous_dtc_timestamp()
    {
        using var dir = new TempDir();
        var parts = Pack(100, 1);
        string input = dir.Write("step.csv", $"1;10:00:00;{Ids[0]};1;{string.Join(';', parts[0])}\n" +
            $";;{Ids[1]};1;{string.Join(';', parts[1])}\n" +
            "2;BAD_TIME;0CFF0008;1;18;0;0;0;0;0;0;0\n");
        string output = dir.File("out.csv");
        Assert.True(new CanLogProcessor().Process(input, Devices(dir), output, Settings(), new ProcessingContext()).Success);
        var rows = File.ReadAllLines(output).Select(l => l.Split(';')).ToArray();
        Assert.Equal("SPN 100 / FMI 0", rows[1][3]);
        Assert.Equal("", rows[2][3]);
        Assert.Equal("18", rows[2][2]);
    }

    [Fact]
    public void Truncated_legacy_first_part_invalidates_previous_complete_state()
    {
        using var dir = new TempDir();
        var parts = Pack(100, 1);
        string input = dir.Write("step.csv", $"1;10:00:00;{Ids[0]};1;{string.Join(';', parts[0])}\n" +
            $";;{Ids[1]};1;{string.Join(';', parts[1])}\n" + "2;10:00:01;18FE2503;1;0;0\n");
        string output = dir.File("out.csv");
        Assert.True(new CanLogProcessor().Process(input, [], output, Settings(), new ProcessingContext()).Success);
        var rows = File.ReadAllLines(output).Select(l => l.Split(';')).ToArray();
        Assert.Equal("SPN 100 / FMI 0", rows[1][2]);
        Assert.Equal("", rows[2][2]);
        Assert.Equal(8, rows[2].Length);
    }

    [Theory]
    [InlineData("trc")]
    [InlineData("asc")]
    [InlineData("txt")]
    public void Frame_formats_keep_dtc_on_an_independent_time_axis_and_expire(string extension)
    {
        using var dir = new TempDir();
        var parts = Pack(100);
        string inputText = extension switch
        {
            "trc" => CycleTrc() + TrcFrame(4, 5000, "0CFF0008", [17, 0, 0, 0, 0, 0, 0, 0]),
            "asc" => "date Wed Apr 26 10:00:00.000 2023\nbase hex timestamps absolute\n" +
                string.Concat(Enumerable.Range(0, 3).Select(i => FormattableString.Invariant($"{i / 1000.0:F6} 1 {Ids[i]}x Rx d 8 {Hex(parts[i], " ")}\n"))) +
                "5.000000 1 0CFF0008x Rx d 8 11 00 00 00 00 00 00 00\n",
            _ => string.Concat(Enumerable.Range(0, 3).Select(i => $"2023.04.26 10:00:00.{i:000} - {Ids[i]} 8 {Hex(parts[i], " ")}\n")) +
                "2023.04.26 10:00:05.000 - 0CFF0008 8 11 00 00 00 00 00 00 00\n"
        };
        string input = dir.Write("log." + extension, inputText);
        string output = dir.File("out.csv");
        var result = new LogProcessingService().ProcessSingleFile(input, output, Devices(dir), Settings(), new ProcessingContext());
        Assert.True(result.Success, result.Error);
        var rows = File.ReadAllLines(output).Select(l => l.Split(';')).ToArray();
        Assert.Equal("Время", rows[0][0]);
        Assert.Equal("Время DTC", rows[0][2]);
        Assert.Equal("17", rows[1][1]);
        Assert.NotEqual(rows[1][0], rows[1][2]);
        Assert.Contains(rows.Skip(1), r => r[3] == "SPN 100 / FMI 0" && r[8] == "SPN 105 / FMI 5");
        Assert.Equal("", rows[^1][3]);
        Assert.All(rows[^1][3..9], value => Assert.Equal("", value));
    }

    [Fact]
    public void Time_series_xlsx_dtc_only_uses_text_cells_on_the_existing_sheet()
    {
        using var dir = new TempDir();
        string input = dir.Write("log.trc", CycleTrc());
        string output = dir.File("out.xlsx");
        Assert.True(new PCanLogProcessor().Process(input, [], output, Settings(OutputFormat.Xlsx), new ProcessingContext()).Success);
        using var workbook = new XLWorkbook(output);
        Assert.Single(workbook.Worksheets);
        var sheet = workbook.Worksheet(1);
        Assert.Equal("Время DTC", sheet.Cell(1, 1).GetString());
        var value = sheet.CellsUsed().Single(c => c.GetString() == "SPN 105 / FMI 5");
        Assert.Equal(XLDataType.Text, value.DataType);
        Assert.Equal(7, sheet.LastColumnUsed()!.ColumnNumber());
    }

    [Fact]
    public void Merged_matrix_and_trc_reset_dtc_between_files()
    {
        using var dir = new TempDir();
        var parts = Pack(100, 1);
        string first = dir.Write("a.csv", ";18FE2503;18FE2603\n" + $"10:00:00;{Hex(parts[0])};{Hex(parts[1])}\n");
        string second = dir.Write("b.csv", ";18FE2503;18FE2603\n10:00:00.001;;\n");
        string matrixOutput = dir.File("matrix.csv");
        Assert.True(new MatrixCsvLogProcessor().ProcessMerged([first, second], [], matrixOutput, Settings(), new ProcessingContext()).Success);
        var matrixRows = File.ReadAllLines(matrixOutput).Select(l => l.Split(';')).ToArray();
        Assert.Equal("SPN 100 / FMI 0", matrixRows[1][2]);
        Assert.Equal("", matrixRows[2][2]);

        string firstTrc = dir.Write("a.trc", CycleTrc());
        string secondTrc = dir.Write("b.trc", TrcFrame(1, 3, "0CFF0008", [17, 0, 0, 0, 0, 0, 0, 0]));
        Assert.True(TrcBatchAggregator.TryBuildMergedAggregate([firstTrc, secondTrc], [], Settings(), new ProcessingContext(), out var aggregate).Success);
        Assert.NotNull(aggregate!.DtcRows);
        Assert.Equal("Н/Д", aggregate.DtcRows![^1].Snapshot.Slots[0].Text);
    }

    [Fact]
    public void Date_split_keeps_same_file_dtc_fragments_across_midnight()
    {
        using var dir = new TempDir();
        var parts = Pack(100, 1);
        string input = dir.Write("midnight.trc", "; Start time: 26.04.2023 23:59:59.990.0\n" +
            TrcFrame(1, 5, Ids[0], parts[0]) + TrcFrame(2, 15, Ids[1], parts[1]));
        var result = TrcBatchAggregator.TryBuildAggregatesByDate([input], [], Settings(), new ProcessingContext(), out var dates);
        Assert.True(result.Success, result.Error);
        Assert.Equal(2, dates.Count);
        Assert.Equal("SPN 100 / FMI 0", dates[new DateOnly(2023, 4, 27)].DtcRows![^1].Snapshot.Slots[0].Text);
    }

    [Fact]
    public void Dst_block_rows_use_state_at_block_end_and_mark_expiry()
    {
        using var dir = new TempDir();
        var parts = Pack(100, 1);
        string input = dir.Write("blocks.trc", TrcFrame(1, 0, Ids[0], parts[0]) + TrcFrame(2, 1, Ids[1], parts[1]) +
            TrcFrame(3, 2, "0CFF0008", [17, 0, 0, 0, 0, 0, 0, 0]) +
            TrcFrame(4, 20, "0CFF0008", [18, 0, 0, 0, 0, 0, 0, 0]) +
            TrcFrame(5, 3000, "0CFF0008", [19, 0, 0, 0, 0, 0, 0, 0]));
        string output = dir.File("dst.csv");
        var settings = new OutputSettings { Format = OutputFormat.CsvDstConnect, IncludeDtc = true,
            DtcDisplayFormat = DtcDisplayFormat.Detailed, DstConnect = new DstConnectOptions { BlockPeriodMs = 20 } };
        var result = new DstConnectTrcProcessor().Process(input, Devices(dir), output, settings, new ProcessingContext());
        Assert.True(result.Success, result.Error);
        var rows = File.ReadAllLines(output).Select(l => l.Split(';')).ToArray();
        Assert.Equal("DTC1", rows[0][3]);
        Assert.Contains(rows.Skip(1), r => r[3] == "SPN 100 / FMI 0");
        Assert.Equal("", rows[^1][3]);
        Assert.Equal("19", rows[^1][2]);
    }

    [Fact]
    public void Counter_above_six_is_partial_and_zero_confirms_empty_slots()
    {
        using var dir = new TempDir();
        var many = Pack(100, 7);
        var empty = Pack(100, 0);
        string input = dir.Write("matrix.csv", ";18FE2503;18FE2603;18FE2703\n" +
            $"10:00:00;{Hex(many[0])};{Hex(many[1])};{Hex(many[2])}\n" +
            $"10:00:01;{Hex(empty[0])};{Hex(empty[1])};{Hex(empty[2])}\n");
        string output = dir.File("out.csv");
        Assert.True(new MatrixCsvLogProcessor().Process(input, [], output, Settings(), new ProcessingContext()).Success);
        var rows = File.ReadAllLines(output).Select(l => l.Split(';')).ToArray();
        Assert.Equal(8, rows[0].Length);
        Assert.Equal("SPN 105 / FMI 5", rows[1][7]);
        Assert.All(rows[2][2..8], value => Assert.Equal("", value));
        Assert.Equal(8, rows[2].Length);
    }

    [Fact]
    public void Fractional_dtc_milliseconds_use_the_same_csv_locale_as_numeric_parameters()
    {
        using var dir = new TempDir();
        string input = dir.Write("log.trc", CycleTrc(100, 27.486) +
            TrcFrame(4, 41.125, "0CFF0008", [17, 0, 0, 0, 0, 0, 0, 0]));
        string output = dir.File("out.csv");
        Assert.True(new LogProcessingService().ProcessSingleFile(input, output, Devices(dir), Settings(), new ProcessingContext()).Success);
        var rows = File.ReadAllLines(output).Select(line => line.Split(';')).ToArray();
        Assert.Equal("41,125", rows[1][0]);
        Assert.Equal("27,486", rows[1][2]);
        Assert.Contains(rows.Skip(1), row => row[2] == "29,486" && row[3] == "SPN 100 / FMI 0");
    }

    [Fact]
    public void Cancellation_with_dtc_preserves_previous_output()
    {
        using var dir = new TempDir();
        string input = dir.Write("matrix.csv", ";18FE2503;18FE2603\n10:00:00;0000000100006400;0000000000000000\n");
        string output = dir.Write("out.csv", "PREVIOUS");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => new MatrixCsvLogProcessor().Process(input, [], output, Settings(),
            new ProcessingContext(cancellationToken: cancellation.Token)));
        Assert.Equal("PREVIOUS", File.ReadAllText(output));
        Assert.Empty(Directory.GetFiles(dir.Path, "*.tmp*"));
    }
}
