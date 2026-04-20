using System.Globalization;

namespace logReader
{
    internal static class DbcDevicesLoader
    {
        public static List<Device> LoadDevicesFromDbc(string dbcPath, Action<string>? log = null)
        {
            var logger = log ?? Console.WriteLine;

            if (!File.Exists(dbcPath))
                throw new FileNotFoundException($"Файл не найден: {dbcPath}");

            var deviceGroups = new Dictionary<string, List<FieldInstruction>>(StringComparer.OrdinalIgnoreCase);
            var order = new List<string>();
            string? currentDeviceId = null;
            var seenMessageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string rawLine in File.ReadLines(dbcPath))
            {
                string line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;

                if (DbcLineParser.TryParseMessage(line, out var header))
                {
                    currentDeviceId = header.Id.ToString("X", CultureInfo.InvariantCulture);

                    if (!seenMessageIds.Add(currentDeviceId))
                    {
                        logger($"Предупреждение: дубликат BO_ 0x{currentDeviceId} — сигналы будут объединены.");
                    }

                    if (!deviceGroups.ContainsKey(currentDeviceId))
                    {
                        deviceGroups[currentDeviceId] = new List<FieldInstruction>();
                        order.Add(currentDeviceId);
                    }

                    continue;
                }

                if (currentDeviceId == null || !line.StartsWith("SG_", StringComparison.Ordinal))
                    continue;

                if (!DbcLineParser.TryParseSignal(line, out var sig))
                {
                    logger($"Предупреждение: не удалось разобрать сигнал DBC: {line}");
                    continue;
                }

                if (sig.Length <= 0 || sig.Length > 64)
                {
                    logger($"Предупреждение: '{sig.Name}': Length={sig.Length} вне 1..64 — пропущен.");
                    continue;
                }

                if (sig.IsLittleEndian)
                {
                    if (sig.StartBit < 0 || sig.StartBit + sig.Length > 64)
                    {
                        logger($"Предупреждение: Intel сигнал '{sig.Name}' вне 64 бит — пропущен.");
                        continue;
                    }
                }
                else
                {
                    // Для Motorola StartBit указывает MSB; допускается диапазон 0..63.
                    if (sig.StartBit < 0 || sig.StartBit > 63)
                    {
                        logger($"Предупреждение: Motorola сигнал '{sig.Name}': StartBit={sig.StartBit} вне 0..63 — пропущен.");
                        continue;
                    }
                }

                var list = deviceGroups[currentDeviceId];
                list.Add(new FieldInstruction
                {
                    FieldIndex = list.Count,
                    Header = BeautifySignalName(sig.Name),
                    Type = "NUM",
                    StartBit = sig.StartBit,
                    LengthBit = sig.Length,
                    Scale = sig.Factor,
                    Offset = sig.Offset,
                    UseBitExtraction = true,
                    IsLittleEndian = sig.IsLittleEndian,
                    SignedRaw = sig.IsSigned,
                    Unit = sig.Unit ?? "",
                    Min = sig.Min,
                    Max = sig.Max,
                });
            }

            return order
                .Where(id => deviceGroups[id].Count > 0)
                .Select(id => (Device)new DynamicDevice(id, deviceGroups[id]))
                .ToList();
        }

        private static string BeautifySignalName(string rawName)
        {
            string text = rawName.Replace('_', ' ').Trim();
            return string.IsNullOrWhiteSpace(text) ? rawName : text;
        }
    }
}
