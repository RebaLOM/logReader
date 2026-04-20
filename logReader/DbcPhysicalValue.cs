using System.Globalization;

namespace logReader
{
    /// <summary>
    /// Округление и форматирование физических значений для DBC (избегает артефактов double вроде 6553.400000000001).
    /// </summary>
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
            decimal f = (decimal)factor;
            decimal o = (decimal)offset;
            decimal min = (decimal)rawMin * f + o;
            decimal max = (decimal)rawMax * f + o;
            return (
                (double)decimal.Round(min, MaxDecimalPlaces, MidpointRounding.AwayFromZero),
                (double)decimal.Round(max, MaxDecimalPlaces, MidpointRounding.AwayFromZero));
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
