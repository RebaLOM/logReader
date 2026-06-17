using System.Globalization;
using System.Linq;
using logReader;

namespace logReader.UI
{
    internal sealed class CompositeParamEditForm : Form
    {
        private readonly TextBox _txtBlock = new();
        private readonly TextBox _txtParam = new();
        private readonly TextBox _txtScale = new();
        private readonly TextBox _txtOffset = new();
        private readonly CheckBox _chkSigned = new();
        private readonly TextBox _txtUnit = new();
        private readonly TextBox _txtMin = new();
        private readonly TextBox _txtMax = new();

        private readonly DataGridView _grid = new();

        public CompositeSignal Signal { get; private set; } = new();

        public CompositeParamEditForm(CompositeSignal? existing)
        {
            Text = existing == null ? "Новый составной параметр" : "Изменить составной параметр";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(640, 560);
            ClientSize = new Size(640, 560);
            Icon = Application.OpenForms.OfType<MainForm>().FirstOrDefault()?.Icon;

            BuildLayout();

            if (existing != null)
                LoadFrom(existing);
            else
            {
                _txtBlock.Text = CompositeDefaults.BlockName;
                _txtScale.Text = "1";
                _txtOffset.Text = "0";
            }
        }

        private void BuildLayout()
        {
            var top = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 4,
                AutoSize = true,
                Padding = new Padding(12, 12, 12, 6)
            };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            top.Controls.Add(new Label { Text = "Блок", Anchor = AnchorStyles.Left, AutoSize = true, Margin = new Padding(3, 8, 3, 3) }, 0, 0);
            _txtBlock.Dock = DockStyle.Fill;
            top.Controls.Add(_txtBlock, 1, 0);

            top.Controls.Add(new Label { Text = "Параметр", Anchor = AnchorStyles.Left, AutoSize = true, Margin = new Padding(3, 8, 3, 3) }, 2, 0);
            _txtParam.Dock = DockStyle.Fill;
            top.Controls.Add(_txtParam, 3, 0);

            top.Controls.Add(new Label { Text = "Scale", Anchor = AnchorStyles.Left, AutoSize = true, Margin = new Padding(3, 8, 3, 3) }, 0, 1);
            _txtScale.Dock = DockStyle.Fill;
            top.Controls.Add(_txtScale, 1, 1);

            top.Controls.Add(new Label { Text = "Offset", Anchor = AnchorStyles.Left, AutoSize = true, Margin = new Padding(3, 8, 3, 3) }, 2, 1);
            _txtOffset.Dock = DockStyle.Fill;
            top.Controls.Add(_txtOffset, 3, 1);

            top.Controls.Add(new Label { Text = "Unit", Anchor = AnchorStyles.Left, AutoSize = true, Margin = new Padding(3, 8, 3, 3) }, 0, 2);
            _txtUnit.Dock = DockStyle.Fill;
            top.Controls.Add(_txtUnit, 1, 2);

            _chkSigned.Text = "Знаковый (signed)";
            _chkSigned.AutoSize = true;
            _chkSigned.Anchor = AnchorStyles.Left;
            _chkSigned.Margin = new Padding(3, 6, 3, 3);
            top.Controls.Add(_chkSigned, 3, 2);

            top.Controls.Add(new Label { Text = "Min", Anchor = AnchorStyles.Left, AutoSize = true, Margin = new Padding(3, 8, 3, 3) }, 0, 3);
            _txtMin.Dock = DockStyle.Fill;
            top.Controls.Add(_txtMin, 1, 3);

            top.Controls.Add(new Label { Text = "Max", Anchor = AnchorStyles.Left, AutoSize = true, Margin = new Padding(3, 8, 3, 3) }, 2, 3);
            _txtMax.Dock = DockStyle.Fill;
            top.Controls.Add(_txtMax, 3, 3);

            var info = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = 36,
                Padding = new Padding(12, 4, 12, 0),
                ForeColor = Color.DimGray,
                Text = "Куски идут от старших бит к младшим (строка 1 = старшие). "
                     + "Триггер = посылка, по приходу которой формируется значение."
            };

            _grid.Dock = DockStyle.Fill;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToResizeRows = false;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.EditMode = DataGridViewEditMode.EditOnEnter;

            var colSource = new DataGridViewTextBoxColumn { Name = "SourceID", HeaderText = "SourceID (hex)", FillWeight = 40 };
            var colByte = new DataGridViewTextBoxColumn { Name = "Byte", HeaderText = "Byte (0-7)", FillWeight = 15 };
            var colBitStart = new DataGridViewTextBoxColumn { Name = "BitStart", HeaderText = "BitStart (0-7)", FillWeight = 17 };
            var colBitLen = new DataGridViewTextBoxColumn { Name = "BitLen", HeaderText = "BitLen (1-8)", FillWeight = 16 };
            var colTrigger = new DataGridViewCheckBoxColumn { Name = "Trigger", HeaderText = "Триггер", FillWeight = 12 };
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

            var gridHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 0, 12, 6) };
            gridHost.Controls.Add(_grid);

            var pieceBtns = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(12, 0, 12, 0),
                WrapContents = false
            };
            var btnAddPiece = new Button { Text = "Добавить кусок", AutoSize = true };
            btnAddPiece.Click += (_, _) => _grid.Rows.Add("", "0", "0", "8", false);
            var btnDelPiece = new Button { Text = "Удалить кусок", AutoSize = true };
            btnDelPiece.Click += (_, _) => { if (_grid.CurrentRow != null) _grid.Rows.Remove(_grid.CurrentRow); };
            var btnUp = new Button { Text = "Вверх", AutoSize = true };
            btnUp.Click += (_, _) => MoveRow(-1);
            var btnDown = new Button { Text = "Вниз", AutoSize = true };
            btnDown.Click += (_, _) => MoveRow(+1);
            pieceBtns.Controls.AddRange(new Control[] { btnAddPiece, btnDelPiece, btnUp, btnDown });

            var bottom = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(12),
                Height = 54,
                WrapContents = false
            };
            var btnOk = new Button { Text = "OK", Width = 100, DialogResult = DialogResult.None };
            btnOk.Click += BtnOk_Click;
            var btnCancel = new Button { Text = "Отмена", Width = 100, DialogResult = DialogResult.Cancel };
            bottom.Controls.Add(btnOk);
            bottom.Controls.Add(btnCancel);

            Controls.Add(gridHost);
            Controls.Add(pieceBtns);
            Controls.Add(info);
            Controls.Add(top);
            Controls.Add(bottom);

            AcceptButton = btnOk;
            CancelButton = btnCancel;
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
            _grid.EndEdit();

            string param = _txtParam.Text.Trim();
            if (string.IsNullOrWhiteSpace(param))
            {
                Warn("Укажите имя параметра.");
                return;
            }

            string block = _txtBlock.Text.Trim();
            if (string.IsNullOrWhiteSpace(block)) block = CompositeDefaults.BlockName;

            if (!NumberParseHelper.TryParseOrDefault(_txtScale.Text, 1.0, out double scale)) { Warn("Некорректное значение Scale."); return; }
            if (!NumberParseHelper.TryParseOrDefault(_txtOffset.Text, 0.0, out double offset)) { Warn("Некорректное значение Offset."); return; }

            if (!TryParseOptional(_txtMin.Text, out double? min)) { Warn("Некорректное значение Min."); return; }
            if (!TryParseOptional(_txtMax.Text, out double? max)) { Warn("Некорректное значение Max."); return; }
            if (min.HasValue && max.HasValue && min.Value > max.Value)
            { Warn("Min не может быть больше Max."); return; }

            var pieces = new List<CompositePiece>();
            string? triggerId = null;

            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow) continue;

                string src = (row.Cells["SourceID"].Value?.ToString() ?? "").Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(src)) continue;

                if (!TryParseInt(row.Cells["Byte"].Value, out int b) || b < 0 || b > 7)
                { Warn($"Кусок '{src}': Byte должен быть 0..7."); return; }
                if (!TryParseInt(row.Cells["BitStart"].Value, out int bs) || bs < 0 || bs > 7)
                { Warn($"Кусок '{src}': BitStart должен быть 0..7."); return; }
                if (!TryParseInt(row.Cells["BitLen"].Value, out int bl) || bl < 1 || bl > 8)
                { Warn($"Кусок '{src}': BitLen должен быть 1..8."); return; }
                if (bs + bl > 8)
                { Warn($"Кусок '{src}': BitStart+BitLen не должны превышать 8."); return; }

                pieces.Add(new CompositePiece(src, b, bs, bl));

                if (row.Cells["Trigger"].Value is bool on && on)
                    triggerId = src;
            }

            if (pieces.Count == 0)
            {
                Warn("Добавьте хотя бы один кусок.");
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

        private void Warn(string msg)
            => MessageBox.Show(this, msg, "Проверка", MessageBoxButtons.OK, MessageBoxIcon.Warning);

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
