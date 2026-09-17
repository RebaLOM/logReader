namespace logReader.Tests;

public class CanIdTests
{
    [Theory]
    [InlineData("0CFF0008", "0CFF0008")]
    [InlineData("CFF0008", "0CFF0008")]
    [InlineData("0x0CFF0008", "0CFF0008")]
    [InlineData("0xcff0008", "0CFF0008")]
    [InlineData("1801D0EF", "1801D0EF")]
    [InlineData("123", "123")]
    [InlineData("0x7FF", "7FF")]
    [InlineData("00000100", "00000100")]
    public void TryNormalize_preserves_extended_leading_zero(string raw, string expected)
    {
        Assert.True(CanId.TryNormalize(raw, out string id));
        Assert.Equal(expected, id);
    }

    [Fact]
    public void Format_extended_pads_to_8()
    {
        Assert.Equal("0CFF0008", CanId.Format(0x0CFF0008u, isExtended: true));
        Assert.Equal("CFF0008", CanId.Format(0x0CFF0008u, isExtended: false));
    }

    [Fact]
    public void Format_auto_detects_extended_by_value()
    {
        Assert.Equal("0CFF0008", CanId.Format(0x0CFF0008u));
        Assert.Equal("100", CanId.Format(0x100u));
    }
}
