using System.Globalization;
using ClosedXML.Excel;

namespace logReader
{
    // Общие правила чтения ячеек xlsx для DeviceExcelFile и CompositeExcelFile.
    internal static class XlsxCellReader
    {
        public static double? GetNumber(IXLCell cell)
        {
            if (cell.IsEmpty()) return null;
            if (cell.TryGetValue(out double d) && !double.IsNaN(d)) return d;
            var s = cell.GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(s)) return null;
            return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed) ? parsed : null;
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
