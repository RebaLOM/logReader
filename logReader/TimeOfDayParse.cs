using System.Globalization;

namespace logReader
{
    // Разбор HH:mm:ss и HH:mm:ss.fff (также ".2", ".257") для matrix-CSV и родственных логов.
    public static class TimeOfDayParse
    {
        public static bool TryParse(string? raw, out TimeSpan time)
        {
            time = default;
            if (string.IsNullOrWhiteSpace(raw)) return false;

            string token = raw.Trim().Replace(',', '.');
            string[] parts = token.Split(':');
            if (parts.Length != 3) return false;
            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int hours))
                return false;
            if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int minutes))
                return false;

            string[] secParts = parts[2].Split('.', 2);
            if (!int.TryParse(secParts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int seconds))
                return false;

            int milliseconds = 0;
            if (secParts.Length == 2 && secParts[1].Length > 0)
            {
                string frac = secParts[1];
                if (frac.Length > 3)
                    frac = frac[..3];
                else if (frac.Length < 3)
                    frac = frac.PadRight(3, '0');

                if (!int.TryParse(frac, NumberStyles.Integer, CultureInfo.InvariantCulture, out milliseconds))
                    return false;
            }

            if (hours < 0 || minutes is < 0 or > 59 || seconds is < 0 or > 59 || milliseconds is < 0 or > 999)
                return false;

            time = new TimeSpan(0, hours, minutes, seconds, milliseconds);
            return true;
        }
    }
}
