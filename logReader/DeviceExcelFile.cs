using System.Globalization;
using ClosedXML.Excel;

namespace logReader
{
    /// <summary>
    /// Одна строка Excel = один сигнал. Для Type=NUM поля соответствуют DBC (глобальный StartBit, длина в битах).
    /// Для Type=BIN: колонка StartBit = индекс байта 0..7, Length = длина битовой маски 1..8, BitStart = смещение внутри байта 0..7.
    /// </summary>
    public sealed record DeviceFieldRow(
        int FieldIndex,
        string Header,
        string Type,
        int StartBit,
        int Length,
        bool IsLittleEndian,
        bool SignedRaw,
        double Scale,
        double Offset,
        string? Unit,
        double? MinPhys,
        double? MaxPhys,
        int? BitStart);

    public sealed class DeviceDefinition
    {
        public string DeviceId { get; set; } = "";
        public string MessageName { get; set; } = "";
        public bool Extended { get; set; } = true;
        public int Dlc { get; set; } = 8;
        public List<DeviceFieldRow> Rows { get; set; } = new();
    }

    public static class DeviceExcelFile
    {
        public static readonly string[] HeaderRow =
        {
            "DeviceID",
            "MessageName",
            "Extended",
            "DLC",
            "FieldIndex",
            "Header",
            "Type",
            "StartBit",
            "Length",
            "ByteOrder",
            "Signed",
            "Scale",
            "Offset",
            "Unit",
            "Min",
            "Max",
            "BitStart"
        };

        public const int ColCount = 17;

        public static void CreateDevicesExcelTemplate(string path)
        {
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            SafeFileWriter.Write(path, tmp =>
            {
                using var workbook = new XLWorkbook();
                var ws = workbook.Worksheets.Add("Devices");
                WriteHeader(ws);
                ws.SheetView.FreezeRows(1);
                ws.Columns().AdjustToContents();
                workbook.SaveAs(tmp);
            });
        }

        public static List<DeviceDefinition> ReadAllDevices(string path)
            => ReadAllDevices(path, null);

        public static List<DeviceDefinition> ReadAllDevices(string path, Action<string>? log)
        {
            var logger = log ?? (_ => { });

            if (!File.Exists(path))
                throw new FileNotFoundException($"Файл не найден: {path}");

            var result = new Dictionary<string, DeviceDefinition>(StringComparer.OrdinalIgnoreCase);
            var order = new List<string>();

            using var workbook = new XLWorkbook(path);
            if (workbook.Worksheets.Count == 0)
                return new List<DeviceDefinition>();

            var ws = workbook.Worksheet(1);
            int lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
            if (lastRow < 2) return new List<DeviceDefinition>();

            for (int rowNum = 2; rowNum <= lastRow; rowNum++)
            {
                var row = ws.Row(rowNum);
                string deviceId = row.Cell(1).GetString().Trim();
                if (string.IsNullOrWhiteSpace(deviceId)) continue;

                string messageName = row.Cell(2).GetString().Trim();
                bool extended = ParseBool01(row.Cell(3), defaultValue: true);
                int dlc = (int)(GetNumber(row.Cell(4)) ?? 8);
                dlc = Math.Clamp(dlc, 1, 8);

                string header = row.Cell(6).GetString().Trim();
                string type = row.Cell(7).GetString().Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(type)) type = "NUM";

                int startBit = (int)(GetNumber(row.Cell(8)) ?? 0);
                int length = (int)(GetNumber(row.Cell(9)) ?? 0);
                bool littleEndian = ParseByteOrder(row.Cell(10));
                bool signedRaw = ParseSigned(row.Cell(11));
                double scale = GetNumber(row.Cell(12)) ?? 1.0;
                double offset = GetNumber(row.Cell(13)) ?? 0.0;
                string? unit = row.Cell(14).GetString().Trim();
                if (string.IsNullOrEmpty(unit)) unit = null;
                double? minP = GetNumber(row.Cell(15));
                double? maxP = GetNumber(row.Cell(16));
                int? bitStart = GetNumber(row.Cell(17)) is double b ? (int)b : null;

                int fieldIndex = (int)(GetNumber(row.Cell(5)) ?? 0);

                var field = new DeviceFieldRow(
                    FieldIndex: fieldIndex,
                    Header: header,
                    Type: type,
                    StartBit: startBit,
                    Length: length,
                    IsLittleEndian: littleEndian,
                    SignedRaw: signedRaw,
                    Scale: scale,
                    Offset: offset,
                    Unit: unit,
                    MinPhys: minP,
                    MaxPhys: maxP,
                    BitStart: bitStart);

                if (!result.TryGetValue(deviceId, out var def))
                {
                    def = new DeviceDefinition
                    {
                        DeviceId = deviceId,
                        MessageName = messageName,
                        Extended = extended,
                        Dlc = dlc
                    };
                    result[deviceId] = def;
                    order.Add(deviceId);
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(messageName)
                        && !string.Equals(messageName, def.MessageName, StringComparison.Ordinal))
                    {
                        logger($"Предупреждение: строка {rowNum}: разные MessageName для DeviceID={deviceId} ('{def.MessageName}' vs '{messageName}'). Оставлено '{def.MessageName}'.");
                    }
                    if (extended != def.Extended)
                        logger($"Предупреждение: строка {rowNum}: разные Extended для DeviceID={deviceId}. Оставлено '{(def.Extended ? "Extended" : "Standard")}'.");
                    if (dlc != def.Dlc)
                        logger($"Предупреждение: строка {rowNum}: разные DLC для DeviceID={deviceId} ({def.Dlc} vs {dlc}). Оставлено {def.Dlc}.");
                }

                def.Rows.Add(field);
            }

            foreach (var def in result.Values)
            {
                def.Rows.Sort((a, b) => a.FieldIndex.CompareTo(b.FieldIndex));
            }

            return order.Select(id => result[id]).ToList();
        }

        public static void WriteAllDevices(string path, IReadOnlyList<DeviceDefinition> devices)
        {
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            SafeFileWriter.Write(path, tmp =>
            {
                using var workbook = new XLWorkbook();
                var ws = workbook.Worksheets.Add("Devices");
                WriteHeader(ws);

                int row = 2;
                foreach (var dev in devices)
                {
                    int fieldIndex = 0;
                    foreach (var f in dev.Rows)
                    {
                        WriteRow(ws, row, dev, fieldIndex, f);
                        fieldIndex++;
                        row++;
                    }
                }

                ws.SheetView.FreezeRows(1);
                ws.Columns().AdjustToContents();
                workbook.SaveAs(tmp);
            });
        }

        public static void AppendDeviceFields(string path, string deviceId, IReadOnlyList<DeviceFieldRow> rows, DeviceDefinition? messageMeta = null)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
                throw new ArgumentException("DeviceID не задан.", nameof(deviceId));
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            if (!File.Exists(path))
                throw new FileNotFoundException($"Файл не найден: {path}");

            var all = ReadAllDevices(path);
            var existing = all.FirstOrDefault(d => d.DeviceId.Equals(deviceId, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                existing = messageMeta ?? new DeviceDefinition
                {
                    DeviceId = deviceId,
                    MessageName = "",
                    Extended = true,
                    Dlc = 8
                };
                existing.DeviceId = deviceId;
                all.Add(existing);
            }
            foreach (var r in rows) existing.Rows.Add(r);

            WriteAllDevices(path, all);
        }

        private static void WriteRow(IXLWorksheet ws, int row, DeviceDefinition dev, int fieldIndex, DeviceFieldRow r)
        {
            ws.Cell(row, 1).Value = dev.DeviceId;
            ws.Cell(row, 2).Value = dev.MessageName ?? "";
            ws.Cell(row, 3).Value = dev.Extended ? 1 : 0;
            ws.Cell(row, 4).Value = dev.Dlc;
            ws.Cell(row, 5).Value = fieldIndex;
            ws.Cell(row, 6).Value = r.Header ?? "";
            ws.Cell(row, 7).Value = r.Type;

            string type = (r.Type ?? "").Trim().ToUpperInvariant();
            if (type == "NUM")
            {
                ws.Cell(row, 8).Value = r.StartBit;
                ws.Cell(row, 9).Value = r.Length;
                ws.Cell(row, 10).Value = r.IsLittleEndian ? "Intel" : "Motorola";
                ws.Cell(row, 11).Value = r.SignedRaw ? "-" : "+";
                ws.Cell(row, 12).Value = r.Scale;
                ws.Cell(row, 13).Value = r.Offset;
                ws.Cell(row, 14).Value = r.Unit ?? "";
                if (r.MinPhys.HasValue) ws.Cell(row, 15).Value = r.MinPhys.Value;
                if (r.MaxPhys.HasValue) ws.Cell(row, 16).Value = r.MaxPhys.Value;
            }
            else if (type == "BIN")
            {
                ws.Cell(row, 8).Value = r.StartBit;
                ws.Cell(row, 9).Value = r.Length;
                if (r.BitStart.HasValue)
                    ws.Cell(row, 17).Value = r.BitStart.Value;
            }
        }

        private static void WriteHeader(IXLWorksheet ws)
        {
            for (int c = 0; c < HeaderRow.Length; c++)
            {
                var cell = ws.Cell(1, c + 1);
                cell.Value = HeaderRow[c];
                cell.Style.Font.Bold = true;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }
        }

        private static double? GetNumber(IXLCell cell)
        {
            if (cell.IsEmpty()) return null;
            if (cell.TryGetValue(out double d) && !double.IsNaN(d)) return d;
            var s = cell.GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(s)) return null;
            return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed) ? parsed : null;
        }

        private static bool ParseBool01(IXLCell cell, bool defaultValue)
        {
            if (cell.IsEmpty()) return defaultValue;
            var s = cell.GetString().Trim();
            if (s.Length == 0)
            {
                if (cell.TryGetValue(out double d) && !double.IsNaN(d))
                    return Math.Abs(d) >= 0.5;
                return defaultValue;
            }
            if (s.Equals("1", StringComparison.Ordinal) || s.Equals("true", StringComparison.OrdinalIgnoreCase)
                || s.Equals("yes", StringComparison.OrdinalIgnoreCase) || s.Equals("extended", StringComparison.OrdinalIgnoreCase))
                return true;
            if (s.Equals("0", StringComparison.Ordinal) || s.Equals("false", StringComparison.OrdinalIgnoreCase)
                || s.Equals("no", StringComparison.OrdinalIgnoreCase) || s.Equals("standard", StringComparison.OrdinalIgnoreCase))
                return false;
            return defaultValue;
        }

        private static bool ParseByteOrder(IXLCell cell)
        {
            var s = cell.GetString().Trim();
            if (s.Length == 0) return true;
            if (s.Equals("Motorola", StringComparison.OrdinalIgnoreCase) || s.Equals("0", StringComparison.Ordinal))
                return false;
            return true;
        }

        private static bool ParseSigned(IXLCell cell)
        {
            var s = cell.GetString().Trim();
            if (s.Length == 0) return false;
            if (s.Equals("-", StringComparison.Ordinal) || s.Equals("signed", StringComparison.OrdinalIgnoreCase)
                || s.Equals("1", StringComparison.Ordinal) || s.Equals("true", StringComparison.OrdinalIgnoreCase))
                return true;
            return false;
        }
    }
}
