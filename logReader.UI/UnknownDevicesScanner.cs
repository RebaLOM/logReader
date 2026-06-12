using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace logReader.UI
{
    internal static class UnknownDevicesScanner
    {
        public static List<string> ScanForUnknownDevices(string logPath, List<Device> knownDevices)
        {
            var knownIds = new HashSet<string>(knownDevices.Select(d => d.ID), StringComparer.OrdinalIgnoreCase);
            var unknownIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(logPath))
                return new List<string>();

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
            {
                ScanFile(file, knownIds, unknownIds);
            }

            var result = unknownIds.ToList();
            result.Sort();
            return result;
        }

        private static void ScanFile(string filePath, HashSet<string> knownIds, HashSet<string> unknownIds)
        {
            try
            {
                string ext = Path.GetExtension(filePath).ToLowerInvariant();
                Encoding enc = LogFileEncoding.Detect(filePath);

                bool isCanfox = ext == ".txt" && CanfoxLogParser.LooksLikeCanfoxLog(filePath, enc);
                bool isTrc = ext == ".trc";
                bool isPCan = isTrc || isCanfox;
                bool isAsc = ext == ".asc";

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
                    else // csv
                    {
                        var parts = line.Split(';', 5);
                        if (parts.Length >= 4 && int.TryParse(parts[3], out int pri) && pri == 1)
                        {
                            id = parts[2].Trim();
                        }
                    }

                    if (!string.IsNullOrEmpty(id) && !knownIds.Contains(id))
                    {
                        unknownIds.Add(id);
                    }
                }
            }
            catch
            {
                // Игнорируем ошибки при сканировании
            }
        }
    }
}
