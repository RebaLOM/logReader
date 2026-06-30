using System.Globalization;
using System.Text;

namespace logReader.UI
{
    internal sealed class MatrixCsvToAscConverter
    {
        public void Convert(string csvPath, string ascPath, Action<string> log)
        {
            if (!File.Exists(csvPath))
            {
                log($"Ошибка: файл не найден: {csvPath}");
                return;
            }

            Encoding encoding;
            try
            {
                encoding = LogFileEncoding.Detect(csvPath);
            }
            catch (Exception ex)
            {
                log($"Ошибка определения кодировки: {ex.Message}");
                return;
            }

            if (!MatrixCsvLogParser.LooksLikeMatrixCsv(csvPath, encoding))
            {
                log("Ошибка: файл не является CSV-логом поддерживаемого формата.");
                return;
            }

            List<MatrixCsvColumn> columns = new();
            bool headerRead = false;
            var timeTracker = new MatrixCsvTimeTracker();
            TimeSpan? baseTimeSpan = null;
            DateTime fileDate = File.GetLastWriteTime(csvPath).Date;

            int frameCount = 0;
            int skippedCells = 0;
            Span<int> msgBytes = stackalloc int[8];

            try
            {
                using var writer = new StreamWriter(ascPath, false, new UTF8Encoding(false));

                foreach (string line in File.ReadLines(csvPath, encoding))
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    if (!headerRead)
                    {
                        if (!MatrixCsvLogParser.TryReadHeader(line, out columns, out _))
                        {
                            log("Ошибка: не удалось прочитать заголовок CSV.");
                            return;
                        }

                        headerRead = true;
                        continue;
                    }

                    string[] parts = line.Split(';');
                    if (parts.Length < 2)
                        continue;
                    if (!timeTracker.TryAdvance(parts[0], out TimeSpan absoluteTime))
                        continue;

                    if (!baseTimeSpan.HasValue)
                    {
                        baseTimeSpan = absoluteTime;
                        VectorCanFdAscWriter.WriteHeader(
                            writer,
                            fileDate.Add(absoluteTime));
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

                if (!baseTimeSpan.HasValue)
                {
                    log("Предупреждение: в CSV нет строк данных. Создан пустой ASC.");
                    VectorCanFdAscWriter.WriteHeader(writer, DateTime.Today);
                }
            }
            catch (Exception ex)
            {
                log($"Ошибка записи файла: {ex.Message}");
                return;
            }

            if (skippedCells > 0)
                log($"Предупреждение: пропущено ячеек с невалидным hex: {skippedCells}.");

            log($"Конвертация завершена. Записано кадров: {frameCount:N0}.");
        }
    }
}
