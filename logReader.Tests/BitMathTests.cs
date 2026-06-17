using logReader;

namespace logReader.Tests
{
    public class BitMathTests
    {
        [Theory]
        [InlineData(8, false, 0, 255)]
        [InlineData(8, true, -128, 127)]
        [InlineData(16, false, 0, 65535)]
        [InlineData(1, false, 0, 1)]
        public void ComputeRawRange_ReturnsExpectedBounds(int length, bool signed, long expectedMin, long expectedMax)
        {
            BitMath.ComputeRawRange(length, signed, out long min, out long max);

            Assert.Equal(expectedMin, min);
            Assert.Equal(expectedMax, max);
        }

        [Fact]
        public void ComputeRawRange_64Bit_UsesLongLimits()
        {
            BitMath.ComputeRawRange(64, true, out long sMin, out long sMax);
            Assert.Equal(long.MinValue, sMin);
            Assert.Equal(long.MaxValue, sMax);

            BitMath.ComputeRawRange(64, false, out long uMin, out long uMax);
            Assert.Equal(0, uMin);
            Assert.Equal(long.MaxValue, uMax);
        }

        [Theory]
        [InlineData(0x1234, 16, "1234")]
        [InlineData(10, 12, "00A")]
        [InlineData(-1, 8, "FF")]
        public void FormatHex_MasksAndPadsToNibbleWidth(long raw, int length, string expected)
        {
            Assert.Equal(expected, BitMath.FormatHex(raw, length));
        }

        [Theory]
        [InlineData(0, 8, true, 8, true)]
        [InlineData(1, 8, true, 8, false)]
        [InlineData(0, 64, true, 64, true)]
        [InlineData(7, 8, false, 8, true)]
        [InlineData(7, 16, false, 8, false)]
        [InlineData(7, 16, false, 16, true)]
        public void SignalFitsInDlc_HandlesIntelAndMotorola(int startBit, int length, bool littleEndian, int payloadBits, bool expected)
        {
            Assert.Equal(expected, BitMath.SignalFitsInDlc(startBit, length, littleEndian, payloadBits));
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(0, -1)]
        public void SignalFitsInDlc_RejectsNonPositiveLength(int startBit, int length)
        {
            Assert.False(BitMath.SignalFitsInDlc(startBit, length, true, 8));
        }
    }
}
