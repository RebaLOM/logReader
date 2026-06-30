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

        // Пропорции Fill: широкие Name/ID, узкие DLC и счётчики.
        public static void ApplyDevicesListColumnWeights(DataGridView grid)
        {
            SetFill(grid, "Name", 44, 120);
            SetFill(grid, "Id", 32, 100);
            SetFill(grid, "Fmt", 14, 72);
            SetFill(grid, "Dlc", 5, 40);
            SetFill(grid, "Count", 7, 56);
        }

        public static void ApplySignalListColumnWeights(DataGridView grid)
        {
            SetFill(grid, "Name", 36, 140);
            SetFill(grid, "ByteIdx", 7, 52);
            SetFill(grid, "StartBit", 7, 52);
            SetFill(grid, "Length", 7, 52);
            SetFill(grid, "Type", 9, 64);
            SetFill(grid, "Factor", 11, 56);
            SetFill(grid, "Offset", 11, 56);
            SetFill(grid, "Unit", 10, 48);
            SetFill(grid, "Order", 12, 72);
        }

        private static void SetFill(DataGridView grid, string name, int weight, int minWidth)
        {
            if (grid.Columns[name] is not DataGridViewColumn col) return;
            col.FillWeight = weight;
            col.MinimumWidth = minWidth;
        }

        public static bool TryParseOptionalInt(string? text, out int? value)
        {
            value = null;
            string t = (text ?? "").Trim();
            if (t.Length == 0) return true;
            if (!int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n))
                return false;
            value = n;
            return true;
        }

        public static bool InOptionalRange(int value, int? min, int? max)
        {
            if (min.HasValue && value < min.Value) return false;
            if (max.HasValue && value > max.Value) return false;
            return true;
        }

        public static bool TextMatchesQuery(string? haystack, string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return true;
            return (haystack ?? "").Contains(query.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        public static bool IdMatchesQuery(string idHex, string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return true;
            string q = query.Trim();
            if (q.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                q = q.Substring(2);
            return idHex.Contains(q, StringComparison.OrdinalIgnoreCase);
        }

        public static int SelectedSourceIndex(DataGridView grid)
            => grid.CurrentRow?.Tag is int idx ? idx : -1;

        public static TextBox MakeFilterTextBox(int width = 44)
            => new()
            {
                Width = width,
                Margin = new Padding(3, 3, 8, 0)
            };

        public static ComboBox MakeFormatFilterCombo()
        {
            var cmb = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 110,
                Margin = new Padding(3, 3, 8, 0)
            };
            cmb.Items.AddRange(new object[] { "Все", "Standard", "Extended" });
            cmb.SelectedIndex = 0;
            return cmb;
        }

        public static ComboBox MakeSignalTypeFilterCombo(bool includeBin)
        {
            var cmb = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 100,
                Margin = new Padding(3, 3, 8, 0)
            };
            if (includeBin)
                cmb.Items.AddRange(new object[] { "Все", "int", "unsigned", "BIN" });
            else
                cmb.Items.AddRange(new object[] { "Все", "int", "unsigned" });
            cmb.SelectedIndex = 0;
            return cmb;
        }

        public static ComboBox MakeByteOrderFilterCombo()
        {
            var cmb = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 100,
                Margin = new Padding(3, 3, 8, 0)
            };
            cmb.Items.AddRange(new object[] { "Все", "Intel", "Motorola" });
            cmb.SelectedIndex = 0;
            return cmb;
        }

        public static DialogResult PromptSaveChanges(IWin32Window owner, string description)
        {
            string body = string.IsNullOrWhiteSpace(description)
                ? "Сохранить изменения?"
                : "Сохранить изменения?\n\n" + description.Trim();

            return MessageBox.Show(
                owner,
                body,
                "Подтверждение",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);
        }

        // Да — trySave(); Нет — закрыть без сохранения; Отмена — e.Cancel = true.
        public static void ResolveFormCloseWithDirty(
            Form form,
            FormClosingEventArgs e,
            bool dirty,
            bool suppressPrompt,
            string description,
            Func<bool> trySave,
            DialogResult discardDialogResult = DialogResult.Cancel)
        {
            if (suppressPrompt || !dirty || e.Cancel)
                return;

            switch (PromptSaveChanges(form, description))
            {
                case DialogResult.Yes:
                    if (!trySave())
                        e.Cancel = true;
                    break;
                case DialogResult.No:
                    form.DialogResult = discardDialogResult;
                    break;
                default:
                    e.Cancel = true;
                    break;
            }
        }
    }
}
