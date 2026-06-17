using System.Collections.Generic;
using logReader;

namespace logReader.Tests
{
    public class DynamicDeviceTests
    {
        private static DynamicDevice Build(FieldInstruction instr, params int[] bytes)
        {
            var dev = new DynamicDevice("123", new List<FieldInstruction> { instr });
            var raw = new int[8];
            for (int i = 0; i < bytes.Length && i < raw.Length; i++) raw[i] = bytes[i];
            dev.RawBytes = raw;
            return dev;
        }

        [Fact]
        public void Decode_IntelBitField_LittleEndian16()
        {
            var instr = new FieldInstruction
            {
                FieldIndex = 0, Header = "v", Type = "NUM",
                UseBitExtraction = true, IsLittleEndian = true,
                StartBit = 0, LengthBit = 16, Scale = 1, Offset = 0
            };
            var dev = Build(instr, 0x34, 0x12);

            dev.Decode();

            Assert.Equal("4660", dev.ProcessedData[0]);
        }

        [Fact]
        public void Decode_MotorolaBitField_BigEndianByte()
        {
            var instr = new FieldInstruction
            {
                FieldIndex = 0, Header = "v", Type = "NUM",
                UseBitExtraction = true, IsLittleEndian = false,
                StartBit = 7, LengthBit = 8, Scale = 1, Offset = 0
            };
            var dev = Build(instr, 0xA5);

            dev.Decode();

            Assert.Equal("165", dev.ProcessedData[0]);
        }

        [Fact]
        public void Decode_BytePair_AppliesScaleAndOffset()
        {
            var instr = new FieldInstruction
            {
                FieldIndex = 0, Header = "v", Type = "NUM",
                UseBitExtraction = false, ByteLow = 0,
                Scale = 0.5, Offset = -10
            };
            var dev = Build(instr, 100);

            dev.Decode();

            Assert.Equal("40", dev.ProcessedData[0]);
        }

        [Fact]
        public void Decode_SignedBytePair_AppliesTwosComplement()
        {
            var instr = new FieldInstruction
            {
                FieldIndex = 0, Header = "v", Type = "NUM",
                UseBitExtraction = false, ByteLow = 0, ByteHigh = 1,
                SignedRaw = true, Scale = 1, Offset = 0
            };
            var dev = Build(instr, 0x00, 0xFF);

            dev.Decode();

            Assert.Equal("-256", dev.ProcessedData[0]);
        }

        [Fact]
        public void Decode_BinField_ExtractsBits()
        {
            var instr = new FieldInstruction
            {
                FieldIndex = 0, Header = "flag", Type = "BIN",
                ByteLow = 0, StartBit = 0, LengthBit = 4
            };
            var dev = Build(instr, 0xA5);

            dev.Decode();

            Assert.Equal("10", dev.ProcessedData[0]);
        }

        [Fact]
        public void Decode_BitFieldOutOfRange_YieldsErr()
        {
            var instr = new FieldInstruction
            {
                FieldIndex = 0, Header = "v", Type = "NUM",
                UseBitExtraction = true, IsLittleEndian = true,
                StartBit = 60, LengthBit = 16, Scale = 1, Offset = 0
            };
            var dev = Build(instr, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF);

            dev.Decode();

            Assert.Equal("ERR", dev.ProcessedData[0]);
        }
    }
}
