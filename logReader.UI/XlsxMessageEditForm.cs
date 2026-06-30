using System.Globalization;
using System.Linq;
using logReader;

namespace logReader.UI
{
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
        private readonly Button _btnSave = new();
        private readonly TextBox _txtSearch = new();
        private readonly ComboBox _cmbType = MessageEditFormHelpers.MakeSignalTypeFilterCombo(includeBin: true);
        private readonly ComboBox _cmbOrder = MessageEditFormHelpers.MakeByteOrderFilterCombo();
        private readonly TextBox _txtLenMin = MessageEditFormHelpers.MakeFilterTextBox();
        private readonly TextBox _txtLenMax = MessageEditFormHelpers.MakeFilterTextBox();
        private readonly Label _lblFilterStatus = new();

        public DeviceDefinition Definition { get; private set; }
        private readonly List<DeviceFieldRow> _rows;
        private readonly bool _deviceIdReadOnly;
        private readonly string _baseTitle;
        private bool _dirty;
        private bool _suppressClosePrompt;
        private bool _saved;

        public XlsxMessageEditForm(DeviceDefinition? initial, bool deviceIdReadOnly = false)
        {
            _deviceIdReadOnly = deviceIdReadOnly;
            Definition = initial != null
                ? CloneDefinition(initial)
                : new DeviceDefinition { Extended = true, Dlc = 8 };
            _rows = new List<DeviceFieldRow>(Definition.Rows);

            _baseTitle = initial == null ? "Новая посылка (XLSX)" : "Редактирование посылки (XLSX)";
            Text = _baseTitle;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi;
            MinimumSize = new Size(720, 460);
            ClientSize = new Size(720, 460);

            Icon = Application.OpenForms.OfType<MainForm>().FirstOrDefault()?.Icon;

            BuildLayout();
            WireDirtyTracking();
            LoadFromDefinition(Definition);
            FormClosing += OnFormClosing;

            AcceptButton = _btnSave;
        }

        private void WireDirtyTracking()
        {
            _txtName.TextChanged += (_, _) => MarkDirty();
            if (!_deviceIdReadOnly)
                _txtId.TextChanged += (_, _) => MarkDirty();
            _numDlc.ValueChanged += (_, _) => MarkDirty();
            _rbStandard.CheckedChanged += (_, _) => { if (_rbStandard.Checked) MarkDirty(); };
            _rbExtended.CheckedChanged += (_, _) => { if (_rbExtended.Checked) MarkDirty(); };
        }

        private void MarkDirty()
        {
            if (_dirty) return;
            _dirty = true;
            UpdateTitle();
        }

        private void UpdateTitle() => Text = _dirty ? _baseTitle + " *" : _baseTitle;

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

            var filterPanel = BuildSignalFilterPanel();

            _lblFilterStatus.Dock = DockStyle.Top;
            _lblFilterStatus.AutoSize = false;
            _lblFilterStatus.Height = 22;
            _lblFilterStatus.Padding = new Padding(12, 0, 12, 2);
            _lblFilterStatus.ForeColor = Color.DimGray;

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
            foreach (DataGridViewColumn col in _grid.Columns)
                col.SortMode = DataGridViewColumnSortMode.NotSortable;
            MessageEditFormHelpers.ApplySignalListColumnWeights(_grid);

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
            _btnSave.Text = "Сохранить изменения";
            _btnSave.AutoSize = true;
            _btnSave.Click += (_, _) => SaveChanges();
            bottom.Controls.Add(_btnSave);

            Controls.Add(gridHost);
            Controls.Add(_lblFilterStatus);
            Controls.Add(filterPanel);
            Controls.Add(signalButtons);
            Controls.Add(fmtPanel);
            Controls.Add(top);
            Controls.Add(bottom);
        }

        private Panel BuildSignalFilterPanel()
        {
            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(12, 0, 12, 4),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true
            };

            _txtSearch.Width = 160;
            _txtSearch.Margin = new Padding(3, 3, 8, 0);
            _txtSearch.PlaceholderText = "Имя параметра…";
            _txtLenMin.PlaceholderText = "—";
            _txtLenMax.PlaceholderText = "—";

            void OnFilterChanged(object? s, EventArgs e) => RefreshGrid();
            _txtSearch.TextChanged += OnFilterChanged;
            _cmbType.SelectedIndexChanged += OnFilterChanged;
            _cmbOrder.SelectedIndexChanged += OnFilterChanged;
            _txtLenMin.TextChanged += OnFilterChanged;
            _txtLenMax.TextChanged += OnFilterChanged;

            panel.Controls.Add(MessageEditFormHelpers.MakeLabel("Поиск:"));
            panel.Controls.Add(_txtSearch);
            panel.Controls.Add(MessageEditFormHelpers.MakeLabel("Тип:"));
            panel.Controls.Add(_cmbType);
            panel.Controls.Add(MessageEditFormHelpers.MakeLabel("Длина от:"));
            panel.Controls.Add(_txtLenMin);
            panel.Controls.Add(MessageEditFormHelpers.MakeLabel("до:"));
            panel.Controls.Add(_txtLenMax);
            panel.Controls.Add(MessageEditFormHelpers.MakeLabel("Порядок:"));
            panel.Controls.Add(_cmbOrder);

            return panel;
        }

        private bool TryGetSignalLengthFilters(out int? lenMin, out int? lenMax)
        {
            lenMin = lenMax = null;
            if (!MessageEditFormHelpers.TryParseOptionalInt(_txtLenMin.Text, out lenMin)) return false;
            if (!MessageEditFormHelpers.TryParseOptionalInt(_txtLenMax.Text, out lenMax)) return false;
            return true;
        }


        private bool PassesSignalTypeFilter(DeviceFieldRow r)
        {
            string type = (r.Type ?? "").Trim().ToUpperInvariant();
            return _cmbType.SelectedIndex switch
            {
                1 => type == "NUM" && r.SignedRaw,
                2 => type == "NUM" && !r.SignedRaw,
                3 => type == "BIN",
                _ => true
            };
        }

        private bool PassesByteOrderFilter(DeviceFieldRow r)
        {
            if (string.Equals(r.Type, "BIN", StringComparison.OrdinalIgnoreCase))
                return _cmbOrder.SelectedIndex == 0;

            return _cmbOrder.SelectedIndex switch
            {
                1 => r.IsLittleEndian,
                2 => !r.IsLittleEndian,
                _ => true
            };
        }

        private void LoadFromDefinition(DeviceDefinition d)
        {
            _txtName.Text = d.MessageName;
            _txtId.Text = d.DeviceId.Trim();
            _numDlc.Value = Math.Clamp(d.Dlc, 1, 8);
            _rbExtended.Checked = d.Extended;
            _rbStandard.Checked = !d.Extended;
            RefreshGrid();
            _dirty = false;
            UpdateTitle();
        }

        private void RefreshGrid()
        {
            _grid.Rows.Clear();
            string query = _txtSearch.Text.Trim();

            if (!TryGetSignalLengthFilters(out int? lenMin, out int? lenMax))
            {
                _lblFilterStatus.Text = "Фильтр: неверное число в «Длина»";
                return;
            }

            int shown = 0;
            for (int i = 0; i < _rows.Count; i++)
            {
                var r = _rows[i];
                if (!MessageEditFormHelpers.TextMatchesQuery(r.Header, query)) continue;
                if (!PassesSignalTypeFilter(r)) continue;
                if (!PassesByteOrderFilter(r)) continue;
                if (!MessageEditFormHelpers.InOptionalRange(r.Length, lenMin, lenMax)) continue;

                string type = (r.Type ?? "").Trim().ToUpperInvariant();
                int rowIdx;
                if (type == "BIN")
                {
                    rowIdx = _grid.Rows.Add(
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
                    rowIdx = _grid.Rows.Add(
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

                _grid.Rows[rowIdx].Tag = i;
                shown++;
            }

            _lblFilterStatus.Text = shown == _rows.Count
                ? $"Показано: {shown}"
                : $"Показано: {shown} из {_rows.Count}";
        }

        private int SelectedIndex() => MessageEditFormHelpers.SelectedSourceIndex(_grid);

        private void SelectRowBySourceIndex(int sourceIndex)
        {
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.Tag is int t && t == sourceIndex)
                {
                    row.Selected = true;
                    _grid.CurrentCell = row.Cells[0];
                    return;
                }
            }
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
            MarkDirty();
            RefreshGrid();
            SelectRowBySourceIndex(_rows.Count - 1);
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
            MarkDirty();
            RefreshGrid();
            SelectRowBySourceIndex(idx);
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
            MarkDirty();
            RefreshGrid();
        }

        private void SaveChanges()
        {
            if (!TryCommitChanges())
                return;

            _dirty = false;
            _saved = true;
            UpdateTitle();
        }

        private void OnFormClosing(object? sender, FormClosingEventArgs e)
        {
            string description = string.IsNullOrWhiteSpace(_txtName.Text)
                ? _baseTitle
                : $"Посылка: {_txtName.Text.Trim()}";

            MessageEditFormHelpers.ResolveFormCloseWithDirty(
                this,
                e,
                _dirty,
                _suppressClosePrompt,
                description,
                () =>
                {
                    if (!TryCommitChanges())
                        return false;
                    _suppressClosePrompt = true;
                    _dirty = false;
                    _saved = true;
                    return true;
                });

            if (e.Cancel)
                return;

            DialogResult = _saved ? DialogResult.OK : DialogResult.Cancel;
        }

        private bool TryCommitChanges()
        {
            string name = _txtName.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show(this, "Введите имя посылки (MessageName).", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtName.Focus();
                return false;
            }

            bool isExtended = _rbExtended.Checked;
            if (!MessageEditFormHelpers.TryParseHexId(_txtId.Text, isExtended, out uint id, out string idError))
            {
                MessageBox.Show(this, idError, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtId.Focus();
                return false;
            }

            if (_rows.Count == 0)
            {
                var cont = MessageBox.Show(
                    this,
                    "Нет ни одного параметра. Продолжить?",
                    "Подтверждение",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (cont != DialogResult.Yes) return false;
            }

            int dlc = (int)_numDlc.Value;
            foreach (var r in _rows)
            {
                if (string.Equals(r.Type, "NUM", StringComparison.OrdinalIgnoreCase))
                {
                    if (!BitMath.SignalFitsInDlc(r.StartBit, r.Length, r.IsLittleEndian, dlc * 8))
                    {
                        MessageBox.Show(
                            this,
                            $"Параметр '{r.Header}' выходит за пределы DLC={dlc} байт.",
                            "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return false;
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

            return true;
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
