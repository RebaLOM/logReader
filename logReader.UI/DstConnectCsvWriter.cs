using System.Globalization;
using System.Text;

namespace logReader.UI
{
    internal static class DstConnectCsvWriter
    {
        internal sealed record Column(string Key, string DeviceId, string Header);

        internal static List<Column> BuildColumns(
            List<Device> devices,
            Dictionary<string, bool>? deviceEnabled,
            Dictionary<string, bool[]>? paramEnabled)
        {
            var columns = new List<Column>();
            var headerCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var device in devices)
            {
                bool devOn = deviceEnabled == null || deviceEnabled.GetValueOrDefault(device.ID, true);
                if (!devOn) continue;

                for (int i = 0; i < device.headers.Length; i++)
                {
                    bool paramOn = paramEnabled == null
                        || !paramEnabled.TryGetValue(device.ID, out var arr)
                        || (i < arr.Length && arr[i]);
                    if (!paramOn) continue;

                    string paramName = device.headers[i];
                    string header = paramName;
                    if (!headerCounts.TryAdd(paramName, 1))
                    {
                        headerCounts[paramName]++;
                        header = device.ID + " " + paramName;
                    }

                    string key = device.ID + "|" + i;
                    columns.Add(new Column(key, device.ID, header));
                }
            }

            return columns;
        }

        internal static List<Column> FilterColumnsWithData(
            IReadOnlyList<Column> columns,
            IReadOnlyList<DstConnectSnapshotRow> rows)
        {
            if (columns.Count == 0 || rows.Count == 0)
                return [];

            var usedKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                foreach (var key in row.Values.Keys)
                    usedKeys.Add(key);
            }

            return columns.Where(c => usedKeys.Contains(c.Key)).ToList();
        }

        internal static void Write(
            string outputPath,
            DateTime? startTime,
            IReadOnlyList<Column> columns,
            IReadOnlyList<DstConnectSnapshotRow> rows,
            Action<string> log)
        {
            string? tempPath = null;
            try
            {
                tempPath = SafeFileWriter.CreateTempPath(outputPath);
                var ru = CultureInfo.GetCultureInfo("ru-RU");
                using (var writer = new StreamWriter(tempPath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)))
                {
                    var row1 = new List<string> { "Time", "Step" };
                    var row2 = new List<string> { "", "" };
                    string? lastDeviceId = null;
                    foreach (var col in columns)
                    {
                        if (!col.DeviceId.Equals(lastDeviceId, StringComparison.OrdinalIgnoreCase))
                        {
                            row1.Add(col.DeviceId);
                            lastDeviceId = col.DeviceId;
                        }
                        else
                        {
                            row1.Add("");
                        }
                        row2.Add(col.Header);
                    }

                    CsvOutput.WriteRow(writer, row1);
                    CsvOutput.WriteRow(writer, row2);

                    foreach (var row in rows.OrderBy(r => r.StepMs))
                    {
                        var line = new List<string>
                        {
                            FormatClockTime(startTime, row.StepMs),
                            FormatStep(row.StepMs, ru)
                        };

                        foreach (var col in columns)
                        {
                            row.Values.TryGetValue(col.Key, out string? val);
                            line.Add(FormatValue(val, ru));
                        }

                        CsvOutput.WriteRow(writer, line);
                    }
                }

                SafeFileWriter.Publish(tempPath, outputPath);
                log("Обработка завершена.");
            }
            catch (Exception ex)
            {
                log($"Ошибка сохранения: {ex.Message}");
            }
            finally
            {
                if (tempPath != null && File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
                }
            }
        }

        private static string FormatClockTime(DateTime? startTime, double stepMs)
        {
            if (!startTime.HasValue)
                return "";

            var t = startTime.Value.AddMilliseconds(stepMs);
            return t.ToString("H:mm:ss", CultureInfo.InvariantCulture);
        }

        private static string FormatStep(double stepMs, CultureInfo ru)
        {
            if (Math.Abs(stepMs - Math.Round(stepMs)) < 0.001)
                return ((long)Math.Round(stepMs)).ToString(ru);
            return stepMs.ToString(ru);
        }

        private static string FormatValue(string? value, CultureInfo ru)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                return d.ToString(ru);

            return value;
        }
    }
}
