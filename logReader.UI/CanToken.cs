using System.Globalization;

namespace logReader.UI
{
    // Общий разбор hex-токенов логов; расхождения между парсерами вынесены в параметры.
    internal static class CanToken
    {
        // allowTrailingX: в ASC extended-ID помечается хвостовой «x».
        public static bool TryNormalizeId(string? raw, out string id, bool allowTrailingX = false, int minHexLength = 1)
        {
            id = "";
            if (string.IsNullOrWhiteSpace(raw)) return false;

            string token = raw.Trim();
            if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                token = token.Substring(2);

            if (allowTrailingX && token.EndsWith("x", StringComparison.OrdinalIgnoreCase))
                token = token.Substring(0, token.Length - 1);

            if (token.Length < minHexLength) return false;

            foreach (char c in token)
            {
                bool isHex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!isHex) return false;
            }

            id = token.ToUpperInvariant();
            return true;
        }

        // requireTwoChars: в ASC однобуквенные токены вроде «d» — не данные кадра.
        public static bool TryParseHexByte(string? raw, out int value, bool requireTwoChars = false)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(raw)) return false;

            string token = raw.Trim();
            if (requireTwoChars)
            {
                if (token.Length != 2) return false;
            }
            else if (token.Length is 0 or > 2)
            {
                return false;
            }

            return int.TryParse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value)
                   && value is >= 0 and <= byte.MaxValue;
        }
    }
}
