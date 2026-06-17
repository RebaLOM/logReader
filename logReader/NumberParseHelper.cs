using System.Globalization;

namespace logReader
{
    // Единый разбор чисел из UI и xlsx: Invariant, затем локаль, затем запятая→точка.
    public static class NumberParseHelper
    {
        // Пустой ввод — не число; для полей с умолчанием см. TryParseOrDefault.
        public static bool TryParseDouble(string? text, out double value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;

            string s = text.Trim();
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return true;
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out value)) return true;

            s = s.Replace(',', '.');
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        public static bool TryParseOrDefault(string? text, double fallback, out double value)
        {
            if (string.IsNullOrWhiteSpace(text)) { value = fallback; return true; }
            return TryParseDouble(text, out value);
        }

        public static double ParseDoubleInvariant(string value)
        {
            string normalized = value.Trim().Replace(',', '.');
            return double.Parse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture);
        }
    }
}
