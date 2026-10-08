using System.Globalization;

namespace logReader.Processing
{
    internal static class DstConnectCsvWriter
    {
        internal sealed record Column(int Index, Device Device, int ParamIndex, string Header);

        // Колонки по плану фильтра; повтор имени параметра у разных устройств — с префиксом ID.
        internal static List<Column> BuildColumns(IEnumerable<Device> devices, OutputFilter filter)
        {
            var columns = new List<Column>();
            var headerCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var group in filter.BuildColumns(devices))
            {
                foreach (int i in group.ParamIndexes)
                {
                    string paramName = group.Device.Headers[i];
                    string header = paramName;
                    if (!headerCounts.TryAdd(paramName, 1))
                    {
                        headerCounts[paramName]++;
                        header = group.Device.ID + " " + paramName;
                    }

                    columns.Add(new Column(columns.Count, group.Device, i, header));
                }
            }

            return columns;
        }

        internal static List<Column> FilterColumnsWithData(IReadOnlyList<Column> columns, IReadOnlyList<DstConnectSnapshotRow> rows)
        {
            if (columns.Count == 0 || rows.Count == 0)
                return [];

            var used = new bool[columns.Count];
            foreach (var row in rows)
                for (int i = 0; i < used.Length; i++)
                    if (!double.IsNaN(row.Values[i]) || TimeSeriesCollector.IsError(row.Values[i]))
                        used[i] = true;

            return columns.Where(c => used[c.Index]).ToList();
        }

        internal static ProcessingResult Write(
            string outputPath,
            DateTime? startTime,
            IReadOnlyList<Column> columns,
            IReadOnlyList<DstConnectSnapshotRow> rows,
            bool includeDeviceIdHeaderRow,
            ProcessingContext context)
        {
            try
            {
                SafeFileWriter.Write(outputPath, tmp =>
                {
                    using var writer = new StreamWriter(tmp, false, CsvOutput.Encoding);

                    // Time/Step всегда в строке с именами параметров; строка ID — только CAN ID.
                    if (includeDeviceIdHeaderRow)
                    {
                        var idRow = new List<string> { "", "" };
                        Device? lastDevice = null;
                        foreach (var col in columns)
                        {
                            idRow.Add(ReferenceEquals(col.Device, lastDevice) ? "" : CsvOutput.Text(col.Device.ID));
                            lastDevice = col.Device;
                        }
                        CsvOutput.WriteRow(writer, idRow);
                    }

                    var headerRow = new List<string> { "Time", "Step" };
                    headerRow.AddRange(columns.Select(c => CsvOutput.Text(c.Header)));
                    CsvOutput.WriteRow(writer, headerRow);

                    var line = new List<string>(columns.Count + 2);
                    foreach (var row in rows.OrderBy(r => r.StepMs))
                    {
                        line.Clear();
                        // Time — часы:минуты:секунды без замены точки; Step/значения — ru-RU.
                        line.Add(FormatClockTime(startTime, row.StepMs));
                        line.Add(FormatStep(row.StepMs));
                        foreach (var col in columns)
                            line.Add(FormatValue(row.Values[col.Index]));
                        CsvOutput.WriteRow(writer, line);
                    }
                });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return context.Fail($"Ошибка сохранения: {ex.Message}");
            }

            context.Log("Обработка завершена.");
            return ProcessingResult.Ok(outputPath, rows.Count);
        }

        private static string FormatClockTime(DateTime? startTime, double stepMs)
        {
            if (!startTime.HasValue)
                return "";

            var t = startTime.Value.AddMilliseconds(stepMs);
            return t.ToString("H:mm:ss", CultureInfo.InvariantCulture);
        }

        private static string FormatStep(double stepMs)
        {
            if (Math.Abs(stepMs - Math.Round(stepMs)) < 0.001)
                return CsvNumberFormat.Format(Math.Round(stepMs));
            return CsvNumberFormat.Format(stepMs);
        }

        private static string FormatValue(double value)
        {
            if (TimeSeriesCollector.IsError(value)) return "ERR";
            return CsvNumberFormat.Format(value);
        }
    }
}
