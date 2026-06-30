using System.Globalization;
using System.Text;

namespace logReader.UI
{
    // Запись Vector ASC в формате CAN FD — тот же, что читает AscLogParser.
    internal static class VectorCanFdAscWriter
    {
        private const string Channel = "1";

        public static void WriteHeader(TextWriter writer, DateTime baseDateTime)
        {
            writer.WriteLine(
                "date " + baseDateTime.ToString("ddd MMM dd HH:mm:ss.fff yyyy", CultureInfo.InvariantCulture));
            writer.WriteLine("base hex  timestamps absolute");
            writer.WriteLine("no internal events logged");
        }

        public static string FormatId(string idHex)
        {
            string id = idHex.Trim().ToUpperInvariant();
            if (id.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                id = id[2..];

            if (!ulong.TryParse(id, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong idValue))
                return id;

            return idValue > 0x7FFUL ? id + "x" : id;
        }

        public static void WriteFrame(
            TextWriter writer,
            double offsetSeconds,
            string direction,
            string idHex,
            ReadOnlySpan<int> bytes,
            int byteCount = 8)
        {
            writer.WriteLine(BuildFrameLine(offsetSeconds, direction, idHex, bytes, byteCount));
        }

        internal static string BuildFrameLine(
            double offsetSeconds,
            string direction,
            string idHex,
            ReadOnlySpan<int> bytes,
            int byteCount)
        {
            string idOut = FormatId(idHex);
            string dir = string.IsNullOrWhiteSpace(direction) ? "Rx" : direction.Trim();
            int count = Math.Clamp(byteCount, 0, bytes.Length);

            var sb = new StringBuilder();
            sb.Append("   ");
            sb.Append(offsetSeconds.ToString("0.000000", CultureInfo.InvariantCulture));
            sb.Append(" CANFD   ");
            sb.Append(Channel);
            sb.Append(' ');
            sb.Append(dir);
            sb.Append("   ");
            sb.Append(idOut);

            // Выравнивание как в CANoe — парсер читает по токенам, не по колонкам.
            int pad = Math.Max(1, 38 - idOut.Length);
            sb.Append(' ', pad);
            sb.Append("0 0 8  8");

            for (int i = 0; i < count; i++)
                sb.Append(' ').Append(bytes[i].ToString("X2", CultureInfo.InvariantCulture));

            sb.Append("        0    0   200000        0        0        0        0        0");
            return sb.ToString();
        }
    }
}
