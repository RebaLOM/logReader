using System.Globalization;
using System.Text;

namespace logReader.Processing
{
    internal readonly record struct MatrixCsvColumn(int ColumnIndex, string Id);
    internal sealed record MatrixCsvHeader(IReadOnlyList<MatrixCsvColumn> Columns, string? FirstDataLine);

    // Табличный CSV: перед строкой CAN ID может быть шапка, в данных время слева и hex-payload в ячейках.
    internal static class MatrixCsvLogParser
    {
        public const int RowPeriodMs = 20;

        public static bool LooksLikeMatrixCsv(string path, Encoding? encoding = null)
        {
            if (!File.Exists(path)) return false;
            try
            {
                encoding ??= LogFileEncoding.Detect(path);
                using var lines = LogFileReader.ReadLines(path, encoding).GetEnumerator();
                return FindHeader(lines) != null;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        // Проверяем строки до первой временной метки после объявления ID:
        // в шапке тоже может встретиться строка с пустой первой ячейкой и hex-значениями.
        public static MatrixCsvHeader? FindHeader(IEnumerator<string> lines, List<string>? preamble = null)
        {
            IReadOnlyList<MatrixCsvColumn>? columns = null;
            int preambleLength = 0;
            string? firstDataLine = null;
            while (lines.MoveNext())
            {
                string line = lines.Current;
                string[] parts = line.Split(';');
                if (StepCsvLogParser.IsResultHeader(parts))
                    return null;

                if (columns != null && TryParseTimeCell(parts[0], out _))
                {
                    firstDataLine = line;
                    break;
                }

                if (ReadColumns(parts) is { } candidate)
                {
                    columns = candidate;
                    preambleLength = preamble?.Count ?? 0;
                }
                else if (columns == null && StepCsvLogParser.IsFrameRow(parts))
                {
                    return null;
                }

                preamble?.Add(line);
            }

            if (columns == null) return null;
            preamble?.RemoveRange(preambleLength, preamble.Count - preambleLength);
            return new MatrixCsvHeader(columns, firstDataLine);
        }

        public static IEnumerable<string> ReadDataLines(IEnumerator<string> lines, MatrixCsvHeader header)
        {
            if (header.FirstDataLine == null) yield break;
            yield return header.FirstDataLine;
            while (lines.MoveNext()) yield return lines.Current;
        }

        // Первая ячейка пустая, далее — CAN ID (hex); пустые колонки сохраняют индекс.
        private static List<MatrixCsvColumn>? ReadColumns(string[] parts)
        {
            if (parts.Length < 3 || !string.IsNullOrWhiteSpace(parts[0])) return null;

            var columns = new List<MatrixCsvColumn>();
            for (int i = 1; i < parts.Length; i++)
            {
                string cell = parts[i].Trim();
                if (string.IsNullOrEmpty(cell)) continue;
                if (!CanToken.TryNormalizeId(cell, out string id, minHexLength: 1))
                    return null;
                columns.Add(new MatrixCsvColumn(i, id));
            }

            return columns.Count >= 2 ? columns : null;
        }

        public static bool TryParseTimeCell(string? raw, out TimeSpan time)
            => TimeOfDayParse.TryParse(raw, out time);

        // Hex без пробелов; хвостовые нулевые байты могут быть обрезаны — выравнивание вправо в 8 байт.
        public static bool TryParsePayloadHex(string? raw, Span<int> bytes)
        {
            bytes.Clear();

            if (string.IsNullOrWhiteSpace(raw)) return false;

            string token = raw.Trim();
            foreach (char c in token)
            {
                bool isHex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!isHex) return false;
            }

            if (token.Length % 2 == 1)
                token = "0" + token;

            if (token.Length > 16)
                token = token[^16..];

            token = token.PadLeft(16, '0');

            for (int i = 0; i < 8; i++)
            {
                if (!int.TryParse(token.AsSpan(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int b))
                    return false;
                if (b is < 0 or > 255) return false;
                bytes[i] = b;
            }

            return true;
        }

        public static bool IsCellEmpty(string? cell) => string.IsNullOrWhiteSpace(cell);

        public static string FormatTimeWithMs(TimeSpan time) =>
            string.Format(
                CultureInfo.InvariantCulture,
                "{0:00}:{1:00}:{2:00}.{3:000}",
                (int)Math.Floor(time.TotalHours),
                time.Minutes,
                time.Seconds,
                time.Milliseconds);
    }

    // В колонке времени — HH:mm:ss или HH:mm:ss.fff; при одинаковом токене наращиваем +20 мс.
    internal sealed class MatrixCsvTimeTracker
    {
        private string _lastTimeCell = "";
        private int _rowOffset;
        private TimeSpan _lastBaseTime;

        public bool TryAdvance(string timeCell, out TimeSpan absolute)
        {
            if (!MatrixCsvLogParser.TryParseTimeCell(timeCell, out TimeSpan baseTime))
            {
                absolute = default;
                return false;
            }

            string key = timeCell.Trim();
            if (!string.Equals(key, _lastTimeCell, StringComparison.Ordinal))
            {
                _lastTimeCell = key;
                _lastBaseTime = baseTime;
                _rowOffset = 0;
            }
            else
            {
                _rowOffset++;
            }

            // Если в файле уже есть миллисекунды — берём их; +20 мс только при полном дубле токена.
            absolute = _rowOffset == 0
                ? baseTime
                : _lastBaseTime.Add(TimeSpan.FromMilliseconds(_rowOffset * MatrixCsvLogParser.RowPeriodMs));
            return true;
        }
    }
}
