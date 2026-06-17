using logReader;

namespace logReader.Tests
{
    public class DbcPhysicalValueTests
    {
        [Fact]
        public void RoundPhysical_RemovesFloatingPointArtifacts()
        {
            Assert.Equal(6553.4, DbcPhysicalValue.RoundPhysical(6553.400000000001), 9);
        }

        [Fact]
        public void PhysicalBoundsFromRaw_ComputesExactBounds()
        {
            var (min, max) = DbcPhysicalValue.PhysicalBoundsFromRaw(0, 65535, 0.1, -100);

            Assert.Equal(-100, min, 9);
            Assert.Equal(6453.5, max, 9);
        }

        [Fact]
        public void PhysicalBoundsFromRaw_DoesNotThrowOnExtremeFactor()
        {
            // Экстремальный factor не должен ронять расчёт границ в редакторе сигналов.
            var ex = Record.Exception(() =>
            {
                var (min, max) = DbcPhysicalValue.PhysicalBoundsFromRaw(0, long.MaxValue, 1e30, 1e30);
                Assert.True(max >= min);
            });

            Assert.Null(ex);
        }

        [Fact]
        public void FormatForDbc_TrimsTrailingZeros()
        {
            Assert.Equal("6553.4", DbcPhysicalValue.FormatForDbc(6553.400000000001));
        }
    }
}
