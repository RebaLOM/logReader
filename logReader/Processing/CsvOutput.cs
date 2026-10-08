using System.Text;

namespace logReader.Processing
{
    internal static class CsvOutput
    {
        internal const char Delimiter = ';';

        private static readonly char[] QuoteTriggers = { Delimiter, '"', '\r', '\n' };

        public static UTF8Encoding Encoding { get; } = new(encoderShouldEmitUTF8Identifier: true);

        internal static void WriteRow(TextWriter writer, IEnumerable<string> values)
        {
            bool first = true;
            foreach (string value in values)
            {
                if (!first)
                    writer.Write(Delimiter);
                writer.Write(Quote(value));
                first = false;
            }
            writer.WriteLine();
        }

        internal static string Quote(string? value)
        {
            string text = value ?? "";
            if (text.IndexOfAny(QuoteTriggers) < 0)
                return text;
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }

        // Текст из файлов описаний/логов (заголовки, ID, время): «=…», «+…», «@…» Excel выполнил бы как формулу.
        internal static string Text(string? value)
        {
            string text = value ?? "";
            if (text.Length == 0) return text;
            char c = text[0];
            bool looksLikeFormula = c is '=' or '+' or '-' or '@' or '\t' or '\r';
            if (looksLikeFormula && !double.TryParse(text, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out _))
                return "'" + text;
            return text;
        }
    }
}
