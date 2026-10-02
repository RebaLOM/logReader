using ClosedXML.Excel;

namespace logReader.Processing
{
    // Step-таблица («Шаг», «Время», параметры устройств) с построчной записью в CSV или XLSX.
    // До Complete() результат пишется во временный файл; Dispose без Complete удаляет его.
    internal sealed class StepOutputWriter : IDisposable
    {
        private readonly string _outputPath;
        private readonly IReadOnlyList<OutputColumnGroup> _columns;
        private readonly List<string> _row = new();
        private string? _csvTempPath;
        private StreamWriter? _csv;
        private XLWorkbook? _workbook;
        private IXLWorksheet? _sheet;
        private int _excelRow;

        public long RowsWritten { get; private set; }

        public StepOutputWriter(string outputPath, OutputFormat format, IReadOnlyList<OutputColumnGroup> columns, bool includeDeviceIdRow)
        {
            _outputPath = outputPath;
            _columns = columns;

            if (format == OutputFormat.Xlsx)
            {
                _workbook = new XLWorkbook();
                _sheet = _workbook.Worksheets.Add("Log");
                _excelRow = ExcelLayoutBuilder.BuildStepLogHeaders(_sheet, columns, includeDeviceIdRow);
                return;
            }

            _csvTempPath = SafeFileWriter.CreateTempPath(outputPath);
            _csv = new StreamWriter(_csvTempPath, false, CsvOutput.Encoding);
            WriteCsvHeaders(includeDeviceIdRow);
        }

        public void WriteRow(int step, string time)
        {
            if (_sheet != null)
            {
                if (_excelRow > ExcelLayoutBuilder.MaxRows)
                    throw new ExcelRowLimitException();

                _sheet.Cell(_excelRow, 1).Value = step;
                _sheet.Cell(_excelRow, 2).Value = time;
                int col = 3;
                foreach (var group in _columns)
                    foreach (int idx in group.ParamIndexes)
                        ExcelLayoutBuilder.SetCellValue(_sheet.Cell(_excelRow, col++), group.Device, idx);
                _excelRow++;
            }
            else
            {
                _row.Clear();
                _row.Add(step.ToString(System.Globalization.CultureInfo.InvariantCulture));
                _row.Add(CsvOutput.Text(time));
                foreach (var group in _columns)
                    foreach (int idx in group.ParamIndexes)
                        _row.Add(group.Device.FormatValue(idx));
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
                    ExcelLayoutBuilder.AutoFitColumns(_sheet!);
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
                CsvOutput.WriteRow(_csv!, idRow);
            }

            var headerRow = new List<string> { "Шаг", "Время" };
            foreach (var group in _columns)
                headerRow.AddRange(group.Headers.Select(CsvOutput.Text));
            CsvOutput.WriteRow(_csv!, headerRow);
        }
    }

    internal sealed class ExcelRowLimitException : Exception
    {
        public ExcelRowLimitException()
            : base($"Результат не помещается в лист Excel ({ExcelLayoutBuilder.MaxRows:N0} строк). Выберите формат CSV или уменьшите объём данных фильтрами.")
        {
        }
    }
}
