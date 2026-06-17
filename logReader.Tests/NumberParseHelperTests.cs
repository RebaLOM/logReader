using logReader;

namespace logReader.Tests
{
    public class NumberParseHelperTests
    {
        [Theory]
        [InlineData("3.14", 3.14)]
        [InlineData("3,14", 3.14)]
        [InlineData("  42 ", 42)]
        [InlineData("-0.5", -0.5)]
        public void TryParseDouble_ParsesValidNumbers(string input, double expected)
        {
            Assert.True(NumberParseHelper.TryParseDouble(input, out double value));
            Assert.Equal(expected, value, 10);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        [InlineData("abc")]
        public void TryParseDouble_IsStrictForEmptyOrGarbage(string? input)
        {
            Assert.False(NumberParseHelper.TryParseDouble(input, out double value));
            Assert.Equal(0, value);
        }

        [Fact]
        public void TryParseOrDefault_UsesFallbackForEmpty()
        {
            Assert.True(NumberParseHelper.TryParseOrDefault("", 5.0, out double value));
            Assert.Equal(5.0, value, 10);
        }

        [Fact]
        public void TryParseOrDefault_ParsesNonEmpty()
        {
            Assert.True(NumberParseHelper.TryParseOrDefault("2.5", 5.0, out double value));
            Assert.Equal(2.5, value, 10);
        }

        [Fact]
        public void TryParseOrDefault_FailsOnGarbageEvenWithFallback()
        {
            Assert.False(NumberParseHelper.TryParseOrDefault("abc", 5.0, out _));
        }

        [Fact]
        public void ParseDoubleInvariant_NormalisesComma()
        {
            Assert.Equal(1.5, NumberParseHelper.ParseDoubleInvariant("1,5"), 10);
        }
    }
}
