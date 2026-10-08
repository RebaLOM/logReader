using System.Globalization;
using System.Linq;
using logReader;

namespace logReader.UI
{
    internal sealed class CompositeParamEditForm : Form
    {
        private readonly ModernTextBox _txtBlock = new();
        private readonly ModernTextBox _txtParam = new();
        private readonly ModernTextBox _txtScale = new();
        private readonly ModernTextBox _txtOffset = new();
        private readonly CheckBox _chkSigned = new();
        private readonly ModernTextBox _txtUnit = new();
        private readonly ModernTextBox _txtMin = new();
        private readonly ModernTextBox _txtMax = new();

        private readonly DataGridView _grid = new();
        private readonly InlineNotice _validation = new() { Visible = false, Tone = StatusTone.Error };
        private readonly ModernButton _btnDeletePiece = new();
        private readonly ModernButton _btnUp = new();
        private readonly ModernButton _btnDown = new();
        private readonly Label _pieceCount = new() { AutoSize = true };

        public CompositeSignal Signal { get; private set; } = new();

        public CompositeParamEditForm(CompositeSignal? existing)
        {
            SuspendLayout();
            Text = existing == null ? "Новый составной параметр" : "Изменить составной параметр";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96, 96);
            MinimumSize = new Size(760, 520);
            ClientSize = new Size(960, 780);
            Icon = Application.OpenForms.OfType<MainForm>().FirstOrDefault()?.Icon;

            BuildLayout();
            ResumeLayout(true);
            AppTheme.StyleGrid(_grid, "Добавьте фрагмент, чтобы собрать составной параметр.");
            AppTheme.Apply(this);

            if (existing != null)
                LoadFrom(existing);
            else
            {
                _txtBlock.Text = CompositeDefaults.BlockName;
                _txtScale.Text = "1";
                _txtOffset.Text = "0";
            }
            UpdatePieceActions();
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 4 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(UiFactory.Header("Составной параметр", "Соберите одно значение из битов разных CAN-сообщений.", IconKind.Sliders), 0, 0);
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Margin = Padding.Empty };
            var content = new TableLayoutPanel { Dock = DockStyle.Top, Height = 540, ColumnCount = 1, RowCount = 2 };
            scroll.ClientSizeChanged += (_, _) => content.Height = Math.Max(UiScale.Px(scroll, 540), scroll.ClientSize.Height);
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var detailsCard = new ModernCard { AutoSize = true, Dock = DockStyle.Top, Margin = new Padding(0, 0, 0, 16) };
            var top = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 3,
                AutoSize = true
            };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));
            _txtParam.PlaceholderText = "Имя в результирующей таблице";
            _txtUnit.PlaceholderText = "Например, °C";
            _txtMin.PlaceholderText = "Без ограничения";
            _txtMax.PlaceholderText = "Без ограничения";
            AddField("Блок", _txtBlock, 0, 0);
            AddField("Имя параметра", _txtParam, 1, 0);
            AddField("Единица измерения", _txtUnit, 2, 0);
            AddField("Множитель (Scale)", _txtScale, 0, 1);
            AddField("Смещение (Offset)", _txtOffset, 1, 1);
            _chkSigned.Text = "Знаковое исходное значение";
            _chkSigned.AutoSize = true;
            _chkSigned.Anchor = AnchorStyles.Left;
            top.Controls.Add(UiFactory.Field("Представление", _chkSigned), 2, 1);
            AddField("Минимум", _txtMin, 0, 2);
            AddField("Максимум", _txtMax, 1, 2);
            top.Controls.Add(new Label { Text = "Значение = raw × Scale + Offset", AutoSize = true, Anchor = AnchorStyles.Left, Font = Typography.Caption, ForeColor = AppTheme.TextSecondary, Margin = new Padding(0, 20, 0, 0) }, 2, 2);
            detailsCard.Controls.Add(top);
            content.Controls.Add(detailsCard, 0, 0);

            void AddField(string label, Control input, int column, int row)
            {
                var field = UiFactory.Field(label, input);
                field.Dock = DockStyle.Fill;
                field.Margin = new Padding(0, 0, column == 2 ? 0 : 12, 8);
                top.Controls.Add(field, column, row);
            }

            _grid.Dock = DockStyle.Fill;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToResizeRows = false;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.EditMode = DataGridViewEditMode.EditOnEnter;

            var colSource = new DataGridViewTextBoxColumn { Name = "SourceID", HeaderText = "CAN ID (hex)", FillWeight = 32, MinimumWidth = 120 };
            var colByte = new DataGridViewTextBoxColumn { Name = "Byte", HeaderText = "Байт · 0–7", FillWeight = 17, MinimumWidth = 100 };
            var colBitStart = new DataGridViewTextBoxColumn { Name = "BitStart", HeaderText = "Первый бит · 0–7", FillWeight = 20, MinimumWidth = 136 };
            var colBitLen = new DataGridViewTextBoxColumn { Name = "BitLen", HeaderText = "Бит · 1–8", FillWeight = 17, MinimumWidth = 100 };
            var colTrigger = new DataGridViewCheckBoxColumn { Name = "Trigger", HeaderText = "Триггер", FillWeight = 14, MinimumWidth = 90 };
            _grid.Columns.AddRange(colSource, colByte, colBitStart, colBitLen, colTrigger);

            // Один триггер на параметр — иначе момент формирования значения неоднозначен.
            _grid.CellValueChanged += (_, e) =>
            {
                if (e.RowIndex < 0) return;
                if (_grid.Columns[e.ColumnIndex].Name != "Trigger") return;
                if (_grid.Rows[e.RowIndex].Cells["Trigger"].Value is bool on && on)
                {
                    for (int r = 0; r < _grid.Rows.Count; r++)
                        if (r != e.RowIndex)
                            _grid.Rows[r].Cells["Trigger"].Value = false;
                }
            };
            _grid.CurrentCellDirtyStateChanged += (_, _) =>
            {
                if (_grid.IsCurrentCellDirty && _grid.CurrentCell is DataGridViewCheckBoxCell)
                    _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };

            var piecesCard = new ModernCard { Dock = DockStyle.Fill, Margin = Padding.Empty };
            var piecesLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            piecesLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            piecesLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            piecesLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            piecesLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var pieceBtns = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = Padding.Empty,
                WrapContents = true,
                Margin = new Padding(0, 0, 0, 8)
            };
            var btnAddPiece = new ModernButton { Text = "Добавить фрагмент", AutoSize = true, Variant = ButtonVariant.Secondary, Icon = IconKind.Plus };
            btnAddPiece.Click += (_, _) =>
            {
                int row = _grid.Rows.Add("", "0", "0", "8", false);
                _grid.CurrentCell = _grid.Rows[row].Cells[0];
                _grid.BeginEdit(true);
            };
            _btnDeletePiece.Text = "Удалить";
            _btnDeletePiece.AutoSize = true;
            _btnDeletePiece.Variant = ButtonVariant.Danger;
            _btnDeletePiece.Icon = IconKind.Trash;
            _btnDeletePiece.Click += (_, _) => { if (_grid.CurrentRow != null) _grid.Rows.Remove(_grid.CurrentRow); };
            _btnUp.Text = "Вверх";
            _btnUp.AutoSize = true;
            _btnUp.Variant = ButtonVariant.Ghost;
            _btnUp.Icon = IconKind.ArrowUp;
            _btnUp.Click += (_, _) => MoveRow(-1);
            _btnDown.Text = "Вниз";
            _btnDown.AutoSize = true;
            _btnDown.Variant = ButtonVariant.Ghost;
            _btnDown.Icon = IconKind.ArrowDown;
            _btnDown.Click += (_, _) => MoveRow(+1);
            _pieceCount.Font = Typography.Secondary;
            _pieceCount.ForeColor = AppTheme.TextSecondary;
            _pieceCount.Margin = new Padding(16, 12, 0, 0);
            pieceBtns.Controls.AddRange(new Control[] { btnAddPiece, _btnDeletePiece, _btnUp, _btnDown, _pieceCount });
            var info = new Label { AutoSize = true, Dock = DockStyle.Top, Font = Typography.Caption, ForeColor = AppTheme.TextSecondary, Margin = new Padding(0, 0, 0, 12), Text = "Сверху — старшие биты, снизу — младшие. Триггер задаёт сообщение, при получении которого рассчитывается значение." };
            piecesLayout.Controls.Add(pieceBtns, 0, 0);
            piecesLayout.Controls.Add(info, 0, 1);
            piecesLayout.Controls.Add(_grid, 0, 2);
            piecesCard.Controls.Add(piecesLayout);
            content.Controls.Add(piecesCard, 0, 1);
            _grid.SelectionChanged += (_, _) => UpdatePieceActions();
            _grid.RowsAdded += (_, _) => UpdatePieceActions();
            _grid.RowsRemoved += (_, _) => UpdatePieceActions();
            scroll.Controls.Add(content);
            root.Controls.Add(scroll, 0, 1);
            _validation.Dock = DockStyle.Top;
            _validation.Margin = new Padding(0, 12, 0, 0);
            root.Controls.Add(_validation, 0, 2);

            var btnOk = new ModernButton { Text = "Сохранить параметр", AutoSize = true, Variant = ButtonVariant.Primary, Icon = IconKind.Check, DialogResult = DialogResult.None };
            btnOk.Click += BtnOk_Click;
            var btnCancel = new ModernButton { Text = "Отмена", AutoSize = true, Variant = ButtonVariant.Secondary, DialogResult = DialogResult.Cancel };
            root.Controls.Add(UiFactory.Footer(btnOk, btnCancel), 0, 3);
            Controls.Add(root);

            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }

        private void UpdatePieceActions()
        {
            int index = _grid.CurrentRow?.Index ?? -1;
            _btnDeletePiece.Enabled = index >= 0;
            _btnUp.Enabled = index > 0;
            _btnDown.Enabled = index >= 0 && index < _grid.Rows.Count - 1;
            _pieceCount.Text = $"Фрагментов: {_grid.Rows.Count}";
        }

        private void MoveRow(int delta)
        {
            if (_grid.CurrentRow == null) return;
            int idx = _grid.CurrentRow.Index;
            int target = idx + delta;
            if (target < 0 || target >= _grid.Rows.Count) return;

            var values = new object?[_grid.Columns.Count];
            for (int c = 0; c < _grid.Columns.Count; c++)
                values[c] = _grid.Rows[idx].Cells[c].Value;

            _grid.Rows.RemoveAt(idx);
            _grid.Rows.Insert(target, 1);
            for (int c = 0; c < _grid.Columns.Count; c++)
                _grid.Rows[target].Cells[c].Value = values[c];
            _grid.Rows[target].Selected = true;
            _grid.CurrentCell = _grid.Rows[target].Cells[0];
        }

        private void LoadFrom(CompositeSignal sig)
        {
            _txtBlock.Text = sig.Block;
            _txtParam.Text = sig.Param;
            _txtScale.Text = sig.Scale.ToString(CultureInfo.InvariantCulture);
            _txtOffset.Text = sig.Offset.ToString(CultureInfo.InvariantCulture);
            _chkSigned.Checked = sig.Signed;
            _txtUnit.Text = sig.Unit ?? "";
            _txtMin.Text = sig.Min?.ToString(CultureInfo.InvariantCulture) ?? "";
            _txtMax.Text = sig.Max?.ToString(CultureInfo.InvariantCulture) ?? "";

            string trigger = string.IsNullOrWhiteSpace(sig.TriggerId) ? sig.ResolveDefaultTriggerId() : sig.TriggerId;
            int lastTriggerIdx = -1;
            for (int i = 0; i < sig.Pieces.Count; i++)
                if (string.Equals(sig.Pieces[i].SourceId, trigger, StringComparison.OrdinalIgnoreCase))
                    lastTriggerIdx = i;

            for (int i = 0; i < sig.Pieces.Count; i++)
            {
                var p = sig.Pieces[i];
                _grid.Rows.Add(
                    p.SourceId,
                    p.Byte.ToString(CultureInfo.InvariantCulture),
                    p.BitStart.ToString(CultureInfo.InvariantCulture),
                    p.BitLen.ToString(CultureInfo.InvariantCulture),
                    i == lastTriggerIdx);
            }
        }

        private void BtnOk_Click(object? sender, EventArgs e)
        {
            _validation.Visible = false;
            foreach (var field in new[] { _txtParam, _txtScale, _txtOffset, _txtMin, _txtMax }) field.HasError = false;
            _grid.EndEdit();

            string param = _txtParam.Text.Trim();
            if (string.IsNullOrWhiteSpace(param))
            {
                Warn("Укажите имя параметра.", _txtParam);
                return;
            }

            string block = _txtBlock.Text.Trim();
            if (string.IsNullOrWhiteSpace(block)) block = CompositeDefaults.BlockName;

            if (!NumberParseHelper.TryParseOrDefault(_txtScale.Text, 1.0, out double scale)) { Warn("Некорректное значение множителя (Scale).", _txtScale); return; }
            if (!NumberParseHelper.TryParseOrDefault(_txtOffset.Text, 0.0, out double offset)) { Warn("Некорректное значение смещения (Offset).", _txtOffset); return; }

            if (!TryParseOptional(_txtMin.Text, out double? min)) { Warn("Некорректное значение минимума.", _txtMin); return; }
            if (!TryParseOptional(_txtMax.Text, out double? max)) { Warn("Некорректное значение максимума.", _txtMax); return; }
            if (min.HasValue && max.HasValue && min.Value > max.Value)
            { Warn("Минимум не может быть больше максимума.", _txtMin); return; }

            var pieces = new List<CompositePiece>();
            string? triggerId = null;

            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow) continue;

                string src = (row.Cells["SourceID"].Value?.ToString() ?? "").Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(src)) continue;

                if (!TryParseInt(row.Cells["Byte"].Value, out int b) || b < 0 || b > 7)
                { WarnCell(row, "Byte", $"Фрагмент '{src}': номер байта должен быть от 0 до 7."); return; }
                if (!TryParseInt(row.Cells["BitStart"].Value, out int bs) || bs < 0 || bs > 7)
                { WarnCell(row, "BitStart", $"Фрагмент '{src}': начальный бит должен быть от 0 до 7."); return; }
                if (!TryParseInt(row.Cells["BitLen"].Value, out int bl) || bl < 1 || bl > 8)
                { WarnCell(row, "BitLen", $"Фрагмент '{src}': длина должна быть от 1 до 8 бит."); return; }
                if (bs + bl > 8)
                { WarnCell(row, "BitLen", $"Фрагмент '{src}': начальный бит + длина не должны превышать 8."); return; }

                pieces.Add(new CompositePiece(src, b, bs, bl));

                if (row.Cells["Trigger"].Value is bool on && on)
                    triggerId = src;
            }

            if (pieces.Count == 0)
            {
                Warn("Добавьте хотя бы один фрагмент с CAN ID.", _grid);
                return;
            }

            Signal = new CompositeSignal
            {
                Block = block,
                Param = param,
                Pieces = pieces,
                Scale = scale,
                Offset = offset,
                Signed = _chkSigned.Checked,
                Unit = _txtUnit.Text.Trim(),
                Min = min,
                Max = max,
                TriggerId = triggerId ?? pieces[^1].SourceId
            };

            DialogResult = DialogResult.OK;
            Close();
        }

        private void Warn(string msg, Control? input = null)
        {
            _validation.Text = msg;
            _validation.Visible = true;
            if (input is ModernTextBox modern) modern.HasError = true;
            input?.Focus();
            if (input is TextBox textBox) textBox.SelectAll();
        }

        private void WarnCell(DataGridViewRow row, string column, string message)
        {
            _grid.CurrentCell = row.Cells[column];
            Warn(message, _grid);
            _grid.BeginEdit(true);
        }

        private static bool TryParseInt(object? value, out int result)
        {
            result = 0;
            string s = value?.ToString()?.Trim() ?? "";
            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
        }

        // Пустое Min/Max — без ограничения; непустой мусор — ошибка ввода, не null.
        private static bool TryParseOptional(string text, out double? value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(text)) return true;
            if (!NumberParseHelper.TryParseDouble(text, out double d)) return false;
            value = d;
            return true;
        }
    }
}
