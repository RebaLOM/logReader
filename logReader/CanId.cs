using System.Globalization;

namespace logReader
{
    // Канонический hex CAN ID: ToString("X") съедает ведущий 0 у extended (0CFF… → CFF…).
    public static class CanId
    {
        public const uint MaxStandardId = 0x7FFu;
        public const uint MaxExtendedId = 0x1FFFFFFFu;

        public static string Format(uint id, bool isExtended)
            => isExtended
                ? id.ToString("X8", CultureInfo.InvariantCulture)
                : id.ToString("X", CultureInfo.InvariantCulture);

        public static string Format(uint id)
            => Format(id, id > MaxStandardId);

        // Свести разные записи одного ID к одному виду: "CFF0008" и "0CFF0008" → "0CFF0008".
        public static bool TryNormalize(string? raw, out string id, bool allowTrailingX = false, int minHexLength = 1)
        {
            id = "";
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            string token = raw.Trim();
            if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                token = token[2..];

            if (allowTrailingX && token.EndsWith("x", StringComparison.OrdinalIgnoreCase))
                token = token[..^1];

            if (token.Length < minHexLength)
                return false;

            foreach (char c in token)
            {
                bool isHex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!isHex)
                    return false;
            }

            if (!uint.TryParse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint value))
                return false;

            if (value > MaxExtendedId)
                return false;

            // Полные 8 символов в исходнике — extended с ведущими нулями; иначе по значению.
            bool looksExtended = value > MaxStandardId || token.Length >= 8;
            id = Format(value, looksExtended);
            return true;
        }
    }
}
