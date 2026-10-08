using ClosedXML.Excel;

namespace logReader.Tests;

public class XlsxDescriptionTests
{
    [Fact]
    public void Text_cell_with_decimal_comma_is_not_a_thousands_separator()
    {
        using var dir = new TempDir();
        string path = TestData.DevicesXlsx(dir, "devices.xlsx", ws =>
        {
            TestData.SetRow(ws, 2, "0CFF0008", "Msg", 1, 8, 0, "Speed", "NUM", 0, 16, "Intel", "+");
            ws.Cell(2, 12).SetValue("0,1");
            ws.Cell(2, 13).SetValue("-40,5");
        });

        var row = DeviceExcelFile.ReadAllDevices(path)[0].Rows[0];

        Assert.Equal(0.1, row.Scale, precision: 9);
        Assert.Equal(-40.5, row.Offset, precision: 9);
    }

    [Fact]
    public void Composite_source_ids_are_normalized_like_log_ids()
    {
        using var dir = new TempDir();
        string path = dir.File("composites.xlsx");
        using (var wb = new XLWorkbook())
        {
            var ws = wb.Worksheets.Add("Composites");
            TestData.SetRow(ws, 2, "B", "P", 0, "CFF0008", 0, 0, 8);
            TestData.SetRow(ws, 3, "B", "P", 1, "0x18ff0101", 1, 0, 8, 1);
            wb.SaveAs(path);
        }

        var runtime = DeviceFiles.LoadComposites(path);

        Assert.True(runtime.IsSourceId("0CFF0008"));
        Assert.True(runtime.IsSourceId("18FF0101"));
        Assert.True(runtime.IsTriggerId("18FF0101"));
    }

    [Fact]
    public void Saving_devices_keeps_other_sheets_custom_columns_and_note_rows()
    {
        using var dir = new TempDir();
        string path = TestData.DevicesXlsx(dir, "devices.xlsx", ws =>
        {
            ws.Cell(1, 18).Value = "Comment";
            TestData.SetRow(ws, 2, "0CFF0008", "Msg", 1, 8, 0, "Speed", "NUM", 0, 16, "Intel", "+", 0.1, 0, "km/h");
            ws.Cell(2, 18).Value = "проверено на стенде";
            TestData.SetRow(ws, 3, "0CFF0008", "Msg", 1, 8, 1, "Flags", "BIN", 2, 3, null, null, null, null, "bit", null, null, 4);
            TestData.SetRow(ws, 4, null, "— заметка: сигналы ниже не трогать —");
            ws.Workbook.Worksheets.Add("Notes").Cell(1, 1).Value = "история изменений";
        });

        var devices = DeviceExcelFile.ReadAllDevices(path);
        devices[0].Rows.Reverse();
        DeviceExcelFile.WriteAllDevices(path, devices);

        using var wb = new XLWorkbook(path);
        Assert.Equal("история изменений", wb.Worksheet("Notes").Cell(1, 1).GetString());
        var sheet = wb.Worksheet(1);
        Assert.Equal("Comment", sheet.Cell(1, 18).GetString());
        Assert.Equal("Flags", sheet.Cell(2, 6).GetString());
        Assert.Equal("bit", sheet.Cell(2, 14).GetString());
        Assert.Equal("Speed", sheet.Cell(3, 6).GetString());
        Assert.Equal("проверено на стенде", sheet.Cell(3, 18).GetString());
        Assert.Equal("— заметка: сигналы ниже не трогать —", sheet.Cell(4, 2).GetString());
    }

    [Fact]
    public void Saving_composites_keeps_other_sheets_and_reports_invalid_rows()
    {
        using var dir = new TempDir();
        string path = dir.File("composites.xlsx");
        using (var wb = new XLWorkbook())
        {
            var ws = wb.Worksheets.Add("Composites");
            TestData.SetRow(ws, 2, "B", "P", 0, "0CFF0008", 0, 0, 8);
            TestData.SetRow(ws, 3, "B", "Bad", 0, "0CFF0008", 9, 0, 8);
            wb.Worksheets.Add("Readme").Cell(1, 1).Value = "описание";
            wb.SaveAs(path);
        }

        var skipped = new List<int>();
        var signals = CompositeExcelFile.ReadAll(path, null, skipped);
        CompositeExcelFile.WriteAll(path, signals);

        Assert.Equal(new[] { 3 }, skipped);
        using var reread = new XLWorkbook(path);
        Assert.Equal("описание", reread.Worksheet("Readme").Cell(1, 1).GetString());
        Assert.Single(CompositeExcelFile.ReadAll(path));
    }

    [Fact]
    public void Composite_runtime_normalizes_signals_created_in_code()
    {
        var sig = new CompositeSignal { Param = "P", Pieces = { new CompositePiece("cff0008", 0, 0, 8) } };

        var runtime = CompositeRuntime.Build(new[] { sig });

        Assert.True(runtime.IsSourceId("0CFF0008"));
        Assert.True(runtime.IsTriggerId("0CFF0008"));
    }
}
