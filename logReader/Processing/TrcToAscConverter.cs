namespace logReader.Processing
{
    internal sealed class TrcToAscConverter
    {
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

                int frameCount = 0;
                SafeFileWriter.Write(ascPath, tmp =>
                {
                    using var writer = new StreamWriter(tmp, false, new System.Text.UTF8Encoding(false));
                    VectorCanFdAscWriter.WriteHeader(writer, headerTime);

                    Span<int> bytesBuffer = stackalloc int[Device.MaxDataLength];
                    foreach (var line in LogFileReader.ReadLines(trcPath, encoding, context))
                    {
                        if (!TrcLogParser.TryParseTrcFrameLine(line, out decimal timeMs, out string dir, out string idRaw,
                                out _, bytesBuffer, out int parsedByteCount))
                            continue;

                        double offsetSec = (double)(timeMs / 1000m);
                        VectorCanFdAscWriter.WriteFrame(writer, offsetSec, dir, idRaw, bytesBuffer, parsedByteCount);
                        frameCount++;
                    }
                });

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
