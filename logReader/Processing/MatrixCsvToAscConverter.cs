namespace logReader.Processing
{
    internal sealed class MatrixCsvToAscConverter
    {
        public ProcessingResult Convert(string csvPath, string ascPath, ProcessingContext context)
        {
            if (!File.Exists(csvPath))
                return context.Fail($"Ошибка: файл не найден: {csvPath}");

            try
            {
                var encoding = LogFileEncoding.Detect(csvPath);
                if (!MatrixCsvLogParser.LooksLikeMatrixCsv(csvPath, encoding))
                    return context.Fail($"Ошибка: для конвертации нужен {LogFormatUiNames.Csv}, не {LogFormatUiNames.LegacyCsv}.");

                // В табличном CSV нет даты — берём дату изменения файла.
                DateTime fileDate = File.GetLastWriteTime(csvPath).Date;
                int frameCount = 0;
                int skippedCells = 0;
                bool hasData = false;

                SafeFileWriter.Write(ascPath, tmp =>
                {
                    using var writer = new StreamWriter(tmp, false, new System.Text.UTF8Encoding(false));
                    List<MatrixCsvColumn> columns = new();
                    bool headerRead = false;
                    var timeTracker = new MatrixCsvTimeTracker();
                    TimeSpan? baseTimeSpan = null;
                    Span<int> msgBytes = stackalloc int[8];

                    foreach (string line in LogFileReader.ReadLines(csvPath, encoding, context))
                    {
                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        if (!headerRead)
                        {
                            // Исключение отменяет публикацию: прежний файл назначения остаётся нетронутым.
                            if (!MatrixCsvLogParser.TryReadHeader(line, out columns, out _))
                                throw new InvalidDataException("Ошибка: не удалось прочитать заголовок CSV.");
                            headerRead = true;
                            continue;
                        }

                        string[] parts = line.Split(';');
                        if (parts.Length < 2 || !timeTracker.TryAdvance(parts[0], out TimeSpan absoluteTime))
                            continue;

                        if (!baseTimeSpan.HasValue)
                        {
                            baseTimeSpan = absoluteTime;
                            VectorCanFdAscWriter.WriteHeader(writer, fileDate.Add(absoluteTime));
                        }

                        double offsetSec = (absoluteTime - baseTimeSpan.Value).TotalSeconds;

                        foreach (MatrixCsvColumn column in columns)
                        {
                            if (column.ColumnIndex >= parts.Length)
                                continue;

                            string cell = parts[column.ColumnIndex];
                            if (MatrixCsvLogParser.IsCellEmpty(cell))
                                continue;

                            if (!MatrixCsvLogParser.TryParsePayloadHex(cell, msgBytes))
                            {
                                skippedCells++;
                                continue;
                            }

                            VectorCanFdAscWriter.WriteFrame(writer, offsetSec, "Rx", column.Id, msgBytes);
                            frameCount++;
                        }
                    }

                    hasData = baseTimeSpan.HasValue;
                    if (!hasData)
                        VectorCanFdAscWriter.WriteHeader(writer, DateTime.Today);
                });

                if (!hasData)
                    context.Log("Предупреждение: в CSV нет строк данных. Создан пустой ASC.");
                if (skippedCells > 0)
                    context.Log($"Предупреждение: пропущено ячеек с невалидным hex: {skippedCells}.");

                context.Log($"Конвертация завершена. Записано кадров: {frameCount:N0}.");
                return ProcessingResult.Ok(ascPath, frameCount);
            }
            catch (InvalidDataException ex)
            {
                return context.Fail(ex.Message);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return context.Fail($"Ошибка конвертации: {ex.Message}");
            }
        }
    }
}
