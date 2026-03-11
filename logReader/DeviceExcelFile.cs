using ClosedXML.Excel;

namespace logReader
{
    public sealed record DeviceFieldRow(
        string Header,
        int LowByte,
        int? HighByte,
        string Type,
        double? Scale,
        double? Offset,
        int? StartBit,
        int? LenghtBit);

    public static class DeviceExcelFile
    {
        private static readonly string[] HeaderRow =
        {
            "DeviceID",
            "FieldIndex",
            "Header",
            "LowByte",
            "HighByte",
            "Scale",
            "Offset",
            "Type",
            "StartBit",
            "LenghtBit"
        };

        public static void CreateDevicesExcelTemplate(string path)
        {
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Devices");

            WriteHeader(ws);
            ws.SheetView.FreezeRows(1);
            ws.Columns().AdjustToContents();

            workbook.SaveAs(path);
        }

        public static void AppendDeviceFields(string path, string deviceId, IReadOnlyList<DeviceFieldRow> rows)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
                throw new ArgumentException("DeviceID не задан.", nameof(deviceId));
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            if (!File.Exists(path))
                throw new FileNotFoundException($"Файл не найден: {path}");

            using var workbook = new XLWorkbook(path);
            var ws = workbook.Worksheets.Count > 0
                ? workbook.Worksheet(1)
                : workbook.Worksheets.Add("Devices");

            EnsureHeader(ws);

            int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
            int nextFieldIndex = GetNextFieldIndex(ws, deviceId, lastRow);

            int writeRow = Math.Max(lastRow + 1, 2);
            foreach (var r in rows)
            {
                ws.Cell(writeRow, 1).Value = deviceId.Trim();
                ws.Cell(writeRow, 2).Value = nextFieldIndex++;
                ws.Cell(writeRow, 3).Value = r.Header?.Trim() ?? "";
                ws.Cell(writeRow, 4).Value = r.LowByte;

                if (r.HighByte.HasValue)
                    ws.Cell(writeRow, 5).Value = r.HighByte.Value;

                string type = (r.Type ?? "").Trim().ToUpperInvariant();
                ws.Cell(writeRow, 8).Value = type;

                if (type == "NUM")
                {
                    if (r.Scale.HasValue)
                        ws.Cell(writeRow, 6).Value = r.Scale.Value;
                    if (r.Offset.HasValue)
                        ws.Cell(writeRow, 7).Value = r.Offset.Value;
                    // StartBit / LenghtBit оставляем пустыми
                }
                else if (type == "BIN")
                {
                    if (r.StartBit.HasValue)
                        ws.Cell(writeRow, 9).Value = r.StartBit.Value;
                    if (r.LenghtBit.HasValue)
                        ws.Cell(writeRow, 10).Value = r.LenghtBit.Value;
                    // Scale / Offset оставляем пустыми
                }

                writeRow++;
            }

            ws.Columns().AdjustToContents();
            workbook.SaveAs(path);
        }

        private static void EnsureHeader(IXLWorksheet ws)
        {
            // Пустой лист — пишем заголовки
            var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
            if (lastRow == 0)
            {
                WriteHeader(ws);
                return;
            }

            string first = ws.Cell(1, 1).GetString().Trim();
            if (string.IsNullOrWhiteSpace(first))
            {
                WriteHeader(ws);
                return;
            }

            if (!first.Equals(HeaderRow[0], StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Неверный формат Excel-файла устройств: в A1 ожидается 'DeviceID'.");
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

        private static int GetNextFieldIndex(IXLWorksheet ws, string deviceId, int lastRow)
        {
            int next = 0;
            for (int r = 2; r <= lastRow; r++)
            {
                string id = ws.Cell(r, 1).GetString().Trim();
                if (!id.Equals(deviceId, StringComparison.OrdinalIgnoreCase)) continue;

                if (TryGetInt(ws.Cell(r, 2), out int fieldIndex))
                    next = Math.Max(next, fieldIndex + 1);
            }
            return next;
        }

        private static bool TryGetInt(IXLCell cell, out int value)
        {
            value = 0;

            if (cell.IsEmpty()) return false;
            if (cell.TryGetValue(out double d) && !double.IsNaN(d))
            {
                value = (int)d;
                return true;
            }

            var s = cell.GetString()?.Trim();
            return int.TryParse(s, out value);
        }
    }
}
