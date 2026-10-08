using logReader;

namespace logReader.Tests
{
    public class CsvNumberFormatTests
    {
        [Theory]
        [InlineData("12.5", "12,5")]
        [InlineData("0.5", "0,5")]
        [InlineData("-12.34", "-12,34")]
        [InlineData("1000", "1000")]
        [InlineData("1234567.89", "1234567,89")]
        public void Format_string_uses_ru_decimal_comma(string input, string expected)
        {
            Assert.Equal(expected, CsvNumberFormat.Format(input));
        }

        [Fact]
        public void Format_double_uses_ru_decimal_comma()
        {
            Assert.Equal("12,5", CsvNumberFormat.Format(12.5));
            Assert.Equal("0,5", CsvNumberFormat.Format(0.5));
            Assert.Equal("", CsvNumberFormat.Format(double.NaN));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Format_empty_stays_empty(string? input)
        {
            Assert.Equal("", CsvNumberFormat.Format(input));
        }

        [Theory]
        [InlineData("12:30:45")]
        [InlineData("12:30:45.123")]
        [InlineData("00:00:00.000")]
        [InlineData("2026-09-25 12:30:45.123")]
        [InlineData("H:mm:ss")]
        [InlineData("OK")]
        [InlineData("ERR")]
        public void Format_time_and_non_numeric_unchanged(string input)
        {
            Assert.Equal(input, CsvNumberFormat.Format(input));
        }

        [Fact]
        public void Format_never_emits_dot_decimal_separator_for_numbers()
        {
            string formatted = CsvNumberFormat.Format(Math.PI);
            Assert.DoesNotContain('.', formatted);
            Assert.Contains(',', formatted);
        }
    }
}
