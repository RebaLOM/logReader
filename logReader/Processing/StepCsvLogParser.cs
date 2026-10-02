using System.Globalization;

namespace logReader.Processing
{
    // Legacy CSV: «шаг;время;ID;признак;байт0…байт7» (байты десятичные).
    internal static class StepCsvLogParser
    {
        public const int FirstByteColumn = 4;

        public static bool TryParseAcceptedId(string line, out string id)
        {
            id = "";
            if (string.IsNullOrWhiteSpace(line)) return false;
            var parts = line.Split(';', FirstByteColumn + 1);
            if (parts.Length < FirstByteColumn) return false;
            return IsAccepted(parts[3]) && CanId.TryNormalize(parts[2], out id);
        }

        public static bool IsAccepted(string flag)
            => int.TryParse(flag, NumberStyles.Integer, CultureInfo.InvariantCulture, out int pri) && pri == 1;

        public static bool TryParseByte(string raw, out int value)
            => int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
               && value is >= 0 and <= byte.MaxValue;
    }
}
