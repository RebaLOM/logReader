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
    public void Composite_runtime_normalizes_signals_created_in_code()
    {
        var sig = new CompositeSignal { Param = "P", Pieces = { new CompositePiece("cff0008", 0, 0, 8) } };

        var runtime = CompositeRuntime.Build(new[] { sig });

        Assert.True(runtime.IsSourceId("0CFF0008"));
        Assert.True(runtime.IsTriggerId("0CFF0008"));
    }
}
