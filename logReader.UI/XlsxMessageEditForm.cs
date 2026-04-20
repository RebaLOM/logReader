using System.Globalization;
using System.Linq;
using logReader;

namespace logReader.UI
{
    /// <summary>Редактирование одной посылки в XLSX — тот же каркас, что <see cref="DbcMessageEditForm"/>.</summary>
    internal sealed class XlsxMessageEditForm : Form
    {
        private readonly TextBox _txtName = new();
        private readonly TextBox _txtId = new();
        private readonly NumericUpDown _numDlc = new();
        private readonly RadioButton _rbStandard = new();
        private readonly RadioButton _rbExtended = new();
        private readonly DataGridView _grid = new();
        private readonly Button _btnAdd = new();
        private readonly Button _btnEdit = new();
        private readonly Button _btnDelete = new();
        private readonly Button _btnOk = new();
        private readonly Button _btnCancel = new();

        public DeviceDefinition Definition { get; private set; }
        private readonly List<DeviceFieldRow> _rows;
        private readonly bool _deviceIdReadOnly;

        public XlsxMessageEditForm(DeviceDefinition? initial, bool deviceIdReadOnly = false)
        {
            _deviceIdReadOnly = deviceIdReadOnly;
            Definition = initial != null
                ? CloneDefinition(initial)
                : new DeviceDefinition { Extended = true, Dlc = 8 };
            _rows = new List<DeviceFieldRow>(Definition.Rows);

            Text = initial == null ? "Новая посылка (XLSX)" : "Редактирование посылки (XLSX)";
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi;
            MinimumSize = new Size(720, 460);
            ClientSize = new Size(720, 460);

            Icon = Application.OpenForms.OfType<MainForm>().FirstOrDefault()?.Icon;

            BuildLayout();
            LoadFromDefinition(Definition);

            AcceptButton = _btnOk;
            CancelButton = _btnCancel;
        }

        private void BuildLayout()
        {
            var top = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 6,
                Padding = new Padding(12, 12, 12, 6),
            };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));

            top.Controls.Add(MessageEditFormHelpers.MakeLabel("Имя"), 0, 0);
            _txtName.Dock = DockStyle.Fill;
            top.Controls.Add(_txtName, 1, 0);

            top.Controls.Add(MessageEditFormHelpers.MakeLabel("ID (hex)"), 2, 0);
            _txtId.Dock = DockStyle.Fill;
            _txtId.ReadOnly = _deviceIdReadOnly;
            top.Controls.Add(_txtId, 3, 0);

            top.Controls.Add(MessageEditFormHelpers.MakeLabel("DLC"), 4, 0);
            _numDlc.Minimum = 1;
            _numDlc.Maximum = 8;
            _numDlc.Value = 8;
            _numDlc.Dock = DockStyle.Left;
            _numDlc.Width = 60;
            top.Controls.Add(_numDlc, 5, 0);

            var fmtPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(12, 0, 12, 6),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };
            fmtPanel.Controls.Add(MessageEditFormHelpers.MakeLabel("Формат:"));
            _rbStandard.Text = "Standard (11-bit)";
            _rbStandard.AutoSize = true;
            _rbStandard.Margin = new Padding(8, 3, 8, 0);
            _rbExtended.Text = "Extended (29-bit)";
            _rbExtended.AutoSize = true;
            _rbExtended.Margin = new Padding(8, 3, 8, 0);
            fmtPanel.Controls.Add(_rbStandard);
            fmtPanel.Controls.Add(_rbExtended);

            var signalButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(12, 0, 12, 6),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };
            _btnAdd.Text = "Добавить сигнал";
            _btnAdd.AutoSize = true;
            _btnAdd.Click += (_, _) => AddSignal();
            _btnEdit.Text = "Изменить";
            _btnEdit.AutoSize = true;
            _btnEdit.Click += (_, _) => EditSignal();
            _btnDelete.Text = "Удалить";
            _btnDelete.AutoSize = true;
            _btnDelete.Click += (_, _) => DeleteSignal();
            signalButtons.Controls.AddRange(new Control[] { _btnAdd, _btnEdit, _btnDelete });

            _grid.Dock = DockStyle.Fill;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToResizeRows = false;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.ReadOnly = true;
            _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) EditSignal(); };
            _grid.Columns.Add("Name", "Name");
            _grid.Columns.Add("ByteIdx", "Byte Idx");
            _grid.Columns.Add("StartBit", "Start Bit");
            _grid.Columns.Add("Length", "Length");
            _grid.Columns.Add("Type", "Type");
            _grid.Columns.Add("Factor", "Factor");
            _grid.Columns.Add("Offset", "Offset");
            _grid.Columns.Add("Unit", "Unit");
            _grid.Columns.Add("Order", "Order");
            _grid.Columns["Name"]!.FillWeight = 25;
            foreach (DataGridViewColumn col in _grid.Columns)
                col.SortMode = DataGridViewColumnSortMode.NotSortable;

            var gridHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 0, 12, 6) };
            gridHost.Controls.Add(_grid);

            var bottom = new FlowLayoutPanel
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
            bottom.Controls.Add(_btnOk);
            bottom.Controls.Add(_btnCancel);

            Controls.Add(gridHost);
            Controls.Add(signalButtons);
            Controls.Add(fmtPanel);
            Controls.Add(top);
            Controls.Add(bottom);
        }

        private void LoadFromDefinition(DeviceDefinition d)
        {
            _txtName.Text = d.MessageName;
            _txtId.Text = d.DeviceId.Trim();
            _numDlc.Value = Math.Clamp(d.Dlc, 1, 8);
            _rbExtended.Checked = d.Extended;
            _rbStandard.Checked = !d.Extended;
            RefreshGrid();
        }

        private void RefreshGrid()
        {
            _grid.Rows.Clear();
            foreach (var r in _rows)
            {
                string type = (r.Type ?? "").Trim().ToUpperInvariant();
                if (type == "BIN")
                {
                    _grid.Rows.Add(
                        r.Header,
                        r.StartBit.ToString(CultureInfo.InvariantCulture),
                        (r.BitStart ?? 0).ToString(CultureInfo.InvariantCulture),
                        r.Length.ToString(CultureInfo.InvariantCulture),
                        "BIN",
                        "",
                        "",
                        "",
                        "");
                }
                else
                {
                    int byteIdx = r.Length > 0 ? r.StartBit / 8 : 0;
                    int bitInByte = r.StartBit % 8;
                    _grid.Rows.Add(
                        r.Header,
                        byteIdx.ToString(CultureInfo.InvariantCulture),
                        bitInByte.ToString(CultureInfo.InvariantCulture),
                        r.Length.ToString(CultureInfo.InvariantCulture),
                        r.SignedRaw ? "int" : "unsigned",
                        r.Scale.ToString(CultureInfo.InvariantCulture),
                        r.Offset.ToString(CultureInfo.InvariantCulture),
                        r.Unit ?? "",
                        r.IsLittleEndian ? "Intel" : "Motorola");
                }
            }
        }

        private int SelectedIndex()
        {
            if (_grid.CurrentRow == null) return -1;
            return _grid.CurrentRow.Index;
        }

        private void AddSignal()
        {
            using var dlg = new DeviceFieldRowEditForm(null, (int)_numDlc.Value, _rows.Count);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            if (_rows.Any(x => x.Header.Equals(dlg.Row.Header, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(this, "Параметр с таким именем уже существует.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _rows.Add(dlg.Row);
            RefreshGrid();
        }

        private void EditSignal()
        {
            int idx = SelectedIndex();
            if (idx < 0 || idx >= _rows.Count) return;

            using var dlg = new DeviceFieldRowEditForm(_rows[idx], (int)_numDlc.Value, idx);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            if (_rows
                .Where((_, i) => i != idx)
                .Any(x => x.Header.Equals(dlg.Row.Header, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(this, "Параметр с таким именем уже существует.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _rows[idx] = dlg.Row;
            RefreshGrid();
            if (idx < _grid.Rows.Count) _grid.Rows[idx].Selected = true;
        }

        private void DeleteSignal()
        {
            int idx = SelectedIndex();
            if (idx < 0 || idx >= _rows.Count) return;

            var confirm = MessageBox.Show(
                this,
                $"Удалить параметр '{_rows[idx].Header}'?",
                "Подтверждение",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            _rows.RemoveAt(idx);
            RefreshGrid();
        }

        private void OnOk()
        {
            string name = _txtName.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show(this, "Введите имя посылки (MessageName).", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtName.Focus();
                return;
            }

            bool isExtended = _rbExtended.Checked;
            if (!MessageEditFormHelpers.TryParseHexId(_txtId.Text, isExtended, out uint id, out string idError))
            {
                MessageBox.Show(this, idError, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtId.Focus();
                return;
            }

            if (_rows.Count == 0)
            {
                var cont = MessageBox.Show(
                    this,
                    "Нет ни одного параметра. Продолжить?",
                    "Подтверждение",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (cont != DialogResult.Yes) return;
            }

            int dlc = (int)_numDlc.Value;
            foreach (var r in _rows)
            {
                if (string.Equals(r.Type, "NUM", StringComparison.OrdinalIgnoreCase))
                {
                    if (r.StartBit + r.Length > dlc * 8)
                    {
                        MessageBox.Show(
                            this,
                            $"Параметр '{r.Header}' выходит за пределы DLC={dlc} байт.",
                            "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }
            }

            string deviceId = id.ToString("X", CultureInfo.InvariantCulture);
            var rows = new List<DeviceFieldRow>();
            for (int i = 0; i < _rows.Count; i++)
            {
                var r = _rows[i];
                rows.Add(r with { FieldIndex = i });
            }

            Definition = new DeviceDefinition
            {
                DeviceId = deviceId,
                MessageName = name,
                Extended = isExtended,
                Dlc = dlc,
                Rows = rows
            };

            DialogResult = DialogResult.OK;
            Close();
        }

        private static DeviceDefinition CloneDefinition(DeviceDefinition d)
        {
            var copy = new DeviceDefinition
            {
                DeviceId = d.DeviceId,
                MessageName = d.MessageName,
                Extended = d.Extended,
                Dlc = d.Dlc
            };
            foreach (var r in d.Rows)
                copy.Rows.Add(r);
            return copy;
        }
    }
}
