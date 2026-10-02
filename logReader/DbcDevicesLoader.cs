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
            var logger = log ?? (_ => { });
            var deviceGroups = new Dictionary<string, List<FieldInstruction>>(StringComparer.OrdinalIgnoreCase);
            var order = new List<string>();
            var seenMessageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var message in messages)
            {
                string deviceId = CanId.Format(message.Id);

                if (!seenMessageIds.Add(deviceId))
                    logger($"Предупреждение: дубликат 0x{deviceId} — сигналы будут объединены.");

                if (!deviceGroups.ContainsKey(deviceId))
                {
                    deviceGroups[deviceId] = new List<FieldInstruction>();
                    order.Add(deviceId);
                }

                // Не меньше 8 байт: DLC в описаниях бывает занижен, сигналы раньше проверялись по 64 битам.
                int payloadBits = Math.Clamp(message.Dlc, 8, Device.MaxDataLength) * 8;
                bool hasMultiplexor = false;
                foreach (var sig in message.Signals)
                {
                    if (sig.IsMultiplexor)
                    {
                        if (hasMultiplexor)
                            logger($"Предупреждение: посылка {message.Name}: несколько мультиплексоров — учитывается первый.");
                        hasMultiplexor = true;
                    }
                    if (sig.MultiplexValue.HasValue && !message.Signals.Any(s => s.IsMultiplexor))
                        logger($"Предупреждение: '{sig.Name}': мультиплексированный сигнал без мультиплексора — декодируется в каждом кадре.");

                    if (sig.Length <= 0 || sig.Length > 64)
                    {
                        logger($"Предупреждение: '{sig.Name}': Length={sig.Length} вне 1..64 — пропущен.");
                        continue;
                    }

                    if (!BitMath.SignalFitsInDlc(sig.StartBit, sig.Length, sig.IsLittleEndian, payloadBits))
                    {
                        logger($"Предупреждение: сигнал '{sig.Name}' ({sig.StartBit}|{sig.Length}) выходит за {payloadBits / 8} байт данных — пропущен.");
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
                        IsLittleEndian = sig.IsLittleEndian,
                        SignedRaw = sig.IsSigned,
                        Unit = sig.Unit ?? "",
                        Min = sig.Min,
                        Max = sig.Max,
                        ValueType = sig.ValueType,
                        IsMultiplexor = sig.IsMultiplexor,
                        MuxValue = message.Signals.Any(s => s.IsMultiplexor) ? sig.MultiplexValue : null,
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
