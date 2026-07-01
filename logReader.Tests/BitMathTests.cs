namespace logReader.Tests;

public class BitMathTests
{
    [Fact]
    public void CellToGlobalBit_maps_byte_and_bit_in_byte()
    {
        Assert.Equal(0, BitMath.CellToGlobalBit(0, 0));
        Assert.Equal(7, BitMath.CellToGlobalBit(0, 7));
        Assert.Equal(8, BitMath.CellToGlobalBit(1, 0));
        Assert.Equal(15, BitMath.CellToGlobalBit(1, 7));
    }

    [Fact]
    public void TryGlobalBitToCell_round_trips()
    {
        Assert.True(BitMath.TryGlobalBitToCell(11, out int byteIndex, out int bitInByte));
        Assert.Equal(1, byteIndex);
        Assert.Equal(3, bitInByte);
        Assert.Equal(11, BitMath.CellToGlobalBit(byteIndex, bitInByte));
    }

    [Fact]
    public void EnumerateSignalBits_Intel_8_bits_from_byte0()
    {
        var bits = BitMath.EnumerateSignalBits(startBit: 0, length: 8, littleEndian: true).ToArray();
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 6, 7 }, bits);
    }

    [Fact]
    public void EnumerateSignalBits_Motorola_16_bits_across_byte_boundary()
    {
        var bits = BitMath.EnumerateSignalBits(startBit: 7, length: 16, littleEndian: false).ToArray();
        Assert.Equal(new[] { 7, 6, 5, 4, 3, 2, 1, 0, 15, 14, 13, 12, 11, 10, 9, 8 }, bits);
    }

    [Fact]
    public void TryBuildSelectionFromGlobalBits_Motorola_follows_dbc_path()
    {
        Assert.True(BitMath.TryBuildSelectionFromGlobalBits(
            anchorBit: 7,
            targetBit: 12,
            littleEndian: false,
            payloadBits: 64,
            out int startBit,
            out int length));

        Assert.Equal(7, startBit);
        Assert.Equal(12, length);
        Assert.Equal(
            BitMath.EnumerateSignalBits(7, 12, littleEndian: false).ToArray(),
            BitMath.EnumerateSignalBits(startBit, length, littleEndian: false).ToArray());
    }
}
