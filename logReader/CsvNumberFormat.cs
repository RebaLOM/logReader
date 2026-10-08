using System.Globalization;

namespace logReader
{
    // CSV под Excel/Power BI (ru-RU): точка в числе читается как время и искажает значение.
    // Колонки времени (HH:mm:ss, OADate-строки и т.п.) не трогаем — только числовые значения.
    public static class CsvNumberFormat
    {
        private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

        public static string Format(double value)
            => !double.IsFinite(value) ? "" : value.ToString(Ru);

        public static string Format(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            // Время и прочий нечисловой текст оставляем как есть (в т.ч. «12:30:45.123»).
            if (LooksLikeTimeOrNonNumber(value))
                return value;

            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                return d.ToString(Ru);

            return value;
        }

        private static bool LooksLikeTimeOrNonNumber(string value)
        {
            // HH:mm:ss[.fff], yyyy-MM-dd HH:mm:ss.fff — есть двоеточие.
            if (value.IndexOf(':') >= 0)
                return true;

            return false;
        }
    }
}
