using System.Globalization;
using System.Text;

namespace logReader.UI
{
    internal static class CsvOutput
    {
        internal const char Delimiter = ';';

        internal static void WriteRow(StreamWriter writer, IEnumerable<string> values)
        {
            bool first = true;
            foreach (string value in values)
            {
                if (!first)
                    writer.Write(Delimiter);
                writer.Write(Escape(value));
                first = false;
            }
            writer.WriteLine();
        }

        internal static string Escape(string? value)
        {
            string text = value ?? "";
            bool mustQuote = text.IndexOfAny(new[] { Delimiter, '"', '\r', '\n' }) >= 0;
            if (!mustQuote)
                return text;
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }

        internal static string FormatValue(string value)
        {
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                return d.ToString(CultureInfo.InvariantCulture);
            return value;
        }
    }
}
