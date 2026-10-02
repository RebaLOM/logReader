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

        public static bool LooksLikeStepCsv(string path, System.Text.Encoding encoding)
        {
            int checkedLines = 0;
            foreach (string line in LogFileReader.ReadLines(path, encoding))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split(';');

                // В первых строках результатов LOGER есть заголовок «Шаг»/«Время»/«Time».
                if (checkedLines < 2 && parts.Any(p => p.Trim() is "Шаг" or "Время" or "Time"))
                    return false;

                if (IsFrameRow(parts))
                    return true;
                if (++checkedLines >= 50) break;
            }
            return false;
        }

        private static bool IsFrameRow(string[] parts)
        {
            if (parts.Length < FirstByteColumn + 8) return false;
            if (parts[0].Length > 0 && !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out _)) return false;
            if (!CanId.TryNormalize(parts[2], out _)) return false;
            if (!int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out _)) return false;
            for (int i = 0; i < 8; i++)
                if (!TryParseByte(parts[FirstByteColumn + i], out _)) return false;
            return true;
        }

        public static bool IsAccepted(string flag)
            => int.TryParse(flag, NumberStyles.Integer, CultureInfo.InvariantCulture, out int pri) && pri == 1;

        public static bool TryParseByte(string raw, out int value)
            => int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
               && value is >= 0 and <= byte.MaxValue;
    }
}
