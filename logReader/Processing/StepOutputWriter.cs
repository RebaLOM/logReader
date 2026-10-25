using ClosedXML.Excel;
using System.Text;

namespace logReader.Processing
{
    // Step-таблица («Шаг», «Время», параметры устройств) с построчной записью в CSV или XLSX.
    // До Complete() результат пишется во временный файл; Dispose без Complete удаляет его.
    // При заполнении листа Excel создаётся следующий (Log, Log_2, …) с той же шапкой.
    internal sealed class StepOutputWriter : IDisposable
    {
        private readonly string _outputPath;
        private readonly IReadOnlyList<OutputColumnGroup> _columns;
        private readonly bool _includeDeviceIdRow;
        private readonly bool _includeDtc;
        private readonly bool _autoIncludeDtc;
        private readonly DtcDisplayFormat _dtcDisplayFormat;
        private readonly ProcessingContext _context;
        private bool _hasDtcCodes;
        private readonly List<string> _row = new();
        private string? _csvTempPath;
        private StreamWriter? _csv;
        private string? _dtcTempPath;
        private StreamWriter? _dtcCsv;
        private XLWorkbook? _workbook;
        private IXLWorksheet? _sheet;
        private int _excelRow;
        private int _sheetPart = 1;
        private readonly string _sheetBaseName = "Log";

        public long RowsWritten { get; private set; }
        public int SheetCount { get; private set; }
        internal bool HasDtcColumns => _includeDtc || (_autoIncludeDtc && _hasDtcCodes);

        public StepOutputWriter(string outputPath, OutputFormat format, IReadOnlyList<OutputColumnGroup> columns, bool includeDeviceIdRow,
            bool includeDtc = false, bool autoIncludeDtc = false, DtcDisplayFormat dtcDisplayFormat = DtcDisplayFormat.Compact,
            ProcessingContext? context = null)
        {
            _outputPath = outputPath;
            _columns = columns;
            _includeDeviceIdRow = includeDeviceIdRow;
            _includeDtc = includeDtc;
            _autoIncludeDtc = autoIncludeDtc;
            _dtcDisplayFormat = dtcDisplayFormat;
            _context = context ?? new ProcessingContext();

            if (format == OutputFormat.Xlsx)
            {
                _workbook = new XLWorkbook();
                StartNewSheet();
                return;
            }

            try
            {
                _csvTempPath = SafeFileWriter.CreateTempPath(outputPath);
                _csv = new StreamWriter(_csvTempPath, false, CsvOutput.Encoding);
                WriteCsvHeaders(includeDeviceIdRow);
                if (autoIncludeDtc && !includeDtc)
                {
                    _dtcTempPath = SafeFileWriter.CreateTempPath(outputPath);
                    _dtcCsv = new StreamWriter(_dtcTempPath, false, CsvOutput.Encoding);
                }
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void WriteRow(int step, string time, DtcSnapshot? dtc = null)
        {
            _hasDtcCodes |= DtcOutput.HasCodes(dtc);
            if (_sheet != null)
            {
                if (_excelRow > ExcelLayoutBuilder.RowsPerSheet)
                    StartNewSheet();

                _sheet.Cell(_excelRow, 1).Value = step;
                _sheet.Cell(_excelRow, 2).Value = time;
                int col = 3;
                foreach (var group in _columns)
                    foreach (int idx in group.ParamIndexes)
                        ExcelLayoutBuilder.SetCellValue(_sheet.Cell(_excelRow, col++), group.Device, idx);
                if (HasDtcColumns) DtcOutput.WriteFields(_sheet, _excelRow, col, dtc, _dtcDisplayFormat);
                _excelRow++;
            }
            else
            {
                _row.Clear();
                _row.Add(step.ToString(System.Globalization.CultureInfo.InvariantCulture));
                // Колонка времени — как в логе (HH:mm:ss[.fff]), без замены точки на запятую.
                _row.Add(CsvOutput.Text(time));
                foreach (var group in _columns)
                    foreach (int idx in group.ParamIndexes)
                        _row.Add(group.Device.FormatValue(idx));
                if (_includeDtc) _row.AddRange(DtcOutput.Fields(dtc, _dtcDisplayFormat));
                CsvOutput.WriteRow(_csv!, _row);
                if (_dtcCsv != null) CsvOutput.WriteRow(_dtcCsv, DtcOutput.Fields(dtc, _dtcDisplayFormat));
            }

            RowsWritten++;
        }

        public void Complete()
        {
            _context.ThrowIfCancellationRequested();
            if (_workbook != null)
            {
                SafeFileWriter.Write(_outputPath, tmp =>
                {
                    foreach (var ws in _workbook.Worksheets)
                    {
                        if (HasDtcColumns && !_includeDtc)
                            DtcOutput.WriteHeaders(ws, 3 + _columns.Sum(c => c.ParamIndexes.Length), _includeDeviceIdRow, false);
                        ExcelLayoutBuilder.AutoFitColumns(ws);
                        ExcelLayoutBuilder.EnsureStepColumnMinWidth(ws);
                    }
                    _workbook.SaveAs(tmp);
                    _context.ThrowIfCancellationRequested();
                });
                return;
            }

            _csv!.Dispose();
            _csv = null;
            _dtcCsv?.Dispose();
            _dtcCsv = null;
            if (_dtcTempPath != null && HasDtcColumns)
            {
                CompleteAutomaticCsv();
                return;
            }
            _context.ThrowIfCancellationRequested();
            SafeFileWriter.Publish(_csvTempPath!, _outputPath);
            _csvTempPath = null;
        }

        public void Dispose()
        {
            _csv?.Dispose();
            _dtcCsv?.Dispose();
            SafeFileWriter.TryDelete(_csvTempPath);
            SafeFileWriter.TryDelete(_dtcTempPath);
            _workbook?.Dispose();
        }

        // Numeric CSV remains untouched if no DTC is found. Otherwise add the
        // six spooled text fields after reading each complete (possibly multiline)
        // CSV record, so quoted parameter headings are preserved verbatim.
        private void CompleteAutomaticCsv()
        {
            string mergedPath = SafeFileWriter.CreateTempPath(_outputPath);
            try
            {
                using (var numeric = new StreamReader(_csvTempPath!, CsvOutput.Encoding))
                using (var diagnostics = new StreamReader(_dtcTempPath!, CsvOutput.Encoding))
                using (var merged = new StreamWriter(mergedPath, false, CsvOutput.Encoding))
                {
                    int recordIndex = 0;
                    int headerRows = _includeDeviceIdRow ? 2 : 1;
                    while (ReadCsvRecord(numeric) is { } record)
                    {
                        if ((recordIndex & 4095) == 0) _context.ThrowIfCancellationRequested();
                        merged.Write(record);
                        merged.Write(CsvOutput.Delimiter);
                        if (recordIndex < headerRows)
                            merged.Write(string.Join(CsvOutput.Delimiter, recordIndex == headerRows - 1
                                ? DtcOutput.Headers : Enumerable.Repeat("", DtcOutput.Headers.Length)));
                        else
                            merged.Write(diagnostics.ReadLine() ?? throw new InvalidDataException("Нарушено соответствие строк DTC."));
                        merged.WriteLine();
                        recordIndex++;
                    }
                }
                _context.ThrowIfCancellationRequested();
                SafeFileWriter.Publish(mergedPath, _outputPath);
                SafeFileWriter.TryDelete(_csvTempPath);
                _csvTempPath = null;
            }
            finally { SafeFileWriter.TryDelete(mergedPath); }
        }

        private static string? ReadCsvRecord(StreamReader reader)
        {
            var record = new StringBuilder();
            bool quoted = false;
            while (reader.Read() is var value && value >= 0)
            {
                char current = (char)value;
                if (current == '"')
                {
                    record.Append(current);
                    if (quoted && reader.Peek() == '"') record.Append((char)reader.Read());
                    else quoted = !quoted;
                    continue;
                }
                if (!quoted && current is '\r' or '\n')
                {
                    if (current == '\r' && reader.Peek() == '\n') reader.Read();
                    return record.ToString();
                }
                record.Append(current);
            }
            return record.Length == 0 ? null : record.ToString();
        }

        private void StartNewSheet()
        {
            string name = ExcelLayoutBuilder.SheetNameForPart(_sheetBaseName, _sheetPart);
            _sheet = _workbook!.Worksheets.Add(name);
            _excelRow = ExcelLayoutBuilder.BuildStepLogHeaders(_sheet, _columns, _includeDeviceIdRow);
            if (_includeDtc)
                DtcOutput.WriteHeaders(_sheet, 3 + _columns.Sum(c => c.ParamIndexes.Length), _includeDeviceIdRow, false);
            SheetCount = _sheetPart;
            _sheetPart++;
        }

        private void WriteCsvHeaders(bool includeDeviceIdRow)
        {
            // «Шаг»/«Время» всегда в строке с именами параметров; строка ID — только CAN ID.
            if (includeDeviceIdRow)
            {
                var idRow = new List<string> { "", "" };
                foreach (var group in _columns)
                {
                    idRow.Add(CsvOutput.Text(group.Device.ID));
                    for (int i = 1; i < group.ParamIndexes.Length; i++)
                        idRow.Add("");
                }
                if (_includeDtc) idRow.AddRange(Enumerable.Repeat("", DtcOutput.Headers.Length));
                CsvOutput.WriteRow(_csv!, idRow);
            }

            var headerRow = new List<string> { "Шаг", "Время" };
            foreach (var group in _columns)
                headerRow.AddRange(group.Headers.Select(CsvOutput.Text));
            if (_includeDtc) headerRow.AddRange(DtcOutput.Headers);
            CsvOutput.WriteRow(_csv!, headerRow);
        }
    }
}
