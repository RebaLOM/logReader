using System.Globalization;
using ClosedXML.Excel;

namespace logReader.Processing
{
    public enum TimeAxisKind
    {
        // Миллисекунды от начала записи (pCAN .trc).
        Milliseconds,
        // Время суток как доля суток (может превышать 1 после полуночи): ASC, CANfox.
        TimeOfDay,
        // Абсолютная дата и время как OADate (объединение логов с разным началом записи).
        DateTime
    }

    internal static class TimeAxisFormat
    {
        public static string Format(double value, TimeAxisKind kind) => kind switch
        {
            TimeAxisKind.TimeOfDay => FormatTimeOfDay(value),
            TimeAxisKind.DateTime => RoundToMillisecond(FromOADate(value)).ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture),
            _ => value.ToString(CultureInfo.InvariantCulture)
        };

        // CSV под Excel (ru-RU): мс с точкой читаются как дата; часы:минуты оставляем с «.».
        public static string FormatCsv(double value, TimeAxisKind kind) => kind switch
        {
            TimeAxisKind.Milliseconds => CsvNumberFormat.Format(value),
            _ => Format(value, kind)
        };

        public static string? ExcelNumberFormat(TimeAxisKind kind) => kind switch
        {
            TimeAxisKind.TimeOfDay => "[h]:mm:ss.000",
            TimeAxisKind.DateTime => "yyyy-mm-dd hh:mm:ss.000",
            _ => null
        };

        // Округление до тика: прямое TimeSpan.FromDays(доля) теряло 1 мс примерно у 5 % меток.
        public static string FormatTimeOfDay(double dayFraction)
        {
            var ts = TimeSpan.FromTicks((long)Math.Round(dayFraction * TimeSpan.TicksPerDay));
            ts = TimeSpan.FromMilliseconds(Math.Round(ts.TotalMilliseconds, MidpointRounding.AwayFromZero));
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0:00}:{1:00}:{2:00}.{3:000}",
                (int)Math.Floor(ts.TotalHours),
                ts.Minutes,
                ts.Seconds,
                ts.Milliseconds);
        }

        public static double ToOADate(DateTime value)
            => (value.Ticks - OADateEpochTicks) / (double)TimeSpan.TicksPerDay;

        private static readonly long OADateEpochTicks = new DateTime(1899, 12, 30).Ticks;

        // «fff» усекает: 10,0099999 мс без округления печатается как .009.
        private static DateTime RoundToMillisecond(DateTime value)
            => new(value.Ticks + TimeSpan.TicksPerMillisecond / 2 - (value.Ticks + TimeSpan.TicksPerMillisecond / 2) % TimeSpan.TicksPerMillisecond);

        public static DateTime FromOADate(double value)
            => new DateTime(OADateEpochTicks + (long)Math.Round(value * TimeSpan.TicksPerDay), DateTimeKind.Unspecified);
    }

    // Накопление time-series: на каждый кадр устройства — время и значения только активных параметров.
    internal sealed class TimeSeriesCollector
    {
        private readonly Dictionary<string, Series> _series = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<Device> _order;
        private readonly OutputFilter _filter;

        public TimeAxisKind TimeKind { get; }
        internal List<(double Time, DtcSnapshot Snapshot)>? DtcRows { get; }
        private DtcOutputTimeline? _dtcTimeline;
        private readonly double? _dtcTimeoutMs;

        public TimeSeriesCollector(IEnumerable<Device> outputDevices, OutputFilter filter, TimeAxisKind timeKind, double? dtcTimeoutMs = null)
        {
            _order = outputDevices.ToList();
            _filter = filter;
            TimeKind = timeKind;
            _dtcTimeoutMs = dtcTimeoutMs;
            if (dtcTimeoutMs.HasValue) DtcRows = new();
        }

        internal void BeginDtcFile()
            => _dtcTimeline = _dtcTimeoutMs.HasValue ? new DtcOutputTimeline(_dtcTimeoutMs.Value) : null;

        internal void ObserveDtc(string id, ReadOnlySpan<int> payload, double time)
        {
            if (DtcRows == null) return;
            _dtcTimeline ??= new DtcOutputTimeline(_dtcTimeoutMs!.Value);
            int previous = _dtcTimeline.Rows.Count;
            _dtcTimeline.Observe(id, payload, time, TimeKind);
            for (int i = previous; i < _dtcTimeline.Rows.Count; i++) DtcRows.Add(_dtcTimeline.Rows[i]);
            _dtcTimeline.Rows.Clear();
        }

        // Ошибка поля хранится как NaN с особым битовым шаблоном: обычный NaN означает «нет значения».
        internal static readonly double ErrorMarker = BitConverter.Int64BitsToDouble(0x7FF8_0000_0E44_0001);

        internal static bool IsError(double value)
            => BitConverter.DoubleToInt64Bits(value) == BitConverter.DoubleToInt64Bits(ErrorMarker);

        public bool HasData => _series.Values.Any(s => s.Times.Count > 0) || DtcRows?.Count > 0;

        public int MaxRows => Math.Max(_series.Count == 0 ? 0 : _series.Values.Max(s => s.Times.Count), DtcRows?.Count ?? 0);

        public void Record(Device device, double time)
        {
            if (!_series.TryGetValue(device.ID, out var series))
            {
                series = new Series(_filter.GetActiveParams(device));
                _series[device.ID] = series;
            }

            if (series.ParamIndexes.Length == 0) return;

            var values = new double[series.ParamIndexes.Length];
            for (int i = 0; i < values.Length; i++)
            {
                int idx = series.ParamIndexes[i];
                values[i] = device.FieldErrors[idx] ? ErrorMarker : device.Values[idx];
            }
            series.Times.Add(time);
            series.Rows.Add(values);
        }

        public IReadOnlyList<(OutputColumnGroup Group, Series Series)> Columns()
        {
            var result = new List<(OutputColumnGroup, Series)>();
            foreach (var device in _order)
            {
                if (_series.TryGetValue(device.ID, out var s) && s.Times.Count > 0 && s.ParamIndexes.Length > 0)
                    result.Add((new OutputColumnGroup(device, s.ParamIndexes), s));
            }
            return result;
        }

        internal sealed class Series
        {
            public Series(int[] paramIndexes) => ParamIndexes = paramIndexes;
            public int[] ParamIndexes { get; }
            public List<double> Times { get; } = new();
            public List<double[]> Rows { get; } = new();
        }
    }

    // Единый вывод time-series в Excel/CSV: у каждого устройства своя колонка «Время».
    internal static class TimeSeriesOutputWriter
    {
        public static ProcessingResult Write(
            TimeSeriesCollector data,
            string outputPath,
            OutputSettings settings,
            string sheetName,
            ProcessingContext context)
        {
            var columns = data.Columns();
            var dtcRows = settings.IncludeDtc || (settings.AutoIncludeDtc && data.DtcRows?.Any(r => DtcOutput.HasCodes(r.Snapshot)) == true)
                ? data.DtcRows : null;
            if (columns.Count == 0 && dtcRows is not { Count: > 0 })
                return context.Fail("Нет совпадающих устройств — проверьте файл посылок.");

            int maxRows = Math.Max(columns.Count == 0 ? 0 : columns.Max(c => c.Series.Times.Count), dtcRows?.Count ?? 0);
            try
            {
                if (settings.Format == OutputFormat.Xlsx)
                    WriteExcel(columns, data.TimeKind, outputPath, sheetName, settings.IncludeDeviceIdHeaderRow, context, dtcRows, settings.DtcDisplayFormat);
                else
                    WriteCsv(columns, data.TimeKind, outputPath, settings.IncludeDeviceIdHeaderRow, context, dtcRows, settings.DtcDisplayFormat);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return context.Fail($"Ошибка сохранения: {ex.Message}");
            }

            context.Log("Обработка завершена.");
            return ProcessingResult.Ok(outputPath, maxRows);
        }

        private static void WriteExcel(
            IReadOnlyList<(OutputColumnGroup Group, TimeSeriesCollector.Series Series)> columns,
            TimeAxisKind timeKind,
            string outputPath,
            string sheetName,
            bool includeDeviceIdRow,
            ProcessingContext context,
            List<(double Time, DtcSnapshot Snapshot)>? dtcRows,
            DtcDisplayFormat dtcDisplayFormat)
        {
            int headerRows = ExcelLayoutBuilder.HeaderRowCount(includeDeviceIdRow);
            int dataCapacity = Math.Max(1, ExcelLayoutBuilder.RowsPerSheet - headerRows);
            int maxRows = Math.Max(columns.Count == 0 ? 0 : columns.Max(c => c.Series.Times.Count), dtcRows?.Count ?? 0);
            int sheetCount = Math.Max(1, (maxRows + dataCapacity - 1) / dataCapacity);
            var groups = columns.Select(c => c.Group).ToList();
            string? timeFormat = TimeAxisFormat.ExcelNumberFormat(timeKind);

            using var workbook = new XLWorkbook();
            for (int part = 0; part < sheetCount; part++)
            {
                context.ThrowIfCancellationRequested();
                int dataStart = part * dataCapacity;
                int dataEnd = Math.Min(maxRows, dataStart + dataCapacity);

                var ws = workbook.Worksheets.Add(ExcelLayoutBuilder.SheetNameForPart(sheetName, part + 1));
                int firstDataRow = ExcelLayoutBuilder.BuildTimeSeriesHeaders(ws, groups, includeDeviceIdRow);
                if (dtcRows != null)
                    DtcOutput.WriteHeaders(ws, 1 + groups.Sum(g => 1 + g.ParamIndexes.Length), includeDeviceIdRow, true);

                int col = 1;
                foreach (var (group, series) in columns)
                {
                    int written = 0;
                    for (int r = dataStart; r < dataEnd && r < series.Times.Count; r++)
                    {
                        int excelRow = firstDataRow + (r - dataStart);
                        ws.Cell(excelRow, col).Value = series.Times[r];
                        double[] values = series.Rows[r];
                        for (int i = 0; i < values.Length; i++)
                            SetValue(ws.Cell(excelRow, col + 1 + i), values[i]);
                        written++;
                    }

                    if (timeFormat != null && written > 0)
                        ws.Range(firstDataRow, col, firstDataRow + written - 1, col).Style.NumberFormat.Format = timeFormat;

                    col += 1 + group.ParamIndexes.Length;
                }
                if (dtcRows != null)
                {
                    for (int r = dataStart; r < dataEnd && r < dtcRows.Count; r++)
                    {
                        int excelRow = firstDataRow + r - dataStart;
                        ws.Cell(excelRow, col).Value = dtcRows[r].Time;
                        DtcOutput.WriteFields(ws, excelRow, col + 1, dtcRows[r].Snapshot, dtcDisplayFormat);
                    }
                    if (timeFormat != null && dataEnd > dataStart && dataStart < dtcRows.Count)
                        ws.Range(firstDataRow, col, firstDataRow + Math.Min(dataEnd, dtcRows.Count) - dataStart - 1, col)
                            .Style.NumberFormat.Format = timeFormat;
                }
            }

            if (sheetCount > 1)
                context.Log($"XLSX: данные разбиты на {sheetCount} лист(а/ов) (лимит {ExcelLayoutBuilder.RowsPerSheet:N0} строк на лист).");

            SafeFileWriter.Write(outputPath, tmp =>
            {
                foreach (var ws in workbook.Worksheets)
                    ExcelLayoutBuilder.AutoFitColumns(ws);
                workbook.SaveAs(tmp);
            });
        }

        private static void WriteCsv(
            IReadOnlyList<(OutputColumnGroup Group, TimeSeriesCollector.Series Series)> columns,
            TimeAxisKind timeKind,
            string outputPath,
            bool includeDeviceIdRow,
            ProcessingContext context,
            List<(double Time, DtcSnapshot Snapshot)>? dtcRows,
            DtcDisplayFormat dtcDisplayFormat)
        {
            SafeFileWriter.Write(outputPath, tmp =>
            {
                using var writer = new StreamWriter(tmp, false, CsvOutput.Encoding);

                if (includeDeviceIdRow)
                {
                    var idRow = new List<string>();
                    foreach (var (group, _) in columns)
                    {
                        idRow.Add(CsvOutput.Text(group.Device.ID));
                        for (int i = 0; i < group.ParamIndexes.Length; i++)
                            idRow.Add("");
                    }
                    if (dtcRows != null) idRow.AddRange(new[] { "DTC" }.Concat(Enumerable.Repeat("", DtcOutput.Headers.Length)));
                    CsvOutput.WriteRow(writer, idRow);
                }

                var headerRow = new List<string>();
                foreach (var (group, _) in columns)
                {
                    headerRow.Add("Время");
                    headerRow.AddRange(group.Headers.Select(CsvOutput.Text));
                }
                if (dtcRows != null)
                {
                    headerRow.Add("Время DTC");
                    headerRow.AddRange(DtcOutput.Headers);
                }
                CsvOutput.WriteRow(writer, headerRow);

                int maxRows = Math.Max(columns.Count == 0 ? 0 : columns.Max(c => c.Series.Times.Count), dtcRows?.Count ?? 0);
                var row = new List<string>();
                for (int r = 0; r < maxRows; r++)
                {
                    if (r % 4096 == 0) context.ThrowIfCancellationRequested();
                    row.Clear();
                    foreach (var (group, series) in columns)
                    {
                        if (r < series.Times.Count)
                        {
                            row.Add(TimeAxisFormat.FormatCsv(series.Times[r], timeKind));
                            foreach (double v in series.Rows[r])
                                row.Add(FormatValue(v));
                        }
                        else
                        {
                            for (int i = 0; i <= group.ParamIndexes.Length; i++)
                                row.Add("");
                        }
                    }
                    if (dtcRows != null)
                    {
                        if (r < dtcRows.Count)
                        {
                            row.Add(TimeAxisFormat.FormatCsv(dtcRows[r].Time, timeKind));
                            row.AddRange(DtcOutput.Fields(dtcRows[r].Snapshot, dtcDisplayFormat));
                        }
                        else row.AddRange(Enumerable.Repeat("", 1 + DtcOutput.Headers.Length));
                    }
                    CsvOutput.WriteRow(writer, row);
                }
            });
        }

        // Числа — с запятой для Excel/Power BI; колонка «Время» идёт через TimeAxisFormat без замены.
        private static string FormatValue(double v)
            => TimeSeriesCollector.IsError(v) ? "ERR" : ValueFormatter.FormatCsv(v);

        private static void SetValue(IXLCell cell, double v)
        {
            if (TimeSeriesCollector.IsError(v)) cell.Value = "ERR";
            else if (!double.IsNaN(v)) cell.Value = v;
        }
    }
}
