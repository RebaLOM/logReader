using ClosedXML.Excel;

namespace logReader
{
    // Перезапись листа описаний в существующей книге: другие листы, оформление шапки,
    // пользовательские колонки справа от известных и строки-заметки сохраняются.
    internal sealed class XlsxSheetRewriter : IDisposable
    {
        private readonly int _knownColumns;
        private readonly Dictionary<string, Queue<XLCellValue[]>> _extrasByKey = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<XLCellValue[]> _noteRows = new();
        private readonly int _lastColumn;

        public XLWorkbook Workbook { get; }
        public IXLWorksheet Sheet { get; }

        private XlsxSheetRewriter(XLWorkbook workbook, IXLWorksheet sheet, int knownColumns, int lastColumn)
        {
            Workbook = workbook;
            Sheet = sheet;
            _knownColumns = knownColumns;
            _lastColumn = lastColumn;
        }

        // keyOf: ключ строки данных для привязки пользовательских колонок; null — строка-заметка.
        public static XlsxSheetRewriter Open(string path, string sheetName, int knownColumns, Func<IXLRow, string?> keyOf)
        {
            if (!File.Exists(path))
            {
                var fresh = new XLWorkbook();
                return new XlsxSheetRewriter(fresh, fresh.Worksheets.Add(sheetName), knownColumns, knownColumns);
            }

            var workbook = new XLWorkbook(path);
            var sheet = workbook.Worksheets.Count > 0 ? workbook.Worksheet(1) : workbook.Worksheets.Add(sheetName);
            int lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
            int lastColumn = Math.Max(knownColumns, sheet.LastColumnUsed()?.ColumnNumber() ?? knownColumns);
            var rewriter = new XlsxSheetRewriter(workbook, sheet, knownColumns, lastColumn);

            for (int r = 2; r <= lastRow; r++)
            {
                var row = sheet.Row(r);
                if (row.IsEmpty()) continue;

                string? key = keyOf(row);
                if (key == null)
                {
                    rewriter._noteRows.Add(ReadCells(row, 1, lastColumn));
                    continue;
                }

                if (lastColumn > knownColumns)
                {
                    if (!rewriter._extrasByKey.TryGetValue(key, out var queue))
                        rewriter._extrasByKey[key] = queue = new Queue<XLCellValue[]>();
                    queue.Enqueue(ReadCells(row, knownColumns + 1, lastColumn));
                }
            }

            if (lastRow >= 2)
                sheet.Rows(2, lastRow).Delete();
            return rewriter;
        }

        public void RestoreExtras(int row, string key)
        {
            if (!_extrasByKey.TryGetValue(key, out var queue) || queue.Count == 0) return;
            var values = queue.Dequeue();
            for (int i = 0; i < values.Length; i++)
                if (!values[i].IsBlank)
                    Sheet.Cell(row, _knownColumns + 1 + i).Value = values[i];
        }

        // Строки без ключа (заметки, разделители) переносятся в конец листа.
        public int AppendNoteRows(int row)
        {
            foreach (var values in _noteRows)
            {
                for (int i = 0; i < values.Length; i++)
                    if (!values[i].IsBlank)
                        Sheet.Cell(row, 1 + i).Value = values[i];
                row++;
            }
            return row;
        }

        public bool HasCustomColumns => _lastColumn > _knownColumns;

        public void Dispose() => Workbook.Dispose();

        private static XLCellValue[] ReadCells(IXLRow row, int fromColumn, int toColumn)
        {
            var values = new XLCellValue[toColumn - fromColumn + 1];
            for (int c = fromColumn; c <= toColumn; c++)
                values[c - fromColumn] = row.Cell(c).Value;
            return values;
        }
    }
}
