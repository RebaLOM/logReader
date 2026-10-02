namespace logReader.Processing
{
    internal sealed class TrcToAscConverter
    {
        private const int MaxClassicBytes = 8;

        public ProcessingResult Convert(string trcPath, string ascPath, ProcessingContext context)
        {
            if (!File.Exists(trcPath))
                return context.Fail($"Ошибка: файл не найден: {trcPath}");

            try
            {
                var encoding = LogFileEncoding.Detect(trcPath);
                DateTime? startTime = TrcLogParser.ParseStartTime(trcPath, encoding);
                DateTime headerTime = startTime ?? DateTime.Today;
                if (!startTime.HasValue)
                    context.Log("Предупреждение: не найдено стартовое время (Start time). Заголовок ASC — с сегодняшней датой.");

                int truncatedFdFrames = 0;
                int frameCount = 0;
                SafeFileWriter.Write(ascPath, tmp =>
                {
                    using var writer = new StreamWriter(tmp, false, new System.Text.UTF8Encoding(false));
                    VectorCanFdAscWriter.WriteHeader(writer, headerTime);

                    Span<int> bytesBuffer = stackalloc int[8];
                    foreach (var line in LogFileReader.ReadLines(trcPath, encoding, context))
                    {
                        if (!TrcLogParser.TryParseTrcFrameLine(line, out decimal timeMs, out string dir, out string idRaw,
                                out int dlc, bytesBuffer, out int parsedByteCount))
                            continue;

                        if (dlc > MaxClassicBytes) truncatedFdFrames++;
                        int outByteCount = Math.Min(parsedByteCount, MaxClassicBytes);

                        double offsetSec = (double)(timeMs / 1000m);
                        VectorCanFdAscWriter.WriteFrame(writer, offsetSec, dir, idRaw, bytesBuffer, outByteCount);
                        frameCount++;
                    }
                });

                if (truncatedFdFrames > 0)
                    context.Log($"Предупреждение: {truncatedFdFrames} кадр(ов) с DLC>8 усечены до 8 байт.");

                context.Log($"Конвертация завершена. Записано кадров: {frameCount:N0}.");
                return ProcessingResult.Ok(ascPath, frameCount);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return context.Fail($"Ошибка конвертации: {ex.Message}");
            }
        }
    }
}
