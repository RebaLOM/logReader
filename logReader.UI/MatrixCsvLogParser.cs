using System.Globalization;
using System.Text;

namespace logReader.UI
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
                foreach (string line in File.ReadLines(path, encoding))
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
                if (!CanToken.TryNormalizeId(cell, out string id, minHexLength: 6))
                    return false;
                columns.Add(new MatrixCsvColumn(i, id));
                ids.Add(id);
            }

            return columns.Count >= 2;
        }

        public static bool TryParseTimeCell(string? raw, out TimeSpan time)
        {
            time = default;
            if (string.IsNullOrWhiteSpace(raw)) return false;

            string[] parts = raw.Trim().Split(':');
            if (parts.Length != 3) return false;
            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int hours))
                return false;
            if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int minutes))
                return false;
            if (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int seconds))
                return false;

            if (hours < 0 || minutes is < 0 or > 59 || seconds is < 0 or > 59)
                return false;

            time = new TimeSpan(hours, minutes, seconds);
            return true;
        }

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

    // В колонке времени — только секунды; внутри секунды наращиваем +20 мс по порядку строк.
    internal sealed class MatrixCsvTimeTracker
    {
        private string _lastTimeCell = "";
        private int _rowOffset;

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
                _rowOffset = 0;
            }
            else
            {
                _rowOffset++;
            }

            absolute = baseTime.Add(TimeSpan.FromMilliseconds(_rowOffset * MatrixCsvLogParser.RowPeriodMs));
            return true;
        }
    }
}
