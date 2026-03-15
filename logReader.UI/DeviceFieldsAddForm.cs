using System.Globalization;
using System.Linq;

namespace logReader.UI
{
    internal sealed class DeviceFieldsAddForm : Form
    {
        private const int MaxRows = 64;

        private static readonly Color ReadOnlyBackColor = Color.FromArgb(210, 210, 210);
        private static readonly Color ReadOnlySelectionBackColor = Color.FromArgb(195, 195, 195);
        private static readonly Color ReadOnlyForeColor = Color.DimGray;

        private const string ColHeader = "colHeader";
        private const string ColLowByte = "colLowByte";
        private const string ColHighByte = "colHighByte";
        private const string ColType = "colType";
        private const string ColScale = "colScale";
        private const string ColOffset = "colOffset";
        private const string ColStartBit = "colStartBit";
        private const string ColLenghtBit = "colLenghtBit";

        private readonly TextBox _textDeviceId = new TextBox();
        private readonly NumericUpDown _numCount = new NumericUpDown();
        private readonly DataGridView _grid = new DataGridView();
        private readonly Button _btnOk = new Button();
        private readonly Button _btnCancel = new Button();

        public string DeviceId => _textDeviceId.Text.Trim();
        public IReadOnlyList<logReader.DeviceFieldRow> Rows { get; private set; } = Array.Empty<logReader.DeviceFieldRow>();

        public DeviceFieldsAddForm()
        {
            Text = "Добавить устройство";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(860, 520);

            Icon = Application.OpenForms.OfType<MainForm>().FirstOrDefault()?.Icon;

            BuildLayout();
            BuildGrid();
            SyncRowCount(1);
        }

        private void BuildLayout()
        {
            var topPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 4,
                Padding = new Padding(12, 12, 12, 6),
            };
            topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            var lblId = new Label
            {
                Text = "DeviceID",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                TextAlign = ContentAlignment.MiddleLeft
            };

            _textDeviceId.Dock = DockStyle.Fill;

            var lblCount = new Label
            {
                Text = "Количество параметров",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                TextAlign = ContentAlignment.MiddleLeft
            };

            _numCount.Minimum = 1;
            _numCount.Maximum = MaxRows;
            _numCount.Value = 1;
            _numCount.Dock = DockStyle.Left;
            _numCount.Width = 90;
            _numCount.ValueChanged += (_, _) => SyncRowCount((int)_numCount.Value);

            topPanel.Controls.Add(lblId, 0, 0);
            topPanel.Controls.Add(_textDeviceId, 1, 0);
            topPanel.Controls.Add(lblCount, 2, 0);
            topPanel.Controls.Add(_numCount, 3, 0);

            var bottomPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(12),
                Height = 54,
                WrapContents = false
            };

            _btnOk.Text = "OK";
            _btnOk.Width = 100;
            _btnOk.Click += (_, _) => OnOk();

            _btnCancel.Text = "Отмена";
            _btnCancel.Width = 100;
            _btnCancel.DialogResult = DialogResult.Cancel;

            bottomPanel.Controls.Add(_btnOk);
            bottomPanel.Controls.Add(_btnCancel);

            _grid.Dock = DockStyle.Fill;

            AcceptButton = _btnOk;
            CancelButton = _btnCancel;

            Controls.Add(_grid);
            Controls.Add(bottomPanel);
            Controls.Add(topPanel);
        }

        private void BuildGrid()
        {
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToResizeRows = false;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
            _grid.MultiSelect = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.EditMode = DataGridViewEditMode.EditOnEnter;
            _grid.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText;
            _grid.DataError += (_, e) => e.ThrowException = false;
            _grid.DefaultCellStyle.BackColor = Color.White;

            _grid.CellFormatting += (_, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                var cell = _grid.Rows[e.RowIndex].Cells[e.ColumnIndex];

                if (cell.ReadOnly)
                {
                    e.CellStyle.BackColor = ReadOnlyBackColor;
                    e.CellStyle.ForeColor = ReadOnlyForeColor;
                    e.CellStyle.SelectionBackColor = ReadOnlySelectionBackColor;
                    e.CellStyle.SelectionForeColor = ReadOnlyForeColor;
                }
                else
                {
                    e.CellStyle.BackColor = Color.White;
                    e.CellStyle.ForeColor = Color.Black;
                }
            };

            var list0to7 = new List<string> { "" };
            for (int i = 0; i <= 7; i++) list0to7.Add(i.ToString(CultureInfo.InvariantCulture));

            var list1to8 = new List<string> { "" };
            for (int i = 1; i <= 8; i++) list1to8.Add(i.ToString(CultureInfo.InvariantCulture));

            var typeList = new List<string> { "", "NUM", "BIN" };

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = ColHeader,
                HeaderText = "Header",
                FillWeight = 30
            });

            _grid.Columns.Add(new DataGridViewComboBoxColumn
            {
                Name = ColLowByte,
                HeaderText = "LowByte",
                DataSource = list0to7,
                FlatStyle = FlatStyle.Flat,
                FillWeight = 9
            });

            _grid.Columns.Add(new DataGridViewComboBoxColumn
            {
                Name = ColHighByte,
                HeaderText = "HighByte",
                DataSource = list0to7,
                FlatStyle = FlatStyle.Flat,
                FillWeight = 9
            });

            _grid.Columns.Add(new DataGridViewComboBoxColumn
            {
                Name = ColScale,
                HeaderText = "Scale",
                FillWeight = 10
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = ColOffset,
                HeaderText = "Offset",
                FillWeight = 10
            });

            _grid.Columns.Add(new DataGridViewComboBoxColumn
            {
                Name = ColType,
                HeaderText = "Type",
                DataSource = typeList,
                FlatStyle = FlatStyle.Flat,
                FillWeight = 9
            });

            _grid.Columns.Add(new DataGridViewComboBoxColumn
            {
                Name = ColStartBit,
                HeaderText = "StartBit",
                DataSource = list0to7,
                FlatStyle = FlatStyle.Flat,
                FillWeight = 9
            });

            _grid.Columns.Add(new DataGridViewComboBoxColumn
            {
                Name = ColLenghtBit,
                HeaderText = "LenghtBit",
                DataSource = list1to8,
                FlatStyle = FlatStyle.Flat,
                FillWeight = 9
            });

            _grid.CurrentCellDirtyStateChanged += (_, _) =>
            {
                if (_grid.IsCurrentCellDirty)
                    _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };

            _grid.CellValueChanged += (_, e) =>
            {
                if (e.RowIndex < 0) return;
                if (_grid.Columns[e.ColumnIndex].Name == ColType)
                    ApplyTypeRules(e.RowIndex);
            };
        }

        private void SyncRowCount(int count)
        {
            count = Math.Clamp(count, 1, MaxRows);

            while (_grid.Rows.Count < count)
            {
                int idx = _grid.Rows.Add();
                var r = _grid.Rows[idx];
                r.Cells[ColHeader].Value = "";
                r.Cells[ColLowByte].Value = "";
                r.Cells[ColHighByte].Value = "";
                r.Cells[ColScale].Value = "";
                r.Cells[ColOffset].Value = "";
                r.Cells[ColStartBit].Value = "";
                r.Cells[ColLenghtBit].Value = "";
                r.Cells[ColType].Value = "NUM";
                ApplyTypeRules(idx);
            }

            while (_grid.Rows.Count > count)
                _grid.Rows.RemoveAt(_grid.Rows.Count - 1);
        }

        private void ApplyTypeRules(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _grid.Rows.Count) return;

            var row = _grid.Rows[rowIndex];
            string type = Convert.ToString(row.Cells[ColType].Value)?.Trim().ToUpperInvariant() ?? "";

            var scaleCell = row.Cells[ColScale];
            var offsetCell = row.Cells[ColOffset];
            var startCell = row.Cells[ColStartBit];
            var lenCell = row.Cells[ColLenghtBit];

            if (type == "BIN")
            {
                scaleCell.ReadOnly = true;
                offsetCell.ReadOnly = true;
                scaleCell.Value = "";
                offsetCell.Value = "";

                startCell.ReadOnly = false;
                lenCell.ReadOnly = false;
            }
            else // NUM или пусто
            {
                startCell.ReadOnly = true;
                lenCell.ReadOnly = true;
                startCell.Value = "";
                lenCell.Value = "";

                scaleCell.ReadOnly = false;
                offsetCell.ReadOnly = false;
            }

            ApplyReadOnlyStyle(scaleCell);
            ApplyReadOnlyStyle(offsetCell);
            ApplyReadOnlyStyle(startCell);
            ApplyReadOnlyStyle(lenCell);

            _grid.InvalidateRow(rowIndex);
        }

        private static void ApplyReadOnlyStyle(DataGridViewCell cell)
        {
            cell.Style.BackColor = cell.ReadOnly ? ReadOnlyBackColor : Color.White;
            cell.Style.ForeColor = cell.ReadOnly ? ReadOnlyForeColor : Color.Black;
            cell.Style.SelectionBackColor = cell.ReadOnly ? ReadOnlySelectionBackColor : cell.Style.SelectionBackColor;
            cell.Style.SelectionForeColor = cell.ReadOnly ? ReadOnlyForeColor : cell.Style.SelectionForeColor;

            if (cell is DataGridViewComboBoxCell cb)
            {
                cb.DisplayStyle = cell.ReadOnly
                    ? DataGridViewComboBoxDisplayStyle.Nothing
                    : DataGridViewComboBoxDisplayStyle.DropDownButton;
            }
        }

        private void OnOk()
        {
            _grid.EndEdit();

            string deviceId = DeviceId;
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                MessageBox.Show(this, "Введите DeviceID.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _textDeviceId.Focus();
                return;
            }

            var result = new List<logReader.DeviceFieldRow>();

            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                var row = _grid.Rows[i];

                string header = Convert.ToString(row.Cells[ColHeader].Value)?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(header))
                {
                    FailRow(i, ColHeader, "Заполните Header.");
                    return;
                }

                if (!TryParseRequiredInt(row.Cells[ColLowByte].Value, out int lowByte))
                {
                    FailRow(i, ColLowByte, "Выберите LowByte (0..7).");
                    return;
                }

                int? highByte = TryParseOptionalInt(row.Cells[ColHighByte].Value);

                string type = Convert.ToString(row.Cells[ColType].Value)?.Trim().ToUpperInvariant() ?? "";
                if (type != "NUM" && type != "BIN")
                {
                    FailRow(i, ColType, "Выберите Type (NUM или BIN).");
                    return;
                }

                double? scale = null;
                double? offset = null;
                int? startBit = null;
                int? lenghtBit = null;

                if (type == "NUM")
                {
                    if (!TryParseOptionalDouble(Convert.ToString(row.Cells[ColScale].Value), out scale))
                    {
                        FailRow(i, ColScale, "Scale: неверный формат числа.");
                        return;
                    }
                    if (!TryParseOptionalDouble(Convert.ToString(row.Cells[ColOffset].Value), out offset))
                    {
                        FailRow(i, ColOffset, "Offset: неверный формат числа.");
                        return;
                    }
                }
                else // BIN
                {
                    if (!TryParseRequiredInt(row.Cells[ColStartBit].Value, out int s))
                    {
                        FailRow(i, ColStartBit, "Для BIN выберите StartBit (0..7).");
                        return;
                    }
                    if (!TryParseRequiredInt(row.Cells[ColLenghtBit].Value, out int l))
                    {
                        FailRow(i, ColLenghtBit, "Для BIN выберите LenghtBit (1..8).");
                        return;
                    }

                    if (s + l > 8)
                    {
                        FailRow(i, ColLenghtBit, "Для BIN сумма StartBit + LenghtBit не должна превышать 8.");
                        return;
                    }

                    startBit = s;
                    lenghtBit = l;
                }

                result.Add(new logReader.DeviceFieldRow(
                    Header: header,
                    LowByte: lowByte,
                    HighByte: highByte,
                    Type: type,
                    Scale: scale,
                    Offset: offset,
                    StartBit: startBit,
                    LenghtBit: lenghtBit));
            }

            Rows = result;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void FailRow(int rowIndex, string columnName, string message)
        {
            MessageBox.Show(this, $"Строка {rowIndex + 1}: {message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _grid.ClearSelection();
            var cell = _grid.Rows[rowIndex].Cells[columnName];
            _grid.CurrentCell = cell;
            _grid.BeginEdit(true);
        }

        private static bool TryParseRequiredInt(object? value, out int result)
        {
            result = 0;
            string s = Convert.ToString(value)?.Trim() ?? "";
            return s.Length > 0 && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
        }

        private static int? TryParseOptionalInt(object? value)
        {
            string s = Convert.ToString(value)?.Trim() ?? "";
            if (s.Length == 0) return null;
            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int r) ? r : null;
        }

        private static bool TryParseOptionalDouble(string? text, out double? value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(text)) return true;

            string s = text.Trim();
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double inv))
            {
                value = inv;
                return true;
            }

            if (double.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out double cur))
            {
                value = cur;
                return true;
            }

            s = s.Replace(',', '.');
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double repl))
            {
                value = repl;
                return true;
            }

            return false;
        }
    }
}
