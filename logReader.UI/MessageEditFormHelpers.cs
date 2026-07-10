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
            if (grid.Columns["Color"] is DataGridViewColumn colorCol)
            {
                colorCol.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                colorCol.Width = 32;
                colorCol.MinimumWidth = 32;
                colorCol.Resizable = DataGridViewTriState.False;
            }

            SetFill(grid, "Name", 34, 130);
            SetFill(grid, "ByteIdx", 7, 52);
            SetFill(grid, "StartBit", 7, 52);
            SetFill(grid, "Length", 7, 52);
            SetFill(grid, "Type", 9, 64);
            SetFill(grid, "Factor", 11, 56);
            SetFill(grid, "Offset", 11, 56);
            SetFill(grid, "Unit", 10, 48);
            SetFill(grid, "Order", 12, 72);
        }

        public static void AddSignalColorColumn(DataGridView grid)
        {
            var colorCol = new DataGridViewTextBoxColumn
            {
                Name = "Color",
                HeaderText = "Color",
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                Width = 32,
                MinimumWidth = 32,
                Resizable = DataGridViewTriState.False
            };
            grid.Columns.Insert(0, colorCol);
        }

        public static void WireSignalColorColumnPainting(
            DataGridView grid,
            Func<int, Color?> getColorForRow)
        {
            grid.CellPainting += (_, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                if (grid.Columns[e.ColumnIndex].Name != "Color") return;

                e.Handled = true;
                e.Paint(e.CellBounds, DataGridViewPaintParts.Border | DataGridViewPaintParts.Background);

                Color? color = getColorForRow(e.RowIndex);
                if (color is Color c && e.Graphics != null)
                {
                    var swatch = new Rectangle(
                        e.CellBounds.X + (e.CellBounds.Width - 16) / 2,
                        e.CellBounds.Y + (e.CellBounds.Height - 16) / 2,
                        16,
                        16);
                    using var brush = new SolidBrush(c);
                    e.Graphics.FillRectangle(brush, swatch);
                    e.Graphics.DrawRectangle(Pens.Gray, swatch);
                }
            };
        }

        public static Panel BuildSignalListWithPayloadGrid(
            DataGridView signalGrid,
            CanPayloadGridControl payloadGrid)
        {
            var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 0, 12, 6) };

            var split = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1
            };
            split.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            payloadGrid.Mode = CanPayloadGridMode.View;
            payloadGrid.ShowLegend = false;
            payloadGrid.Margin = new Padding(0, 0, 12, 0);
            payloadGrid.Dock = DockStyle.Fill;
            split.Controls.Add(payloadGrid, 0, 0);

            signalGrid.Dock = DockStyle.Fill;
            split.Controls.Add(signalGrid, 1, 0);

            host.Controls.Add(split);
            return host;
        }

        public static int FindSourceIndexByName(DataGridView grid, string name)
        {
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.Tag is not int idx) continue;
                if (row.Cells["Name"].Value is string rowName
                    && rowName.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return idx;
            }
            return -1;
        }

        public static void SelectRowBySourceIndex(DataGridView grid, int sourceIndex)
        {
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.Tag is int t && t == sourceIndex)
                {
                    row.Selected = true;
                    if (row.Cells.Count > 0)
                        grid.CurrentCell = row.Cells[grid.Columns["Name"]?.Index ?? 0];
                    return;
                }
            }
        }

        public static void SelectRowByName(DataGridView grid, string name)
        {
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.Cells["Name"].Value is string rowName
                    && rowName.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    row.Selected = true;
                    grid.CurrentCell = row.Cells[grid.Columns["Name"]?.Index ?? 0];
                    return;
                }
            }
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
