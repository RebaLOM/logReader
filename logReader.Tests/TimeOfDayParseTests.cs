namespace logReader.Tests;

public class TimeOfDayParseTests
{
    [Theory]
    [InlineData("11:46:22", 11, 46, 22, 0)]
    [InlineData("11:46:22.2", 11, 46, 22, 200)]
    [InlineData("11:46:22.257", 11, 46, 22, 257)]
    [InlineData("0:00:00.001", 0, 0, 0, 1)]
    [InlineData("23:59:59.999", 23, 59, 59, 999)]
    public void TryParse_accepts_milliseconds(string raw, int h, int m, int s, int ms)
    {
        Assert.True(TimeOfDayParse.TryParse(raw, out var t));
        Assert.Equal(h, (int)Math.Floor(t.TotalHours));
        Assert.Equal(m, t.Minutes);
        Assert.Equal(s, t.Seconds);
        Assert.Equal(ms, t.Milliseconds);
    }

    [Fact]
    public void TryParse_rejects_plain_integer_seconds_overflow()
    {
        Assert.False(TimeOfDayParse.TryParse("11:46:99", out _));
    }
}

public class NumberParseHelperTests
{
    [Theory]
    [InlineData("1000", 1000)]
    [InlineData("1 000", 1000)]
    [InlineData("12 345.6", 12345.6)]
    [InlineData("-1 000", -1000)]
    [InlineData("1\u00A0000", 1000)]
    public void ParseDoubleInvariant_strips_thousand_spaces(string raw, double expected)
    {
        Assert.Equal(expected, NumberParseHelper.ParseDoubleInvariant(raw), precision: 5);
    }
}
