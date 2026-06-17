using System.Globalization;

namespace logReader
{
    // Округление физических значений DBC через decimal, чтобы убрать артефакты double.
    public static class DbcPhysicalValue
    {
        public const int MaxDecimalPlaces = 6;

        public static double RoundPhysical(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return value;
            return Math.Round(value, MaxDecimalPlaces, MidpointRounding.AwayFromZero);
        }

        public static (double Min, double Max) PhysicalBoundsFromRaw(long rawMin, long rawMax, double factor, double offset)
        {
            // decimal точен для обычных масштабов; при экстремальных factor/offset
            // произведение выходит за decimal.MaxValue и роняло редактор сигналов.
            try
            {
                decimal f = (decimal)factor;
                decimal o = (decimal)offset;
                decimal min = (decimal)rawMin * f + o;
                decimal max = (decimal)rawMax * f + o;
                return (
                    (double)decimal.Round(min, MaxDecimalPlaces, MidpointRounding.AwayFromZero),
                    (double)decimal.Round(max, MaxDecimalPlaces, MidpointRounding.AwayFromZero));
            }
            catch (OverflowException)
            {
                // double допускает микроартефакты, но не падает на огромных коэффициентах.
                return (RoundPhysical(rawMin * factor + offset), RoundPhysical(rawMax * factor + offset));
            }
        }

        public static string FormatForDbc(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return value.ToString(CultureInfo.InvariantCulture);

            double r = RoundPhysical(value);
            return r.ToString("0.######", CultureInfo.InvariantCulture);
        }
    }
}
