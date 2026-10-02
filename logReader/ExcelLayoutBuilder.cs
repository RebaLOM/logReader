using ClosedXML.Excel;

namespace logReader
{
    // Общая шапка Excel: стили и раскладка для step-таблиц и time-series логов.
    public static class ExcelLayoutBuilder
    {
        public const int MaxRows = 1_048_576;

        // Ширина колонок подбирается по первым строкам: полный AdjustToContents на сотнях тысяч
        // строк занимает больше времени, чем вся обработка лога.
        public const int AutoFitSampleRows = 300;

        public static readonly XLColor[] DeviceColors =
        {
            XLColor.FromArgb(198, 214, 240),
            XLColor.FromArgb(198, 232, 210),
            XLColor.FromArgb(255, 229, 190),
            XLColor.FromArgb(230, 210, 240),
            XLColor.FromArgb(255, 210, 210),
            XLColor.FromArgb(210, 245, 245),
            XLColor.FromArgb(255, 240, 180),
            XLColor.FromArgb(200, 240, 220),
            XLColor.FromArgb(240, 210, 200),
            XLColor.FromArgb(220, 220, 240),
        };

        private static readonly XLColor FixedColumnGray = XLColor.FromArgb(180, 180, 180);

        public static int HeaderRowCount(bool includeDeviceIdRow) => includeDeviceIdRow ? 2 : 1;

        // Step-таблица: «Шаг»/«Время» всегда в строке с именами параметров;
        // includeDeviceIdRow — сверху строка с ID устройств. Возвращает номер первой строки данных.
        public static int BuildStepLogHeaders(IXLWorksheet ws, IReadOnlyList<OutputColumnGroup> columns, bool includeDeviceIdRow)
        {
            int headerRows = HeaderRowCount(includeDeviceIdRow);
            int paramRow = headerRows;

            WriteFixedColumn(ws, 1, "Шаг", paramRow, headerRows);
            WriteFixedColumn(ws, 2, "Время", paramRow, headerRows);

            int col = 3;
            for (int g = 0; g < columns.Count; g++)
            {
                var group = columns[g];
                XLColor bg = DeviceColors[g % DeviceColors.Length];

                if (includeDeviceIdRow)
                    WriteDeviceIdRow(ws, col, group.Device.ID, group.ParamIndexes.Length, bg);

                foreach (var header in group.Headers)
                {
                    var cell = ws.Cell(paramRow, col++);
                    cell.Value = header;
                    ApplyParamHeaderStyle(cell, bg);
                }
            }

            ws.SheetView.FreezeRows(headerRows);
            return headerRows + 1;
        }

        // Time-series: у каждого устройства «Время» + параметры.
        public static int BuildTimeSeriesHeaders(IXLWorksheet ws, IReadOnlyList<OutputColumnGroup> columns, bool includeDeviceIdRow)
        {
            int headerRows = HeaderRowCount(includeDeviceIdRow);
            int paramRow = headerRows;

            int col = 1;
            for (int g = 0; g < columns.Count; g++)
            {
                var group = columns[g];
                XLColor bg = DeviceColors[g % DeviceColors.Length];

                if (includeDeviceIdRow)
                    WriteDeviceIdRow(ws, col, group.Device.ID, 1 + group.ParamIndexes.Length, bg);

                ApplyParamHeaderStyle(ws.Cell(paramRow, col), bg);
                ws.Cell(paramRow, col).Value = "Время";
                col++;

                foreach (var header in group.Headers)
                {
                    var cell = ws.Cell(paramRow, col++);
                    cell.Value = header;
                    ApplyParamHeaderStyle(cell, bg);
                }
            }

            ws.SheetView.FreezeRows(headerRows);
            return headerRows + 1;
        }

        public static void AutoFitColumns(IXLWorksheet ws)
        {
            int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
            ws.Columns().AdjustToContents(1, Math.Min(lastRow, AutoFitSampleRows));
        }

        public static void SetCellValue(IXLCell cell, Device device, int paramIndex)
        {
            if (device.FieldErrors[paramIndex])
                cell.Value = "ERR";
            else if (!double.IsNaN(device.Values[paramIndex]))
                cell.Value = device.Values[paramIndex];
        }

        private static void WriteFixedColumn(IXLWorksheet ws, int col, string title, int titleRow, int headerRows)
        {
            for (int row = 1; row <= headerRows; row++)
                StyleMergedHeader(ws, row, col, FixedColumnGray);
            ws.Cell(titleRow, col).Value = title;
        }

        // Строка 1: ID устройства на blockCols колонок.
        private static void WriteDeviceIdRow(IXLWorksheet ws, int startCol, string deviceId, int blockCols, XLColor bg)
        {
            int devEndCol = startCol + blockCols - 1;
            if (startCol != devEndCol)
                ws.Range(1, startCol, 1, devEndCol).Merge();
            ws.Cell(1, startCol).Value = deviceId;
            ApplyDeviceIdHeaderStyle(ws.Cell(1, startCol), bg);
        }

        private static void StyleMergedHeader(IXLWorksheet ws, int row, int col, XLColor bg)
        {
            var cell = ws.Cell(row, col);
            cell.Style.Fill.BackgroundColor = bg;
            cell.Style.Font.Bold = true;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        public static void ApplyDeviceIdHeaderStyle(IXLCell cell, XLColor bg)
        {
            var darker = XLColor.FromArgb(
                Math.Max(bg.Color.R - 30, 0),
                Math.Max(bg.Color.G - 30, 0),
                Math.Max(bg.Color.B - 30, 0));
            cell.Style.Fill.BackgroundColor = darker;
            cell.Style.Font.Bold = true;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        public static void ApplyParamHeaderStyle(IXLCell cell, XLColor bg)
        {
            cell.Style.Fill.BackgroundColor = bg;
            cell.Style.Font.Bold = true;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Alignment.WrapText = true;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }
    }
}
