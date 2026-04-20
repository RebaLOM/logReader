using System.Globalization;

namespace logReader
{
    /// <summary>
    /// Единая точка разбора чисел для UI и чтения файлов.
    /// Порядок: Invariant → CurrentCulture → ручная замена запятой на точку → Invariant.
    /// </summary>
    public static class NumberParseHelper
    {
        public static bool TryParseDouble(string? text, out double value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text)) return true;

            string s = text.Trim();
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return true;
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out value)) return true;

            s = s.Replace(',', '.');
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        public static double ParseDoubleInvariant(string value)
        {
            string normalized = value.Trim().Replace(',', '.');
            return double.Parse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture);
        }
    }
}
