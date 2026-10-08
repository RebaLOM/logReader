using ClosedXML.Excel;

namespace logReader.Tests;

// Временные файлы для тестов: каждый тест получает свою папку.
public sealed class TempDir : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("logreader-tests-").FullName;

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public string Write(string name, string content)
    {
        string path = File(name);
        System.IO.File.WriteAllText(path, content.Replace("\r\n", "\n"));
        return path;
    }

    public void Dispose() => Directory.Delete(Path, recursive: true);
}

internal static class TestData
{
    // Две extended-посылки 0CFF0008 (A1) и 0CFF0009 (B1), по одному байтовому сигналу.
    public const string TwoMessageDbc =
        "BO_ 2365521928 A: 8 X\n SG_ A1 : 0|8@1+ (1,0) [0|255] \"\" X\n" +
        "BO_ 2365521929 B: 8 X\n SG_ B1 : 0|8@1+ (1,0) [0|255] \"\" X\n";

    public static string DevicesXlsx(TempDir dir, string name, Action<IXLWorksheet> fillRows)
    {
        string path = dir.File(name);
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Devices");
        for (int c = 0; c < DeviceExcelFile.HeaderRow.Length; c++)
            ws.Cell(1, c + 1).Value = DeviceExcelFile.HeaderRow[c];
        fillRows(ws);
        wb.SaveAs(path);
        return path;
    }

    public static void SetRow(IXLWorksheet ws, int row, params object?[] values)
    {
        for (int c = 0; c < values.Length; c++)
            if (values[c] != null)
                ws.Cell(row, c + 1).Value = XLCellValue.FromObject(values[c]);
    }
}
