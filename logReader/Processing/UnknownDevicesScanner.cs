using System.Text;

namespace logReader.Processing
{
    internal sealed class LogDeviceScanResult
    {
        public List<string> MissingInDevices { get; init; } = new();
        public List<string> MatchedInDevices { get; init; } = new();
    }

    // Сверка ID из лога (или папки логов) с файлом посылок.
    internal static class UnknownDevicesScanner
    {
        public static LogDeviceScanResult ScanLogDevices(
            string logPath,
            IEnumerable<Device> knownDevices,
            Action<string>? log = null,
            CancellationToken cancellationToken = default)
        {
            var knownIds = new HashSet<string>(knownDevices.Select(d => d.ID), StringComparer.OrdinalIgnoreCase);
            var logIds = CollectLogIds(logPath, log, cancellationToken).Keys;

            var missing = logIds.Where(id => !knownIds.Contains(id)).OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToList();
            var matched = logIds.Where(knownIds.Contains).OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToList();
            return new LogDeviceScanResult { MissingInDevices = missing, MatchedInDevices = matched };
        }

        // ID → число кадров; для табличного CSV — число непустых ячеек колонки.
        public static Dictionary<string, int> CollectLogIds(
            string logPath,
            Action<string>? log = null,
            CancellationToken cancellationToken = default)
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(logPath))
                return counts;

            IEnumerable<string> files = Directory.Exists(logPath)
                ? LogFolderScanner.EnumerateSupportedLogFiles(logPath).OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                : File.Exists(logPath) ? new[] { logPath } : Array.Empty<string>();

            var context = new ProcessingContext(cancellationToken: cancellationToken);
            foreach (var file in files)
                ScanFile(file, counts, log, context);
            return counts;
        }

        private static void ScanFile(string filePath, Dictionary<string, int> counts, Action<string>? log, ProcessingContext context)
        {
            try
            {
                var kind = LogFormatDetector.Detect(filePath);
                if (kind == LogFormatKind.None) return;
                Encoding enc = LogFileEncoding.Detect(filePath);

                if (kind == LogFormatKind.MatrixCsv)
                {
                    ScanMatrixCsv(filePath, enc, counts, context);
                    return;
                }

                Span<int> bytes = stackalloc int[Device.MaxDataLength];
                foreach (var line in LogFileReader.ReadLines(filePath, enc, context))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    bool ok;
                    string id;
                    switch (kind)
                    {
                        case LogFormatKind.Asc:
                            ok = AscLogParser.TryParseFrameId(line, out id);
                            break;
                        case LogFormatKind.Trc:
                            ok = TrcLogParser.TryParseTrcFrameLine(line, out _, out _, out id, out _, bytes, out _);
                            break;
                        case LogFormatKind.CanfoxTxt:
                            ok = CanfoxLogParser.TryParseCanfoxFrameLine(line, out _, out id, bytes, out _);
                            break;
                        default:
                            ok = StepCsvLogParser.TryParseAcceptedId(line, out id);
                            break;
                    }

                    if (ok && id.Length > 0)
                        counts[id] = counts.TryGetValue(id, out int n) ? n + 1 : 1;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DecoderFallbackException)
            {
                log?.Invoke($"Не удалось просканировать '{Path.GetFileName(filePath)}': {ex.Message}");
            }
        }

        private static void ScanMatrixCsv(string filePath, Encoding enc, Dictionary<string, int> counts, ProcessingContext context)
        {
            List<MatrixCsvColumn>? columns = null;
            Span<int> msgBytes = stackalloc int[8];

            foreach (string line in LogFileReader.ReadLines(filePath, enc, context))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                if (columns == null)
                {
                    if (!MatrixCsvLogParser.TryReadHeader(line, out columns, out _)) return;
                    foreach (var col in columns)
                        counts.TryAdd(col.Id, 0);
                    continue;
                }

                string[] parts = line.Split(';');
                if (parts.Length < 2 || !MatrixCsvLogParser.TryParseTimeCell(parts[0], out _)) continue;

                foreach (var col in columns)
                {
                    if (col.ColumnIndex >= parts.Length) continue;
                    string cell = parts[col.ColumnIndex];
                    if (MatrixCsvLogParser.IsCellEmpty(cell) || !MatrixCsvLogParser.TryParsePayloadHex(cell, msgBytes)) continue;
                    counts[col.Id]++;
                }
            }
        }
    }
}
