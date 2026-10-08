using System.Globalization;
using System.Text;

namespace logReader.Processing
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

        // Код DLC CAN FD для длины данных (9..15 → 12, 16, 20, 24, 32, 48, 64 байт).
        public static int DlcForLength(int length) => length switch
        {
            <= 8 => Math.Max(0, length),
            <= 12 => 9,
            <= 16 => 10,
            <= 20 => 11,
            <= 24 => 12,
            <= 32 => 13,
            <= 48 => 14,
            _ => 15
        };

        public static int LengthForDlc(int dlc) => dlc switch
        {
            <= 8 => Math.Max(0, dlc),
            9 => 12,
            10 => 16,
            11 => 20,
            12 => 24,
            13 => 32,
            14 => 48,
            _ => 64
        };

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
            int count = Math.Clamp(byteCount, 0, Math.Min(bytes.Length, Device.MaxDataLength));
            // Длина данных CAN FD — только из ряда 0..8, 12, 16, …, 64: недостающие байты дополняются нулями.
            int dlc = DlcForLength(count);
            int dataLength = LengthForDlc(dlc);

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
            sb.Append("0 0 ")
              .Append(dlc.ToString(CultureInfo.InvariantCulture))
              .Append(' ')
              .Append(dataLength.ToString(CultureInfo.InvariantCulture));

            for (int i = 0; i < dataLength; i++)
            {
                int value = i < count ? bytes[i] : 0;
                sb.Append(' ').Append(value.ToString("X2", CultureInfo.InvariantCulture));
            }

            sb.Append("        0    0   200000        0        0        0        0        0");
            return sb.ToString();
        }
    }
}
