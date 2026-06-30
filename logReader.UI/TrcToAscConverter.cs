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
            if (!hasStartTime)
                log("Предупреждение: не найдено стартовое время (Start time). Заголовок ASC — с сегодняшней датой.");

            try
            {
                using var writer = new StreamWriter(ascPath, false, new UTF8Encoding(false));
                VectorCanFdAscWriter.WriteHeader(writer, headerTime);

                int truncatedFdFrames = 0;
                int frameCount = 0;
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
                            out _))
                    {
                        continue;
                    }

                    if (dlc > MaxClassicBytes) truncatedFdFrames++;
                    int outByteCount = Math.Min(dlc, MaxClassicBytes);

                    double offsetSec = (double)(timeMs / 1000m);
                    VectorCanFdAscWriter.WriteFrame(writer, offsetSec, dir, idRaw, bytesBuffer, outByteCount);
                    frameCount++;
                }

                if (truncatedFdFrames > 0)
                    log($"Предупреждение: {truncatedFdFrames} кадр(ов) с DLC>8 усечены до 8 байт.");

                log($"Конвертация завершена. Записано кадров: {frameCount:N0}.");
            }
            catch (Exception ex)
            {
                log($"Ошибка записи файла: {ex.Message}");
                return;
            }
        }

        private const int MaxClassicBytes = 8;
    }
}
