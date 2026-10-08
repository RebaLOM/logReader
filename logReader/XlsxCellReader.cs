using ClosedXML.Excel;

namespace logReader
{
    // Общие правила чтения ячеек xlsx для DeviceExcelFile и CompositeExcelFile.
    internal static class XlsxCellReader
    {
        // Текстовая ячейка «0,1» — десятичная запятая, а не разделитель тысяч.
        public static double? GetNumber(IXLCell cell)
        {
            if (cell.IsEmpty()) return null;
            // TryGetValue<double> у ClosedXML сам разбирает текст и тоже считает запятую разделителем тысяч.
            XLCellValue value = cell.Value;
            if (value.IsNumber) return double.IsNaN(value.GetNumber()) ? null : value.GetNumber();
            if (value.IsBoolean) return value.GetBoolean() ? 1 : 0;
            var s = (value.IsText ? value.GetText() : cell.GetString())?.Trim();
            if (string.IsNullOrWhiteSpace(s)) return null;
            return NumberParseHelper.TryParseDouble(s, out double parsed) ? parsed : null;
        }

        public static int? GetInt(IXLCell cell)
        {
            double? d = GetNumber(cell);
            if (d is not double v || double.IsInfinity(v) || v < int.MinValue || v > int.MaxValue)
                return null;
            return (int)Math.Round(v);
        }

        public static bool ParseBool01(IXLCell cell, bool defaultValue = false)
        {
            if (cell.IsEmpty()) return defaultValue;
            var s = cell.GetString().Trim();
            if (s.Length == 0)
            {
                if (cell.TryGetValue(out double d) && !double.IsNaN(d))
                    return Math.Abs(d) >= 0.5;
                return defaultValue;
            }
            if (s.Equals("1", StringComparison.Ordinal) || s.Equals("true", StringComparison.OrdinalIgnoreCase)
                || s.Equals("yes", StringComparison.OrdinalIgnoreCase) || s.Equals("x", StringComparison.OrdinalIgnoreCase)
                || s.Equals("extended", StringComparison.OrdinalIgnoreCase))
                return true;
            if (s.Equals("0", StringComparison.Ordinal) || s.Equals("false", StringComparison.OrdinalIgnoreCase)
                || s.Equals("no", StringComparison.OrdinalIgnoreCase) || s.Equals("standard", StringComparison.OrdinalIgnoreCase))
                return false;
            return defaultValue;
        }

        public static bool ParseSigned(IXLCell cell)
        {
            var s = cell.GetString().Trim();
            if (s.Length == 0) return false;
            return s.Equals("-", StringComparison.Ordinal)
                || s.Equals("signed", StringComparison.OrdinalIgnoreCase)
                || s.Equals("1", StringComparison.Ordinal)
                || s.Equals("true", StringComparison.OrdinalIgnoreCase);
        }
    }
}
