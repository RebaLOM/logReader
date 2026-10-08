using System.Globalization;
using logReader.UI.Controls;
using logReader.UI.Icons;
using logReader.UI.Theme;

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
                Font = Typography.Body,
                ForeColor = AppTheme.TextSecondary,
                Margin = new Padding(0, 8, 8, 0)
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
            SetFill(grid, "Fmt", 14, 100);
            SetFill(grid, "Dlc", 5, 56);
            SetFill(grid, "Count", 7, 80);
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

            SetFill(grid, "Name", 34, 160);
            SetFill(grid, "ByteIdx", 7, 52);
            SetFill(grid, "StartBit", 7, 52);
            SetFill(grid, "Length", 7, 64);
            SetFill(grid, "Type", 9, 80);
            SetFill(grid, "Factor", 11, 84);
            SetFill(grid, "Offset", 11, 88);
            SetFill(grid, "Unit", 10, 64);
            SetFill(grid, "Order", 12, 94);
        }

        public static void AddSignalColorColumn(DataGridView grid)
        {
            var colorCol = new DataGridViewTextBoxColumn
            {
                Name = "Color",
                HeaderText = "",
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
                e.Paint(e.CellBounds, DataGridViewPaintParts.Border | DataGridViewPaintParts.Background | DataGridViewPaintParts.SelectionBackground);

                Color? color = getColorForRow(e.RowIndex);
                if (color is Color c && e.Graphics != null)
                {
                    int swatchSize = UiScale.Px(grid, 16);
                    var swatch = new Rectangle(
                        e.CellBounds.X + (e.CellBounds.Width - swatchSize) / 2,
                        e.CellBounds.Y + (e.CellBounds.Height - swatchSize) / 2,
                        swatchSize,
                        swatchSize);
                    using var brush = new SolidBrush(c);
                    using var border = new Pen(AppTheme.BorderHover);
                    e.Graphics.FillRectangle(brush, swatch);
                    e.Graphics.DrawRectangle(border, swatch);
                }
            };
        }

        public static Panel BuildSignalListWithPayloadGrid(
            DataGridView signalGrid,
            CanPayloadGridControl payloadGrid)
        {
            var host = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };

            var split = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1
            };
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            split.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            split.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            payloadGrid.Mode = CanPayloadGridMode.View;
            payloadGrid.ShowLegend = false;
            payloadGrid.Margin = new Padding(0, 12, 0, 12);
            payloadGrid.Dock = DockStyle.Top;
            var payloadCard = new ModernCard
            {
                Dock = DockStyle.Fill, Width = 320, MinimumSize = new Size(320, 0),
                Margin = new Padding(16, 0, 0, 0), Padding = new Padding(16), AutoScroll = true
            };
            var payloadContent = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 3 };
            payloadContent.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var title = new Label { Text = "Карта данных", Font = Typography.CardTitle, ForeColor = AppTheme.TextPrimary, AutoSize = true, Margin = new Padding(0) };
            var hint = new Label
            {
                Text = "Выберите сигнал в таблице или нажмите на его биты.",
                Font = Typography.Caption, ForeColor = AppTheme.TextSecondary,
                AutoSize = true, MaximumSize = new Size(280, 0), Margin = new Padding(0)
            };
            payloadContent.Controls.Add(title, 0, 0);
            payloadContent.Controls.Add(payloadGrid, 0, 1);
            payloadContent.Controls.Add(hint, 0, 2);
            payloadCard.Controls.Add(payloadContent);
            split.Controls.Add(payloadCard, 1, 0);

            signalGrid.Dock = DockStyle.Fill;
            signalGrid.Margin = new Padding(0);
            split.Controls.Add(signalGrid, 0, 0);

            host.Controls.Add(split);
            return host;
        }

        public static Control BuildMessageMetadata(
            TextBox name, TextBox id, NumericUpDown dlc,
            RadioButton standard, RadioButton extended, bool idReadOnly = false)
        {
            name.Dock = DockStyle.Fill;
            name.PlaceholderText = "Название посылки";
            id.Dock = DockStyle.Fill;
            id.ReadOnly = idReadOnly;
            id.Font = Typography.Mono;
            id.PlaceholderText = "Например, 18FF0100";
            dlc.Minimum = 1;
            dlc.Maximum = 8;
            dlc.Value = 8;
            dlc.Dock = DockStyle.Fill;
            standard.Text = "Standard · 11 бит";
            extended.Text = "Extended · 29 бит";
            standard.AutoSize = extended.AutoSize = true;
            standard.Margin = extended.Margin = new Padding(0, 0, 0, 8);
            var format = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0) };
            format.Controls.AddRange(new Control[] { standard, extended });

            var fields = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 4, RowCount = 1, Margin = new Padding(0) };
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 26));
            fields.Controls.Add(UiFactory.Field("Имя посылки", name), 0, 0);
            fields.Controls.Add(UiFactory.Field("CAN ID · hex", id, idReadOnly ? "ID существующей посылки фиксирован." : "11 или 29 бит, в зависимости от формата."), 1, 0);
            fields.Controls.Add(UiFactory.Field("DLC, байт", dlc), 2, 0);
            fields.Controls.Add(UiFactory.Field("Формат кадра", format), 3, 0);
            foreach (Control field in fields.Controls)
            {
                field.Dock = DockStyle.Fill;
                field.Margin = new Padding(0, 0, 16, 0);
            }
            fields.GetControlFromPosition(3, 0)!.Margin = new Padding(0);
            var card = new ModernCard { Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(0, 0, 0, 16) };
            card.Controls.Add(fields);
            return card;
        }

        public static Control BuildRangeFilter(string label, TextBox min, TextBox max)
        {
            min.Width = max.Width = 52;
            min.Margin = max.Margin = new Padding(0);
            var range = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0) };
            range.Controls.Add(min);
            range.Controls.Add(new Label { Text = "—", Font = Typography.Body, ForeColor = AppTheme.TextMuted, AutoSize = true, Margin = new Padding(8, 8, 8, 0) });
            range.Controls.Add(max);
            return BuildFilterField(label, range, 144);
        }

        public static Control BuildFilterField(string label, Control input, int width)
        {
            var field = UiFactory.Field(label, input);
            field.Width = width;
            field.MinimumSize = new Size(width, 0);
            field.MaximumSize = new Size(width, 0);
            field.Margin = new Padding(0, 0, 12, 8);
            return field;
        }

        public static Control BuildEditorFooter(InlineNotice notice, params Control[] actions)
        {
            var footer = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, RowCount = 1, Margin = new Padding(0, 16, 0, 0) };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            notice.Dock = DockStyle.Fill;
            notice.Margin = new Padding(0, 0, 16, 0);
            var buttons = UiFactory.Footer(actions);
            buttons.Dock = DockStyle.Fill;
            buttons.Margin = new Padding(0);
            footer.Controls.Add(notice, 0, 0);
            footer.Controls.Add(buttons, 1, 0);
            return footer;
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
            => grid.CurrentRow is { Selected: true, Tag: int idx } ? idx : -1;

        public static TextBox MakeFilterTextBox(int width = 44)
            => new ModernTextBox
            {
                Width = width,
                Margin = new Padding(0)
            };

        public static ComboBox MakeFormatFilterCombo()
        {
            var cmb = new ModernComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 132,
                Margin = new Padding(0)
            };
            cmb.Items.AddRange(new object[] { "Все", "Standard", "Extended" });
            cmb.SelectedIndex = 0;
            return cmb;
        }

        public static ComboBox MakeSignalTypeFilterCombo(bool includeBin)
        {
            var cmb = new ModernComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 120,
                Margin = new Padding(0)
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
            var cmb = new ModernComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 120,
                Margin = new Padding(0)
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

            return AppDialog.Show(
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
