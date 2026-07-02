using System.Text;

namespace logReader.UI
{
    [Flags]
    internal enum LogFormatKind
    {
        None = 0,
        Trc = 1 << 0,
        Asc = 1 << 1,
        MatrixCsv = 1 << 2,
        StepCsv = 1 << 3,
        CanfoxTxt = 1 << 4,

        All = Trc | Asc | MatrixCsv | StepCsv | CanfoxTxt,
    }

    internal static class LogFormatUiNames
    {
        public const string Csv = "CSV";
        public const string LegacyCsv = "legacy CSV";

        public static string GetLabel(LogFormatKind kind) => kind switch
        {
            LogFormatKind.Trc => ".trc",
            LogFormatKind.Asc => ".asc",
            LogFormatKind.MatrixCsv => Csv,
            LogFormatKind.StepCsv => LegacyCsv,
            LogFormatKind.CanfoxTxt => "CANfox .txt",
            _ => kind.ToString()
        };

        public static string GetDetectedFormatMessage(LogFormatKind kind) => kind switch
        {
            LogFormatKind.MatrixCsv => "Формат: CSV (20 мс)",
            LogFormatKind.StepCsv => "Формат: legacy CSV",
            _ => string.Empty
        };
    }

    internal readonly record struct LogFolderInventory(
        IReadOnlyDictionary<LogFormatKind, int> Counts,
        IReadOnlyDictionary<LogFormatKind, List<string>> FilesByKind);

    internal static class LogFormatDetector
    {
        public static LogFormatKind Detect(string path)
        {
            string ext = Path.GetExtension(path);
            if (ext.Equals(".trc", StringComparison.OrdinalIgnoreCase))
                return LogFormatKind.Trc;
            if (ext.Equals(".asc", StringComparison.OrdinalIgnoreCase))
                return LogFormatKind.Asc;

            if (ext.Equals(".csv", StringComparison.OrdinalIgnoreCase))
            {
                Encoding enc = LogFileEncoding.Detect(path);
                return MatrixCsvLogParser.LooksLikeMatrixCsv(path, enc)
                    ? LogFormatKind.MatrixCsv
                    : LogFormatKind.StepCsv;
            }

            if (ext.Equals(".txt", StringComparison.OrdinalIgnoreCase))
            {
                Encoding enc = LogFileEncoding.Detect(path);
                return CanfoxLogParser.LooksLikeCanfoxLog(path, enc)
                    ? LogFormatKind.CanfoxTxt
                    : LogFormatKind.None;
            }

            return LogFormatKind.None;
        }
    }

    internal static class LogFolderScanner
    {
        public static IEnumerable<string> EnumerateSupportedLogFiles(string folderPath)
        {
            string[] patterns = { "*.csv", "*.trc", "*.asc", "*.txt" };
            foreach (string pattern in patterns)
            {
                foreach (string path in Directory.EnumerateFiles(folderPath, pattern, SearchOption.TopDirectoryOnly))
                    yield return path;
            }
        }

        public static LogFolderInventory Scan(string folderPath)
        {
            var counts = new Dictionary<LogFormatKind, int>();
            var filesByKind = new Dictionary<LogFormatKind, List<string>>();

            foreach (string path in EnumerateSupportedLogFiles(folderPath))
            {
                LogFormatKind kind;
                try { kind = LogFormatDetector.Detect(path); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    continue;
                }

                if (kind == LogFormatKind.None)
                    continue;

                counts[kind] = counts.TryGetValue(kind, out int n) ? n + 1 : 1;

                if (!filesByKind.TryGetValue(kind, out var list))
                {
                    list = new List<string>();
                    filesByKind[kind] = list;
                }
                list.Add(path);
            }

            return new LogFolderInventory(counts, filesByKind);
        }
    }
}
