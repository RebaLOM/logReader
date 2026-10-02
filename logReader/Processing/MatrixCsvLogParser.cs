using System.Globalization;
using System.Text;

namespace logReader.Processing
{
    internal readonly record struct MatrixCsvColumn(int ColumnIndex, string Id);

    // Широкий matrix-CSV: ID в первой строке, время слева, hex-payload в ячейках (цикл 20 мс).
    internal static class MatrixCsvLogParser
    {
        public const int RowPeriodMs = 20;

        public static bool LooksLikeMatrixCsv(string path, Encoding? encoding = null)
        {
            if (!File.Exists(path)) return false;
            encoding ??= LogFileEncoding.Detect(path);
            try
            {
                foreach (string line in LogFileReader.ReadLines(path, encoding))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    return TryReadHeader(line, out _, out _);
                }
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }

            return false;
        }

        // Первая ячейка пустая, далее — CAN ID (hex); пустые колонки сохраняют индекс.
        public static bool TryReadHeader(string line, out List<MatrixCsvColumn> columns, out List<string> ids)
        {
            columns = new List<MatrixCsvColumn>();
            ids = new List<string>();
            if (string.IsNullOrWhiteSpace(line)) return false;

            string[] parts = line.Split(';');
            if (parts.Length < 3) return false;
            if (!string.IsNullOrWhiteSpace(parts[0])) return false;

            for (int i = 1; i < parts.Length; i++)
            {
                string cell = parts[i].Trim();
                if (string.IsNullOrEmpty(cell)) continue;
                if (!CanToken.TryNormalizeId(cell, out string id, minHexLength: 1))
                    return false;
                columns.Add(new MatrixCsvColumn(i, id));
                ids.Add(id);
            }

            return columns.Count >= 2;
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
