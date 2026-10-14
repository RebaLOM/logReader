using ClosedXML.Excel;

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
        private readonly List<string> _row = new();
        private string? _csvTempPath;
        private StreamWriter? _csv;
        private XLWorkbook? _workbook;
        private IXLWorksheet? _sheet;
        private int _excelRow;
        private int _sheetPart = 1;
        private readonly string _sheetBaseName = "Log";

        public long RowsWritten { get; private set; }
        public int SheetCount { get; private set; }

        public StepOutputWriter(string outputPath, OutputFormat format, IReadOnlyList<OutputColumnGroup> columns, bool includeDeviceIdRow, bool includeDtc = false)
        {
            _outputPath = outputPath;
            _columns = columns;
            _includeDeviceIdRow = includeDeviceIdRow;
            _includeDtc = includeDtc;

            if (format == OutputFormat.Xlsx)
            {
                _workbook = new XLWorkbook();
                StartNewSheet();
                return;
            }

            _csvTempPath = SafeFileWriter.CreateTempPath(outputPath);
            _csv = new StreamWriter(_csvTempPath, false, CsvOutput.Encoding);
            WriteCsvHeaders(includeDeviceIdRow);
        }

        public void WriteRow(int step, string time, DtcSnapshot? dtc = null)
        {
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
                if (_includeDtc) DtcOutput.WriteFields(_sheet, _excelRow, col, dtc);
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
                if (_includeDtc) _row.AddRange(DtcOutput.Fields(dtc));
                CsvOutput.WriteRow(_csv!, _row);
            }

            RowsWritten++;
        }

        public void Complete()
        {
            if (_workbook != null)
            {
                SafeFileWriter.Write(_outputPath, tmp =>
                {
                    foreach (var ws in _workbook.Worksheets)
                        ExcelLayoutBuilder.AutoFitColumns(ws);
                    _workbook.SaveAs(tmp);
                });
                return;
            }

            _csv!.Dispose();
            _csv = null;
            SafeFileWriter.Publish(_csvTempPath!, _outputPath);
            _csvTempPath = null;
        }

        public void Dispose()
        {
            _csv?.Dispose();
            SafeFileWriter.TryDelete(_csvTempPath);
            _workbook?.Dispose();
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
