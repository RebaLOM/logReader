using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using logReader;

namespace logReader.UI
{
    internal sealed class LogDeviceScanResult
    {
        public List<string> MissingInDevices { get; init; } = new();
        public List<string> MatchedInDevices { get; init; } = new();
    }

    internal static class UnknownDevicesScanner
    {
        public static LogDeviceScanResult ScanLogDevices(string logPath, List<Device> knownDevices, Action<string>? log = null)
        {
            var knownIds = new HashSet<string>(knownDevices.Select(d => d.ID), StringComparer.OrdinalIgnoreCase);
            var logIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(logPath))
                return new LogDeviceScanResult();

            var filesToProcess = new List<string>();
            if (Directory.Exists(logPath))
            {
                string[] patterns = { "*.csv", "*.trc", "*.asc", "*.txt" };
                foreach (string pattern in patterns)
                {
                    filesToProcess.AddRange(Directory.EnumerateFiles(logPath, pattern, SearchOption.TopDirectoryOnly));
                }
            }
            else if (File.Exists(logPath))
            {
                filesToProcess.Add(logPath);
            }

            foreach (var file in filesToProcess)
                ScanFile(file, logIds, log);

            var missing = new List<string>();
            var matched = new List<string>();
            foreach (string id in logIds)
            {
                if (knownIds.Contains(id))
                    matched.Add(id);
                else
                    missing.Add(id);
            }

            missing.Sort(StringComparer.OrdinalIgnoreCase);
            matched.Sort(StringComparer.OrdinalIgnoreCase);

            return new LogDeviceScanResult
            {
                MissingInDevices = missing,
                MatchedInDevices = matched,
            };
        }

        public static List<string> ScanForUnknownDevices(string logPath, List<Device> knownDevices, Action<string>? log = null)
            => ScanLogDevices(logPath, knownDevices, log).MissingInDevices;

        private static void ScanFile(string filePath, HashSet<string> logIds, Action<string>? log = null)
        {
            try
            {
                string ext = Path.GetExtension(filePath).ToLowerInvariant();
                Encoding enc = LogFileEncoding.Detect(filePath);

                bool isCanfox = ext == ".txt" && CanfoxLogParser.LooksLikeCanfoxLog(filePath, enc);
                bool isTrc = ext == ".trc";
                bool isAsc = ext == ".asc";
                bool isMatrixCsv = ext == ".csv" && MatrixCsvLogParser.LooksLikeMatrixCsv(filePath, enc);

                if (isMatrixCsv)
                {
                    foreach (string line in File.ReadLines(filePath, enc))
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        if (MatrixCsvLogParser.TryReadHeader(line, out _, out List<string> ids))
                        {
                            foreach (string id in ids)
                                logIds.Add(id);
                        }

                        break;
                    }

                    return;
                }

                Span<int> bytes = stackalloc int[8];

                foreach (var line in File.ReadLines(filePath, enc))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    string? id = null;

                    if (isAsc)
                    {
                        if (AscLogParser.TryParseFrameLine(line, out _, out string parsedId, bytes, out _))
                            id = parsedId;
                    }
                    else if (isTrc)
                    {
                        if (TrcLogParser.TryParseTrcFrameLine(line, out _, out _, out string parsedId, out _, bytes, out _))
                            id = parsedId;
                    }
                    else if (isCanfox)
                    {
                        if (CanfoxLogParser.TryParseCanfoxFrameLine(line, out _, out string parsedId, bytes, out _))
                            id = parsedId;
                    }
                    else // step-csv
                    {
                        var parts = line.Split(';', 5);
                        if (parts.Length >= 4 && int.TryParse(parts[3], out int pri) && pri == 1)
                            id = parts[2].Trim();
                    }

                    if (!string.IsNullOrEmpty(id))
                        logIds.Add(id);
                }
            }
            catch (Exception ex) when (ex is IOException
                                       or UnauthorizedAccessException
                                       or System.Security.SecurityException
                                       or DecoderFallbackException)
            {
                log?.Invoke($"Не удалось просканировать '{Path.GetFileName(filePath)}': {ex.Message}");
            }
        }
    }
}
