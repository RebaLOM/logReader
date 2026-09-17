using System.Globalization;
using logReader;

namespace logReader.UI
{
    // Общий разбор hex-токенов логов; расхождения между парсерами вынесены в параметры.
    internal static class CanToken
    {
        // allowTrailingX: в ASC extended-ID помечается хвостовой «x».
        public static bool TryNormalizeId(string? raw, out string id, bool allowTrailingX = false, int minHexLength = 1)
            => CanId.TryNormalize(raw, out id, allowTrailingX, minHexLength);

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
