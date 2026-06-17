using System.Globalization;
using ClosedXML.Excel;
using static logReader.XlsxCellReader;

namespace logReader
{
    // xlsx составных параметров: одна строка = один кусок; Piece задаёт порядок склейки (0 — старшие биты).
    public static class CompositeExcelFile
    {
        public static readonly string[] HeaderRow =
        {
            "Block",      // 1  имя блока (группа колонок). Пусто = COMPOSITE
            "Param",      // 2  имя составного параметра (заголовок колонки)
            "Piece",      // 3  порядок куска (0 = старшие биты)
            "SourceID",   // 4  ID посылки-источника (hex)
            "Byte",       // 5  индекс байта 0..7
            "BitStart",   // 6  младший бит внутри байта 0..7
            "BitLen",     // 7  длина в битах 1..8
            "Trigger",    // 8  1 = триггер (момент формирования). Пусто = последний кусок
            "Scale",      // 9
            "Offset",     // 10
            "Signed",     // 11 +/-
            "Unit",       // 12
            "Min",        // 13
            "Max"         // 14
        };

        public const int ColCount = 14;

        public static void CreateTemplate(string path)
        {
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            SafeFileWriter.Write(path, tmp =>
            {
                using var workbook = new XLWorkbook();
                var ws = workbook.Worksheets.Add("Composites");
                WriteHeader(ws);
                ws.SheetView.FreezeRows(1);
                ws.Columns().AdjustToContents();
                workbook.SaveAs(tmp);
            });
        }

        public static List<CompositeSignal> ReadAll(string path) => ReadAll(path, null);

        public static List<CompositeSignal> ReadAll(string path, Action<string>? log)
        {
            var logger = log ?? (_ => { });

            if (!File.Exists(path))
                throw new FileNotFoundException($"Файл не найден: {path}");

            using var workbook = new XLWorkbook(path);
            if (workbook.Worksheets.Count == 0)
                return new List<CompositeSignal>();

            var ws = workbook.Worksheet(1);
            int lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
            if (lastRow < 2) return new List<CompositeSignal>();

            // Block|Param -> сигнал; куски накапливаем по Piece.
            var map = new Dictionary<string, CompositeSignal>(StringComparer.Ordinal);
            var order = new List<string>();
            var pieceOrder = new Dictionary<string, List<(int Order, CompositePiece Piece, bool IsTrigger)>>(StringComparer.Ordinal);

            for (int rowNum = 2; rowNum <= lastRow; rowNum++)
            {
                var row = ws.Row(rowNum);

                string param = row.Cell(2).GetString().Trim();
                if (string.IsNullOrWhiteSpace(param)) continue;

                string sourceId = row.Cell(4).GetString().Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(sourceId))
                {
                    logger($"Составные: строка {rowNum}: пустой SourceID — пропуск.");
                    continue;
                }

                string block = row.Cell(1).GetString().Trim();
                if (string.IsNullOrWhiteSpace(block)) block = CompositeDefaults.BlockName;

                int pieceIdx = (int)(GetNumber(row.Cell(3)) ?? (pieceOrder.TryGetValue(block + "|" + param, out var pl) ? pl.Count : 0));
                int byteIdx = (int)(GetNumber(row.Cell(5)) ?? -1);
                int bitStart = (int)(GetNumber(row.Cell(6)) ?? 0);
                int bitLen = (int)(GetNumber(row.Cell(7)) ?? 8);
                bool isTrigger = ParseBool01(row.Cell(8));

                if (byteIdx < 0 || byteIdx > 7)
                {
                    logger($"Составные: строка {rowNum} ('{param}'): Byte должен быть 0..7 — пропуск куска.");
                    continue;
                }
                if (bitStart < 0 || bitStart > 7)
                {
                    logger($"Составные: строка {rowNum} ('{param}'): BitStart должен быть 0..7 — пропуск куска.");
                    continue;
                }
                if (bitLen < 1 || bitLen > 8)
                {
                    logger($"Составные: строка {rowNum} ('{param}'): BitLen должен быть 1..8 — пропуск куска.");
                    continue;
                }
                if (bitStart + bitLen > 8)
                {
                    logger($"Составные: строка {rowNum} ('{param}'): BitStart+BitLen не должны превышать 8 — пропуск куска.");
                    continue;
                }

                string key = block + "|" + param;
                if (!map.TryGetValue(key, out var sig))
                {
                    sig = new CompositeSignal
                    {
                        Block = block,
                        Param = param,
                        Scale = GetNumber(row.Cell(9)) ?? 1.0,
                        Offset = GetNumber(row.Cell(10)) ?? 0.0,
                        Signed = ParseSigned(row.Cell(11)),
                        Unit = NullIfEmpty(row.Cell(12).GetString().Trim()) ?? "",
                        Min = GetNumber(row.Cell(13)),
                        Max = GetNumber(row.Cell(14)),
                    };
                    map[key] = sig;
                    order.Add(key);
                    pieceOrder[key] = new List<(int, CompositePiece, bool)>();
                }
                else
                {
                    // Scale/Offset/Min/Max могут быть на любой строке куска — подхватываем при чтении.
                    if (GetNumber(row.Cell(9)) is double sc) sig.Scale = sc;
                    if (GetNumber(row.Cell(10)) is double of) sig.Offset = of;
                    if (!row.Cell(11).IsEmpty()) sig.Signed = ParseSigned(row.Cell(11));
                    string u = row.Cell(12).GetString().Trim();
                    if (!string.IsNullOrEmpty(u)) sig.Unit = u;
                    if (GetNumber(row.Cell(13)) is double mn) sig.Min = mn;
                    if (GetNumber(row.Cell(14)) is double mx) sig.Max = mx;
                }

                pieceOrder[key].Add((pieceIdx, new CompositePiece(sourceId, byteIdx, bitStart, bitLen), isTrigger));
            }

            var result = new List<CompositeSignal>();
            foreach (var key in order)
            {
                var sig = map[key];
                var pieces = pieceOrder[key];
                pieces.Sort((a, b) => a.Order.CompareTo(b.Order));

                sig.Pieces = pieces.Select(p => p.Piece).ToList();

                var flagged = pieces.FirstOrDefault(p => p.IsTrigger);
                sig.TriggerId = flagged.Piece != null
                    ? flagged.Piece.SourceId
                    : sig.ResolveDefaultTriggerId();

                if (sig.Pieces.Count > 0)
                    result.Add(sig);
            }

            return result;
        }

        public static void WriteAll(string path, IReadOnlyList<CompositeSignal> signals)
        {
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            SafeFileWriter.Write(path, tmp =>
            {
                using var workbook = new XLWorkbook();
                var ws = workbook.Worksheets.Add("Composites");
                WriteHeader(ws);

                int row = 2;
                foreach (var sig in signals)
                {
                    string trigger = string.IsNullOrWhiteSpace(sig.TriggerId)
                        ? sig.ResolveDefaultTriggerId()
                        : sig.TriggerId;

                    bool triggerWritten = false;
                    for (int i = 0; i < sig.Pieces.Count; i++)
                    {
                        var p = sig.Pieces[i];
                        ws.Cell(row, 1).Value = sig.Block;
                        ws.Cell(row, 2).Value = sig.Param;
                        ws.Cell(row, 3).Value = i;
                        ws.Cell(row, 4).Value = p.SourceId;
                        ws.Cell(row, 5).Value = p.Byte;
                        ws.Cell(row, 6).Value = p.BitStart;
                        ws.Cell(row, 7).Value = p.BitLen;

                        bool isTrigger = !triggerWritten
                            && string.Equals(p.SourceId, trigger, StringComparison.OrdinalIgnoreCase);
                        // Триггер по умолчанию — последний кусок с тем же SourceID.
                        if (isTrigger && i == LastIndexOfSource(sig, trigger))
                        {
                            ws.Cell(row, 8).Value = 1;
                            triggerWritten = true;
                        }

                        if (i == 0)
                        {
                            ws.Cell(row, 9).Value = sig.Scale;
                            ws.Cell(row, 10).Value = sig.Offset;
                            ws.Cell(row, 11).Value = sig.Signed ? "-" : "+";
                            ws.Cell(row, 12).Value = sig.Unit ?? "";
                            if (sig.Min.HasValue) ws.Cell(row, 13).Value = sig.Min.Value;
                            if (sig.Max.HasValue) ws.Cell(row, 14).Value = sig.Max.Value;
                        }

                        row++;
                    }
                }

                ws.SheetView.FreezeRows(1);
                ws.Columns().AdjustToContents();
                workbook.SaveAs(tmp);
            });
        }

        private static int LastIndexOfSource(CompositeSignal sig, string sourceId)
        {
            int idx = -1;
            for (int i = 0; i < sig.Pieces.Count; i++)
                if (string.Equals(sig.Pieces[i].SourceId, sourceId, StringComparison.OrdinalIgnoreCase))
                    idx = i;
            return idx;
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

        private static string? NullIfEmpty(string s) => string.IsNullOrEmpty(s) ? null : s;
    }
}
