namespace logReader.Tests;

public class MatrixCsvTimeTests
{
    [Fact]
    public void Sample_file_first_data_row_parses()
    {
        string path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads",
            "TimeFormatCSV_2121_0_20260527T114622.csv");

        if (!File.Exists(path))
            return;

        string? firstData = File.ReadLines(path)
            .Skip(1)
            .FirstOrDefault(l => !string.IsNullOrWhiteSpace(l));

        Assert.NotNull(firstData);
        string timeCell = firstData!.Split(';')[0];
        Assert.True(TimeOfDayParse.TryParse(timeCell, out var t));
        Assert.Equal(257, t.Milliseconds);
    }
}
