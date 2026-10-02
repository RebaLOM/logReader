using logReader.Processing;

namespace logReader.Tests;

public class AscFormatTests
{
    [Theory]
    [InlineData("date Wed Apr 26 01:56:32.811 pm 2023", "13:56:32.811")]
    [InlineData("date Wed Apr 26 12:10:00.000 am 2023", "00:10:00")]
    [InlineData("date Wed Apr 26 12:10:00.000 pm 2023", "12:10:00")]
    [InlineData("date Mi Apr 26 13:56:32.811 2023", "13:56:32.811")]
    public void Date_header_respects_am_pm(string line, string expected)
    {
        Assert.True(AscLogParser.TryParseBaseTimeTicksFromHeaderLine(line, out long ticks));
        Assert.Equal(TimeSpan.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), new TimeSpan(ticks));
    }

    [Fact]
    public void Short_standard_ids_are_parsed()
    {
        Span<int> bytes = stackalloc int[64];
        Assert.True(AscLogParser.TryParseFrameLine("   0.100000 1  64              Rx   d 2 01 02", out _, out string id, bytes, out int n));
        Assert.Equal("64", id);
        Assert.Equal(2, n);
    }

    [Fact]
    public void Remote_and_error_frames_are_skipped()
    {
        Span<int> bytes = stackalloc int[64];
        Assert.False(AscLogParser.TryParseFrameLine("   0.100000 1  123             Rx   r", out _, out _, bytes, out _));
        Assert.False(AscLogParser.TryParseFrameLine("   0.100000 1  ErrorFrame", out _, out _, bytes, out _));
    }

    [Fact]
    public void Decimal_base_and_relative_timestamps_are_honoured()
    {
        using var dir = new TempDir();
        string asc = dir.Write("dec.asc",
            "date Wed Apr 26 10:00:00.000 2023\nbase dec  timestamps relative\n" +
            "   0.500000 1  218038280x      Rx   d 8 01 00 00 00 00 00 00 00\n" +
            "   0.250000 1  218038280x      Rx   d 8 02 00 00 00 00 00 00 00\n");
        var devices = DeviceFiles.LoadDevices(dir.Write("devices.dbc", TestData.TwoMessageDbc));
        string output = dir.File("dec.csv");

        var result = new AscLogProcessor().Process(asc, devices, output,
            new OutputSettings { Format = OutputFormat.Csv }, new ProcessingContext());

        Assert.True(result.Success);
        Assert.Equal(new[] { "Время;A1", "10:00:00.500;1", "10:00:00.750;2" }, File.ReadAllLines(output));
    }

    [Fact]
    public void Writer_declares_actual_length_and_round_trips_short_frames()
    {
        Span<int> data = stackalloc int[] { 0x11, 0x22 };
        string line = VectorCanFdAscWriter.BuildFrameLine(0.01, "Rx", "0CFF0008", data, 2);

        Assert.Contains(" 0 0 2 2 11 22 ", line);
        Span<int> bytes = stackalloc int[64];
        Assert.True(AscLogParser.TryParseFrameLine(line, out _, out string id, bytes, out int n));
        Assert.Equal("0CFF0008", id);
        Assert.Equal(2, n);
        Assert.Equal(new[] { 0x11, 0x22 }, bytes[..n].ToArray());
    }

    [Fact]
    public void Writer_pads_can_fd_payload_to_valid_length()
    {
        var data = Enumerable.Range(1, 20).ToArray();
        string line = VectorCanFdAscWriter.BuildFrameLine(0, "Tx", "18FF0101", data, data.Length);

        Assert.Contains(" 0 0 11 20 ", line);
        Span<int> bytes = stackalloc int[64];
        Assert.True(AscLogParser.TryParseFrameLine(line, out _, out _, bytes, out int n));
        Assert.Equal(20, n);
        Assert.Equal(data, bytes[..n].ToArray());
    }

    [Fact]
    public void Trc_to_asc_keeps_frames_with_short_dlc()
    {
        using var dir = new TempDir();
        string trc = dir.Write("short.trc",
            ";   Start time: 26.04.2023 13:56:32.811.0\n" +
            "     1)        10.0  Rx     0CFF0008  2  11 22\n" +
            "     2)        20.0  Rx     0CFF0009  8  01 02 03 04 05 06 07 08\n");
        string asc = dir.File("short.asc");

        Assert.True(new TrcToAscConverter().Convert(trc, asc, new ProcessingContext()).Success);

        var bytes = new int[64];
        var frames = File.ReadLines(asc).Count(l => AscLogParser.TryParseFrameLine(l, out _, out _, bytes, out _));
        Assert.Equal(2, frames);
    }

    [Fact]
    public void Signal_beyond_eighth_byte_is_decoded_from_can_fd_payload()
    {
        using var dir = new TempDir();
        string dbc = dir.Write("fd.dbc",
            "BO_ 2365521928 Fd: 64 X\n SG_ Tail : 496|16@1+ (1,0) [0|65535] \"\" X\n");
        var device = DeviceFiles.LoadDevices(dbc).Single();
        var payload = new int[64];
        payload[62] = 0x34;
        payload[63] = 0x12;

        device.SetPayload(payload);
        device.Decode();

        Assert.Equal(0x1234, device.Values[0]);
    }
}
