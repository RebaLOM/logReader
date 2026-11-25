using logReader.Processing;

namespace logReader.Tests;

public class MatrixCsvTimeTests
{
    [RealFixtureFact("LOGER_TEST_DATA_ROOT")]
    public void Sample_file_first_data_row_parses()
    {
        string root = UserFixtureFiles.Root!;
        Assert.True(Directory.Exists(root), "Configured real fixture directory does not exist.");
        var files = UserFixtureFiles.Enumerate(root, ".csv")
            .Where(file => MatrixCsvLogParser.LooksLikeMatrixCsv(file)).ToArray();
        Assert.NotEmpty(files);
        foreach (string file in files)
        {
            string clock = FirstDataRow(file).Split(';')[0].Trim().Replace(',', '.');
            Assert.True(TimeOfDayParse.TryParse(clock, out var parsed), $"Unparsed first timestamp: {file}");
            Assert.Equal(TimeSpan.Parse(clock, System.Globalization.CultureInfo.InvariantCulture), parsed);
        }
    }

    private static string FirstDataRow(string path)
    {
        var encoding = LogFileEncoding.Detect(path);
        using var lines = LogFileReader.ReadLines(path, encoding).GetEnumerator();
        string? firstDataLine = MatrixCsvLogParser.FindHeader(lines)?.FirstDataLine;
        Assert.NotNull(firstDataLine);
        return firstDataLine;
    }
}
