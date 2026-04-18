using System.Globalization;
using System.Text.RegularExpressions;

namespace logReader
{
    internal static class DbcDevicesLoader
    {
        private static readonly Regex MessageRegex = new(
            @"^BO_\s+(?<id>\d+)\s+(?<name>\S+)\s*:\s*(?<dlc>\d+)\s+\S+",
            RegexOptions.Compiled);

        private static readonly Regex SignalRegex = new(
            @"^SG_\s+(?<name>\S+)\s*:\s*(?<start>\d+)\|(?<length>\d+)@(?<order>[01])(?<sign>[+-])\s+\((?<factor>[-+]?\d+(?:[.,]\d+)?(?:[eE][-+]?\d+)?),(?<offset>[-+]?\d+(?:[.,]\d+)?(?:[eE][-+]?\d+)?)\)",
            RegexOptions.Compiled);

        public static List<Device> LoadDevicesFromDbc(string dbcPath, Action<string>? log = null)
        {
            var logger = log ?? Console.WriteLine;

            if (!File.Exists(dbcPath))
                throw new FileNotFoundException($"Файл не найден: {dbcPath}");

            var deviceGroups = new Dictionary<string, List<FieldInstruction>>(StringComparer.OrdinalIgnoreCase);
            string? currentDeviceId = null;

            foreach (string rawLine in File.ReadLines(dbcPath))
            {
                string line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var messageMatch = MessageRegex.Match(line);
                if (messageMatch.Success)
                {
                    uint dbcId = uint.Parse(messageMatch.Groups["id"].Value, CultureInfo.InvariantCulture);
                    uint actualId = dbcId & 0x1FFFFFFF;
                    currentDeviceId = actualId.ToString("X", CultureInfo.InvariantCulture);

                    if (!deviceGroups.ContainsKey(currentDeviceId))
                        deviceGroups[currentDeviceId] = new List<FieldInstruction>();

                    continue;
                }

                if (currentDeviceId == null || !line.StartsWith("SG_", StringComparison.Ordinal))
                    continue;

                var signalMatch = SignalRegex.Match(line);
                if (!signalMatch.Success)
                {
                    logger($"Предупреждение: не удалось разобрать сигнал DBC: {line}");
                    continue;
                }

                int startBit = int.Parse(signalMatch.Groups["start"].Value, CultureInfo.InvariantCulture);
                int bitLength = int.Parse(signalMatch.Groups["length"].Value, CultureInfo.InvariantCulture);
                bool isLittleEndian = signalMatch.Groups["order"].Value == "1";

                if (!isLittleEndian)
                {
                    logger($"Предупреждение: Motorola big-endian пока не поддерживается, сигнал пропущен: {signalMatch.Groups["name"].Value}");
                    continue;
                }

                if (bitLength <= 0 || bitLength > 64 || startBit + bitLength > 64)
                {
                    logger($"Предупреждение: сигнал вне диапазона 64 бит, пропущен: {signalMatch.Groups["name"].Value}");
                    continue;
                }

                double factor = ParseDouble(signalMatch.Groups["factor"].Value);
                double offset = ParseDouble(signalMatch.Groups["offset"].Value);

                var list = deviceGroups[currentDeviceId];
                list.Add(new FieldInstruction
                {
                    FieldIndex = list.Count,
                    Header = BeautifySignalName(signalMatch.Groups["name"].Value),
                    Type = "NUM",
                    StartBit = startBit,
                    LenghtBit = bitLength,
                    Scale = factor,
                    Offset = offset,
                    UseBitExtraction = true,
                    IsLittleEndian = true,
                    // В DBC файлах сигналы часто ошибочно помечаются как знаковые (@1-),
                    // даже если для отрицательных значений используется смещение (offset).
                    // Для корректной совместимости с логикой обработки из Excel (где SignedRaw = false),
                    // принудительно сбрасываем флаг знака.
                    SignedRaw = false
                });
            }

            return deviceGroups
                .Where(kvp => kvp.Value.Count > 0)
                .Select(kvp => (Device)new DynamicDevice(kvp.Key, kvp.Value))
                .ToList();
        }

        private static double ParseDouble(string value)
        {
            string normalized = value.Trim().Replace(',', '.');
            return double.Parse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static string BeautifySignalName(string rawName)
        {
            string text = rawName.Replace('_', ' ').Trim();
            return string.IsNullOrWhiteSpace(text) ? rawName : text;
        }
    }
}
