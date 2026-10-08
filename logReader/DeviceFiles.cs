namespace logReader
{
    // Загрузка описаний посылок (.xlsx / .dbc / .dbf) и составных параметров (.xlsx).
    public static class DeviceFiles
    {
        public static List<Device> LoadDevicesFromExcel(string excelPath, Action<string>? log = null)
        {
            var logger = log ?? (_ => { });
            var dynamicDevices = new List<Device>();

            if (!File.Exists(excelPath))
                throw new FileNotFoundException($"Файл не найден: {excelPath}");

            try
            {
                var definitions = DeviceExcelFile.ReadAllDevices(excelPath, logger);
                foreach (var def in definitions)
                {
                    var instructions = new List<FieldInstruction>();
                    // Не меньше 8 байт: в старых файлах DLC часто занижен, а сигналы раньше проверялись по 64 битам.
                    int payloadBits = Math.Clamp(def.Dlc, 8, Device.MaxDataLength) * 8;

                    foreach (var row in def.Rows)
                    {
                        if (string.IsNullOrWhiteSpace(row.Header))
                        {
                            logger($"Устройство {def.DeviceId}: пустой Header — пропускаем.");
                            continue;
                        }

                        string type = (row.Type ?? "").Trim().ToUpperInvariant();

                        if (type == "NUM")
                        {
                            if (row.Length <= 0 || row.Length > 64)
                            {
                                logger($"Устройство {def.DeviceId}, '{row.Header}': Length вне диапазона 1..64 — пропускаем.");
                                continue;
                            }

                            if (!BitMath.SignalFitsInDlc(row.StartBit, row.Length, row.IsLittleEndian, payloadBits))
                            {
                                logger($"Устройство {def.DeviceId}, '{row.Header}': StartBit/Length выходят за пределы {payloadBits / 8} байт данных — пропускаем.");
                                continue;
                            }

                            instructions.Add(new FieldInstruction
                            {
                                FieldIndex = row.FieldIndex,
                                Header = row.Header,
                                Type = "NUM",
                                StartBit = row.StartBit,
                                LengthBit = row.Length,
                                Scale = row.Scale,
                                Offset = row.Offset,
                                IsLittleEndian = row.IsLittleEndian,
                                SignedRaw = row.SignedRaw,
                                Unit = row.Unit ?? "",
                                Min = row.MinPhys ?? 0,
                                Max = row.MaxPhys ?? 0,
                            });
                        }
                        else if (type == "BIN")
                        {
                            int lowByte = row.StartBit;
                            if (lowByte < 0 || lowByte >= Device.MaxDataLength)
                            {
                                logger($"Устройство {def.DeviceId}, '{row.Header}': для BIN колонка StartBit (байт данных) должна быть 0..{Device.MaxDataLength - 1} — пропускаем.");
                                continue;
                            }

                            int bitStart = row.BitStart ?? 0;
                            int len = row.Length;
                            if (len <= 0 || len > 8)
                            {
                                logger($"Устройство {def.DeviceId}, '{row.Header}': для BIN Length должен быть 1..8 — пропускаем.");
                                continue;
                            }

                            if (bitStart < 0 || bitStart + len > 8)
                            {
                                logger($"Устройство {def.DeviceId}, '{row.Header}': для BIN BitStart+Length не должны превышать 8 — пропускаем.");
                                continue;
                            }

                            instructions.Add(new FieldInstruction
                            {
                                FieldIndex = row.FieldIndex,
                                Header = row.Header,
                                Type = "BIN",
                                ByteLow = lowByte,
                                StartBit = bitStart,
                                LengthBit = len,
                                Unit = row.Unit ?? "",
                            });
                        }
                        else
                        {
                            logger($"Устройство {def.DeviceId}, '{row.Header}': неизвестный Type '{type}' — пропускаем.");
                        }
                    }

                    var sorted = instructions.OrderBy(i => i.FieldIndex).ToList();
                    for (int i = 0; i < sorted.Count; i++)
                        sorted[i].FieldIndex = i;

                    if (sorted.Count > 0)
                        dynamicDevices.Add(new DynamicDevice(def.DeviceId, sorted));
                }
            }
            catch (Exception ex) when (ex is not FileNotFoundException)
            {
                throw new InvalidOperationException($"Ошибка загрузки устройств: {ex.Message}", ex);
            }

            return dynamicDevices;
        }

        public static List<Device> LoadDevices(string path, Action<string>? log = null)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Путь к файлу устройств не задан.", nameof(path));

            string extension = Path.GetExtension(path);
            if (extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                return LoadDevicesFromExcel(path, log);
            if (extension.Equals(".dbc", StringComparison.OrdinalIgnoreCase))
                return DbcDevicesLoader.LoadDevicesFromDbc(path, log);
            if (extension.Equals(".dbf", StringComparison.OrdinalIgnoreCase))
                return DbcDevicesLoader.LoadDevicesFromDbf(path, log);

            throw new NotSupportedException($"Поддерживаются только файлы .xlsx, .dbc и .dbf: {path}");
        }

        public static CompositeRuntime LoadComposites(string? path, Action<string>? log = null)
        {
            var logger = log ?? (_ => { });

            if (string.IsNullOrWhiteSpace(path))
                return CompositeRuntime.Build(Array.Empty<CompositeSignal>());

            string extension = Path.GetExtension(path);
            if (!extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException($"Файл составных параметров должен быть .xlsx: {path}");

            var signals = CompositeExcelFile.ReadAll(path, logger);
            return CompositeRuntime.Build(signals);
        }

        public static void ResetState(IEnumerable<Device> devices)
        {
            foreach (var d in devices)
                d?.ResetState();
        }
    }
}
