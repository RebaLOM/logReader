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

        internal static string FrameKey(uint id, bool isExtended)
            => isExtended && id <= MaxStandardId ? Format(id) + "x" : Format(id, isExtended);

        // Свести разные записи одного ID к одному виду: "CFF0008" и "0CFF0008" → "0CFF0008".
        public static bool TryNormalize(string? raw, out string id, bool allowTrailingX = false, int minHexLength = 1)
        {
            id = "";
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            string token = raw.Trim();
            if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                token = token[2..];

            bool explicitlyExtended = allowTrailingX && token.EndsWith("x", StringComparison.OrdinalIgnoreCase);
            if (explicitlyExtended)
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

            id = FrameKey(value, explicitlyExtended || value > MaxStandardId);
            return true;
        }

        // Без явного признака типа выбираем стандартный ID. Если описан только
        // расширенный вариант с тем же числом, сохраняем совместимость со старыми логами.
        internal static Dictionary<string, Device> IndexDevices(IEnumerable<Device> devices, bool allowUntypedIds = false)
        {
            var result = new Dictionary<string, Device>(StringComparer.OrdinalIgnoreCase);
            foreach (var device in devices) result[device.ID] = device;
            if (allowUntypedIds)
            {
                foreach (var device in devices)
                    if (device.ID.EndsWith("x", StringComparison.OrdinalIgnoreCase))
                        result.TryAdd(device.ID[..^1], device);
            }
            return result;
        }

        // Для ключей из пользовательских файлов: нормализованный ID или исходный текст в верхнем регистре.
        public static string NormalizeOrUpper(string? raw)
            => TryNormalize(raw, out string id, allowTrailingX: true) ? id : (raw ?? "").Trim().ToUpperInvariant();
    }
}
