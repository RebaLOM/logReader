using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace logReader.UI
{
    internal class TrcToAscConverter
    {
        private static readonly Regex TrcLineRegex = new Regex(
            @"^\s*\d+\)\s+([\d.,]+)\s+(\w+)\s+([0-9A-Fa-f]+)\s+(\d+)\s+((?:[0-9A-Fa-f]{2}\s*)+)",
            RegexOptions.Compiled);

        private static readonly string[] TailZeros = new[] { "0", "0", "0", "0", "0", "0", "0", "0" };

        public void Convert(string trcPath, string ascPath, Action<string> log)
        {
            if (!File.Exists(trcPath))
            {
                log($"Ошибка: файл не найден: {trcPath}");
                return;
            }

            Encoding encoding;
            try
            {
                encoding = LogFileEncoding.Detect(trcPath);
            }
            catch (Exception ex)
            {
                log($"Ошибка определения кодировки: {ex.Message}");
                return;
            }

            DateTime? startTime;
            try
            {
                startTime = ParseStartTime(File.ReadLines(trcPath, encoding));
            }
            catch (Exception ex)
            {
                log($"Ошибка чтения файла: {ex.Message}");
                return;
            }

            bool hasStartTime = startTime.HasValue;
            DateTime headerTime = hasStartTime ? startTime!.Value : DateTime.Today;
            string timestampsMode = hasStartTime ? "absolute" : "relative";
            if (!hasStartTime)
                log("Предупреждение: не найдено стартовое время (Start time). Время будет относительным.");

            try
            {
                using var writer = new StreamWriter(ascPath, false, new UTF8Encoding(false));
                writer.WriteLine($"date {headerTime.ToString("ddd MMM dd HH:mm:ss.fff yyyy", CultureInfo.InvariantCulture)}");
                writer.WriteLine($"base hex  timestamps {timestampsMode}");
                foreach (var line in File.ReadLines(trcPath, encoding))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    if (line.TrimStart().StartsWith(';')) continue;

                    var match = TrcLineRegex.Match(line);
                    if (!match.Success) continue;

                    if (!TryParseDecimal(match.Groups[1].Value, out decimal timeMs))
                        continue;

                    string dir = match.Groups[2].Value;
                    if (!dir.Equals("Rx", StringComparison.OrdinalIgnoreCase)
                        && !dir.Equals("Tx", StringComparison.OrdinalIgnoreCase))
                        dir = "Rx";
                    else
                        dir = dir.Equals("Tx", StringComparison.OrdinalIgnoreCase) ? "Tx" : "Rx";

                    string idRaw = match.Groups[3].Value.ToUpperInvariant();
                    if (!int.TryParse(idRaw, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int idValue))
                        continue;
                    bool isExtended = idValue > 0x7FF;
                    string idOut = idRaw + (isExtended ? "x" : "");

                    if (!int.TryParse(match.Groups[4].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int dlc))
                        dlc = 0;

                    string[] bytes = ParseBytes(match.Groups[5].Value);
                    decimal seconds = timeMs / 1000m;
                    string timeSec = seconds.ToString("0.000000", CultureInfo.InvariantCulture);
                    string ascLine = BuildAscLine(
                        timeSec,
                        dir,
                        idOut,
                        dlc,
                        bytes);
                    writer.WriteLine(ascLine);
                }
            }
            catch (Exception ex)
            {
                log($"Ошибка записи файла: {ex.Message}");
                return;
            }

            log("Конвертация завершена.");
        }

        private static bool TryParseDecimal(string raw, out decimal value)
        {
            value = 0m;
            if (string.IsNullOrWhiteSpace(raw)) return false;

            if (decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return true;
            if (decimal.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
                return true;

            return false;
        }

        private static string[] ParseBytes(string raw)
        {
            var tokens = raw.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            var bytes = new string[8];

            for (int i = 0; i < 8; i++)
            {
                if (i < tokens.Length && TryParseHexByte(tokens[i], out int v))
                    bytes[i] = v.ToString("X2", CultureInfo.InvariantCulture);
                else
                    bytes[i] = "00";
            }

            return bytes;
        }

        private static bool TryParseHexByte(string hex, out int value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(hex)) return false;
            if (hex.Length > 2) return false;

            return int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value)
                   && value >= 0 && value <= byte.MaxValue;
        }

        private static DateTime? ParseStartTime(IEnumerable<string> lines)
        {
            DateTime? fallback = null;

            foreach (var line in lines)
            {
                if (!line.StartsWith(";")) continue;

                int idx = line.IndexOf("Start time:", StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    string raw = line.Substring(idx + 11).Trim();

                    int lastDot = raw.LastIndexOf('.');
                    int prevDot = lastDot > 0 ? raw.LastIndexOf('.', lastDot - 1) : -1;
                    if (lastDot > 0 && prevDot > 0 && lastDot - prevDot <= 5)
                        raw = raw.Remove(lastDot, 1);

                    if (DateTime.TryParseExact(raw, "dd.MM.yyyy HH:mm:ss.ffff",
                            CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt4))
                        return dt4;
                    if (DateTime.TryParseExact(raw, "dd.MM.yyyy HH:mm:ss.fff",
                            CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt3))
                        return dt3;
                }

                if (line.StartsWith(";$STARTTIME=") && fallback == null)
                {
                    var val = line.Substring(12).Trim();
                    if (double.TryParse(val, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out double oaDate))
                    {
                        try { fallback = DateTime.FromOADate(oaDate); } catch { }
                    }
                }
            }

            return fallback;
        }

        private static string BuildAscLine(
            string timeSec,
            string dir,
            string idOut,
            int dlc,
            string[] bytes)
        {
            const int lineLen = 170;
            char[] buf = Enumerable.Repeat(' ', lineLen).ToArray();

            WriteToken(buf, 3, timeSec);
            WriteToken(buf, 12, "CANFD");
            WriteToken(buf, 20, "1");
            WriteToken(buf, 22, dir);
            WriteToken(buf, 27, idOut);

            WriteToken(buf, 70, "0");
            WriteToken(buf, 72, "0");

            string dlcStr = dlc.ToString(CultureInfo.InvariantCulture);
            WriteToken(buf, 74, dlcStr);
            WriteToken(buf, 77, dlcStr);

            WriteToken(buf, 79, bytes[0]);
            WriteToken(buf, 82, bytes[1]);
            WriteToken(buf, 85, bytes[2]);
            WriteToken(buf, 88, bytes[3]);
            WriteToken(buf, 91, bytes[4]);
            WriteToken(buf, 94, bytes[5]);
            WriteToken(buf, 97, bytes[6]);
            WriteToken(buf, 100, bytes[7]);

            WriteToken(buf, 110, "0");
            WriteToken(buf, 115, "0");
            WriteToken(buf, 119, "200000");

            WriteToken(buf, 133, "0");
            WriteToken(buf, 142, "0");
            WriteToken(buf, 151, "0");
            WriteToken(buf, 160, "0");
            WriteToken(buf, 169, "0");

            return new string(buf);
        }

        private static void WriteToken(char[] buffer, int startIndex, string token)
        {
            int idx = startIndex;
            for (int i = 0; i < token.Length && idx < buffer.Length; i++, idx++)
                buffer[idx] = token[i];
        }
    }
}
