using logReader.Processing;

namespace logReader.Tests;

public class MatrixCsvTimeTests
{
    [Fact]
    public void Sample_file_first_data_row_parses()
    {
        if (UserFixtureFiles.Root is { Length: > 0 } root)
        {
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
            Console.WriteLine($"REAL FIXTURES: first timestamps checked in {files.Length} matrix CSV files.");
            return;
        }
        string path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads",
            "TimeFormatCSV_2121_0_20260527T114622.csv");

        if (!File.Exists(path))
            return;

        string timeCell = FirstDataRow(path).Split(';')[0];
        Assert.True(TimeOfDayParse.TryParse(timeCell, out var t));
        Assert.Equal(257, t.Milliseconds);
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
