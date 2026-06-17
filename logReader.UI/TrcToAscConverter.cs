using System.Globalization;
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

                int truncatedFdFrames = 0;
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

                    // Конвейер классический CAN (до 8 байт). Кадр с DLC>8 не можем
                    // представить честно — усекаем до 8 и предупреждаем (не молча).
                    if (dlc > MaxClassicBytes) truncatedFdFrames++;
                    int outByteCount = Math.Min(dlc, MaxClassicBytes);

                    decimal seconds = timeMs / 1000m;
                    string timeSec = seconds.ToString("0.000000", CultureInfo.InvariantCulture);
                    string ascLine = BuildClassicCanLine(timeSec, dir, idOut, outByteCount, bytesBuffer);
                    writer.WriteLine(ascLine);
                }

                if (truncatedFdFrames > 0)
                    log($"Предупреждение: {truncatedFdFrames} кадр(ов) с DLC>8 усечены до 8 байт (классический CAN).");
            }
            catch (Exception ex)
            {
                log($"Ошибка записи файла: {ex.Message}");
                return;
            }

            log("Конвертация завершена.");
        }

        private const int MaxClassicBytes = 8;
        private const string Channel = "1";

        // Формат Vector ASC classic CAN — тот же, что читает AscLogParser.
        private static string BuildClassicCanLine(string timeSec, string dir, string idOut, int byteCount, ReadOnlySpan<int> bytes)
        {
            var sb = new StringBuilder();
            sb.Append(timeSec).Append(' ')
              .Append(Channel).Append(' ')
              .Append(idOut).Append(' ')
              .Append(dir).Append(" d ")
              .Append(byteCount.ToString(CultureInfo.InvariantCulture));

            for (int i = 0; i < byteCount; i++)
                sb.Append(' ').Append(bytes[i].ToString("X2", CultureInfo.InvariantCulture));

            return sb.ToString();
        }
    }
}
