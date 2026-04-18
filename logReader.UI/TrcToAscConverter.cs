using System.Globalization;
using System.Linq;
using System.Text;

namespace logReader.UI
{
    internal class TrcToAscConverter
    {
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
                startTime = TrcLogParser.ParseStartTime(File.ReadLines(trcPath, encoding));
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

                Span<int> bytesBuffer = stackalloc int[8];
                foreach (var line in File.ReadLines(trcPath, encoding))
                {
                    if (!TrcLogParser.TryParseTrcFrameLine(
                            line,
                            out decimal timeMs,
                            out string dir,
                            out string idRaw,
                            out int dlc,
                            bytesBuffer,
                            out int parsedByteCount))
                    {
                        continue;
                    }

                    if (!ulong.TryParse(idRaw, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong idValue))
                        continue;
                    bool isExtended = idValue > 0x7FFUL;
                    string idOut = idRaw + (isExtended ? "x" : "");

                    string[] bytes = new string[8];
                    for (int i = 0; i < 8; i++)
                    {
                        if (i < parsedByteCount)
                            bytes[i] = bytesBuffer[i].ToString("X2", CultureInfo.InvariantCulture);
                        else
                            bytes[i] = "00";
                    }

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
