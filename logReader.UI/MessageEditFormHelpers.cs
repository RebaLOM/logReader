using System.Globalization;

namespace logReader.UI
{
    // Общие хелперы форм редактирования посылок DBC/XLSX.
    internal static class MessageEditFormHelpers
    {
        public static Label MakeLabel(string text)
            => new()
            {
                Text = text,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(3, 7, 3, 0)
            };

        public static Label MakeLabel(string text, int x, int y)
            => new() { Text = text, Location = new Point(x, y), AutoSize = true };

        public static bool TryParseHexId(string text, bool isExtended, out uint id, out string error)
        {
            id = 0;
            error = "";

            string idText = (text ?? "").Trim();
            if (idText.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                idText = idText.Substring(2);

            if (!uint.TryParse(idText, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out id))
            {
                error = "ID должен быть 16-ричным числом.";
                return false;
            }

            uint maxId = isExtended ? 0x1FFFFFFFu : 0x7FFu;
            if (id > maxId)
            {
                error = isExtended
                    ? "ID выходит за пределы 29-битного диапазона (0..1FFFFFFF)."
                    : "ID выходит за пределы 11-битного диапазона (0..7FF).";
                return false;
            }

            return true;
        }

        public static void MakeGridColumnsNotSortable(DataGridView grid)
        {
            foreach (DataGridViewColumn col in grid.Columns)
                col.SortMode = DataGridViewColumnSortMode.NotSortable;
        }
    }
}
