using logReader.Processing;

namespace logReader.Tests;

public class TrcFormatTests
{
    [Theory]
    [InlineData("0 21.092 Rx 18031603 8 BC 02 5A 50 01 BE 8C FF", 0, "21.092", "Rx", "18031603", 188)]
    [InlineData("7 1059,900 tX 0300 8 01 02 03 04 05 06 07 08", 7, "1059.900", "Tx", "300", 1)]
    public void Dst_frames_without_parentheses_preserve_index_time_direction_id_and_payload(
        string line, int index, string milliseconds, string direction, string id, int firstByte)
    {
        Span<int> bytes = stackalloc int[64];
        Assert.True(TrcLogParser.TryParseTrcFrameLine(line, out int actualIndex, out decimal time,
            out string actualDirection, out string actualId, out int dlc, bytes, out int count));
        Assert.Equal(index, actualIndex);
        Assert.Equal(decimal.Parse(milliseconds, System.Globalization.CultureInfo.InvariantCulture), time);
        Assert.Equal(direction, actualDirection);
        Assert.Equal(id, actualId);
        Assert.Equal(8, dlc);
        Assert.Equal(8, count);
        Assert.Equal(firstByte, bytes[0]);
        Assert.Equal(id == "300" ? 8 : 255, bytes[7]);
    }

    [Theory]
    [InlineData("0 21.092 Rx 18031603 1 RTR")]
    [InlineData("0 21.092 Rx invalid 8 01 02 03 04 05 06 07 08")]
    [InlineData("0 21.092 Rx 18031603 8 01 02")]
    [InlineData("0 21.092 Rx 18031603 -1 01")]
    [InlineData("0 invalid Rx 18031603 1 01")]
    public void Invalid_dst_frames_are_skipped(string line)
    {
        Span<int> bytes = stackalloc int[64];
        Assert.False(TrcLogParser.TryParseTrcFrameLine(line, out _, out _, out _, out _, out _, bytes, out _));
    }

    [Fact]
    public void Plain_dst_header_provides_start_time_and_stops_before_frames()
    {
        string[] header = { "ООО \"ДСТ-УРАЛ\"   Сайт: tm10.ru", "Время начала записи: 16.10.2025 06:41:35.235", "0 21.092 Rx 18031603 0" };
        Assert.Equal(new DateTime(2025, 10, 16, 6, 41, 35, 235), TrcLogParser.ParseStartTime(header));
        Assert.Null(TrcLogParser.ParseStartTime(new[] { header[0], header[2], header[1] }));
    }

    [Fact]
    public void Unknown_plain_header_is_not_scanned_without_a_limit()
    {
        int visited = 0;
        IEnumerable<string> Lines()
        {
            for (int i = 0; i < 10000; i++) { visited++; yield return "unknown header"; }
        }
        Assert.Null(TrcLogParser.ParseStartTime(Lines()));
        Assert.InRange(visited, 1, 65);
    }

    [Theory]
    [InlineData("     1)      1841  0300  8  01 02 03 04 05 06 07 08", "1.0")]
    [InlineData("     1)      1059.9  Rx         0300  8  01 02 03 04 05 06 07 08", "1.1")]
    [InlineData("     1)      1059.900 1  Rx        0300  8  01 02 03 04 05 06 07 08", "1.2")]
    [InlineData("     1)      1059.900 1  Rx        0300 -  8    01 02 03 04 05 06 07 08", "1.3")]
    [InlineData("      1      1059.900 DT     0300 Rx 8    01 02 03 04 05 06 07 08", "2.0")]
    [InlineData("      1      1059.900 DT 1      0300 Rx -  8    01 02 03 04 05 06 07 08", "2.1")]
    public void All_trc_versions_are_parsed(string line, string version)
    {
        Span<int> bytes = stackalloc int[64];

        Assert.True(TrcLogParser.TryParseTrcFrameLine(line, out int index, out _, out string dir, out string id, out int dlc, bytes, out int n), version);
        Assert.Equal(1, index);
        Assert.Equal("Rx", dir);
        Assert.Equal("300", id);
        Assert.Equal(8, dlc);
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7, 8 }, bytes[..n].ToArray());
    }

    [Fact]
    public void Trc_2_1_can_fd_dlc_code_is_converted_to_length()
    {
        string data = string.Join(" ", Enumerable.Range(0, 12).Select(i => i.ToString("X2")));
        string line = $"      7      1059.900 FD 1      18FF0101 Tx -  9    {data}";
        Span<int> bytes = stackalloc int[64];

        Assert.True(TrcLogParser.TryParseTrcFrameLine(line, out _, out _, out string dir, out string id, out int length, bytes, out int n));
        Assert.Equal("Tx", dir);
        Assert.Equal("18FF0101", id);
        Assert.Equal(12, length);
        Assert.Equal(12, n);
    }

    [Theory]
    [InlineData("     2)      1059.9  Rx         0300  1  RTR")]
    [InlineData("     3)      1059.9  Warng      00000004  4  00 00 00 08 BUSHEAVY")]
    [InlineData("      4      1059.900 RR     0300 Rx 1")]
    [InlineData("      5      1059.900 ER     0300 Rx 2  00 00")]
    [InlineData("     6)      1059.9  Rx         0300  8  01 02")]
    public void Non_data_and_incomplete_frames_are_skipped(string line)
    {
        Span<int> bytes = stackalloc int[64];
        Assert.False(TrcLogParser.TryParseTrcFrameLine(line, out _, out _, out _, out _, out _, bytes, out _));
    }

    [Fact]
    public void Start_time_header_is_read_without_scanning_frames()
    {
        var lines = new[] { ";$FILEVERSION=1.1", ";   Start time: 26.04.2023 13:56:32.811.0", "     1)        10.0  Rx     0300  0" };
        Assert.Equal(new DateTime(2023, 4, 26, 13, 56, 32, 811), TrcLogParser.ParseStartTime(lines));

        var noHeader = new[] { "     1)        10.0  Rx     0300  0", ";   Start time: 26.04.2023 13:56:32.811.0" };
        Assert.Null(TrcLogParser.ParseStartTime(noHeader));
    }
}
