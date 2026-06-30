using System.Globalization;

namespace logReader
{
    internal static class DbcDevicesLoader
    {
        public static List<Device> LoadDevicesFromDbc(string dbcPath, Action<string>? log = null)
        {
            if (!File.Exists(dbcPath))
                throw new FileNotFoundException($"Файл не найден: {dbcPath}");

            return LoadDevicesFromMessages(DbcFile.Read(dbcPath), log);
        }

        public static List<Device> LoadDevicesFromDbf(string dbfPath, Action<string>? log = null)
        {
            if (!File.Exists(dbfPath))
                throw new FileNotFoundException($"Файл не найден: {dbfPath}");

            return LoadDevicesFromMessages(DbfFile.Read(dbfPath), log);
        }

        public static List<Device> LoadDevicesFromMessages(IReadOnlyList<DbcMessage> messages, Action<string>? log = null)
        {
            var logger = log ?? Console.WriteLine;
            var deviceGroups = new Dictionary<string, List<FieldInstruction>>(StringComparer.OrdinalIgnoreCase);
            var order = new List<string>();
            var seenMessageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var message in messages)
            {
                string deviceId = message.Id.ToString("X", CultureInfo.InvariantCulture);

                if (!seenMessageIds.Add(deviceId))
                    logger($"Предупреждение: дубликат 0x{deviceId} — сигналы будут объединены.");

                if (!deviceGroups.ContainsKey(deviceId))
                {
                    deviceGroups[deviceId] = new List<FieldInstruction>();
                    order.Add(deviceId);
                }

                foreach (var sig in message.Signals)
                {
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
                    else if (sig.StartBit < 0 || sig.StartBit > 63)
                    {
                        logger($"Предупреждение: Motorola сигнал '{sig.Name}': StartBit={sig.StartBit} вне 0..63 — пропущен.");
                        continue;
                    }

                    var list = deviceGroups[deviceId];
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
