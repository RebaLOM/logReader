using ClosedXML.Excel;

namespace logReader
{
    // Общая двухстрочная шапка Excel: стили и раскладка для step-CSV и time-series логов.
    public static class ExcelLayoutBuilder
    {
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

        public static List<string> GetActiveParamHeaders(Device device, Dictionary<string, bool[]>? paramEnabled)
        {
            var headers = new List<string>();
            for (int i = 0; i < device.headers.Length; i++)
            {
                bool paramOn = paramEnabled == null
                    || !paramEnabled.TryGetValue(device.ID, out var arr)
                    || (i < arr.Length && arr[i]);
                if (paramOn) headers.Add(device.headers[i]);
            }
            return headers;
        }

        // Step-CSV: «Шаг»/«Время» слева, затем блоки устройств (только параметры во 2-й строке).
        public static int BuildStepLogHeaders(
            IXLWorksheet ws,
            List<Device> devices,
            Dictionary<string, bool>? deviceEnabled,
            Dictionary<string, bool[]>? paramEnabled)
        {
            int col = 1;
            int colorIdx = 0;

            WriteFixedColumn(ws, col++, "Шаг");
            WriteFixedColumn(ws, col++, "Время");

            foreach (var device in devices)
            {
                bool devOn = deviceEnabled == null || deviceEnabled.GetValueOrDefault(device.ID, true);
                if (!devOn) continue;

                var activeParams = GetActiveParamHeaders(device, paramEnabled);
                if (activeParams.Count == 0) continue;

                XLColor bg = DeviceColors[colorIdx % DeviceColors.Length];
                colorIdx++;

                int devStartCol = col;
                WriteDeviceIdRow(ws, devStartCol, device.ID, activeParams.Count, bg);

                foreach (var header in activeParams)
                {
                    var cell = ws.Cell(2, col++);
                    cell.Value = header;
                    ApplyParamHeaderStyle(cell, bg);
                }
            }

            ws.SheetView.FreezeRows(2);
            return 3;
        }

        // Time-series: у каждого устройства «Время» + параметры; includeDevice отсекает пустые блоки.
        public static void BuildTimeSeriesHeaders(
            IXLWorksheet ws,
            List<Device> devices,
            Func<Device, bool> includeDevice,
            Dictionary<string, bool>? deviceEnabled,
            Dictionary<string, bool[]>? paramEnabled)
        {
            int col = 1;
            int colorIdx = 0;

            foreach (var device in devices)
            {
                bool devOn = deviceEnabled == null || deviceEnabled.GetValueOrDefault(device.ID, true);
                if (!devOn || !includeDevice(device)) continue;

                var activeParams = GetActiveParamHeaders(device, paramEnabled);
                if (activeParams.Count == 0) continue;

                XLColor bg = DeviceColors[colorIdx % DeviceColors.Length];
                colorIdx++;

                WriteDeviceIdRow(ws, col, device.ID, 1 + activeParams.Count, bg);

                ApplyParamHeaderStyle(ws.Cell(2, col), bg);
                ws.Cell(2, col).Value = "Время";
                col++;

                foreach (var header in activeParams)
                {
                    var cell = ws.Cell(2, col++);
                    cell.Value = header;
                    ApplyParamHeaderStyle(cell, bg);
                }
            }

            ws.SheetView.FreezeRows(2);
        }

        private static void WriteFixedColumn(IXLWorksheet ws, int col, string title)
        {
            StyleMergedHeader(ws, 1, col, FixedColumnGray);
            StyleMergedHeader(ws, 2, col, FixedColumnGray);
            ws.Cell(1, col).Value = title;
        }

        // Строка 1: ID устройства на blockCols колонок.
        private static void WriteDeviceIdRow(IXLWorksheet ws, int startCol, string deviceId, int blockCols, XLColor bg)
        {
            int devEndCol = startCol + blockCols - 1;

            if (startCol == devEndCol)
                ws.Cell(1, startCol).Value = deviceId;
            else
            {
                ws.Range(1, startCol, 1, devEndCol).Merge();
                ws.Cell(1, startCol).Value = deviceId;
            }

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
