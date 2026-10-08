using System.Globalization;
using System.Linq;
using logReader;
using logReader.UI.Controls;
using logReader.UI.Icons;
using logReader.UI.Theme;

namespace logReader.UI
{
    internal sealed class XlsxMessageEditForm : Form
    {
        private readonly TextBox _txtName = new ModernTextBox();
        private readonly TextBox _txtId = new ModernTextBox();
        private readonly NumericUpDown _numDlc = new ModernNumericUpDown();
        private readonly RadioButton _rbStandard = new();
        private readonly RadioButton _rbExtended = new();
        private readonly DataGridView _grid = new();
        private readonly Button _btnAdd = new ModernButton { Icon = IconKind.Plus, Variant = ButtonVariant.Primary };
        private readonly Button _btnEdit = new ModernButton { Icon = IconKind.Edit, Variant = ButtonVariant.Secondary };
        private readonly Button _btnDelete = new ModernButton { Icon = IconKind.Trash, Variant = ButtonVariant.Danger };
        private readonly Button _btnSave = new ModernButton { Icon = IconKind.Save, Variant = ButtonVariant.Primary };
        private readonly Button _btnClose = new ModernButton { Icon = IconKind.Close, Variant = ButtonVariant.Ghost };
        private readonly InlineNotice _notice = new();
        private readonly TextBox _txtSearch = new ModernTextBox();
        private readonly ComboBox _cmbType = MessageEditFormHelpers.MakeSignalTypeFilterCombo(includeBin: true);
        private readonly ComboBox _cmbOrder = MessageEditFormHelpers.MakeByteOrderFilterCombo();
        private readonly TextBox _txtLenMin = MessageEditFormHelpers.MakeFilterTextBox();
        private readonly TextBox _txtLenMax = MessageEditFormHelpers.MakeFilterTextBox();
        private readonly Label _lblFilterStatus = new();
        private readonly CanPayloadGridControl _payloadGrid = new();

        private IReadOnlyDictionary<string, Color> _signalColors =
            new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
        private string? _highlightedSignalName;
        private bool _syncingPayloadSelection;

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
            SuspendLayout();
            Text = _baseTitle;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            MinimumSize = new Size(960, 600);
            ClientSize = new Size(1280, 860);

            Icon = Application.OpenForms.OfType<MainForm>().FirstOrDefault()?.Icon;

            BuildLayout();
            ResumeLayout(true);
            AppTheme.Apply(this);
            WireDirtyTracking();
            LoadFromDefinition(Definition);
            FormClosing += OnFormClosing;

            AcceptButton = _btnSave;
            CancelButton = _btnClose;
        }

        private void WireDirtyTracking()
        {
            _txtName.TextChanged += (_, _) => MarkDirty();
            if (!_deviceIdReadOnly)
                _txtId.TextChanged += (_, _) => MarkDirty();
            _numDlc.ValueChanged += (_, _) =>
            {
                MarkDirty();
                RefreshPayloadGrid();
            };
            _rbStandard.CheckedChanged += (_, _) => { if (_rbStandard.Checked) MarkDirty(); };
            _rbExtended.CheckedChanged += (_, _) => { if (_rbExtended.Checked) MarkDirty(); };
        }

        private void MarkDirty()
        {
            if (_dirty) return;
            _dirty = true;
            _notice.Text = "Есть несохранённые изменения.";
            _notice.Tone = StatusTone.Warning;
            UpdateTitle();
        }

        private void UpdateTitle() => Text = _dirty ? _baseTitle + " *" : _baseTitle;

        private void BuildLayout()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 4 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(UiFactory.Header("Посылка XLSX", "Параметры CAN-кадра и расположение сигналов в данных.", IconKind.Signal), 0, 0);
            root.Controls.Add(MessageEditFormHelpers.BuildMessageMetadata(_txtName, _txtId, _numDlc, _rbStandard, _rbExtended, _deviceIdReadOnly), 0, 1);

            var signalButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                Padding = new Padding(0, 0, 0, 12),
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
            signalButtons.Controls.Add(new Label { Text = "Сигналы", AutoSize = true, Font = Typography.SectionTitle, ForeColor = AppTheme.TextPrimary, Margin = new Padding(0, 8, 24, 0) });
            signalButtons.Controls.AddRange(new Control[] { _btnAdd, _btnEdit, _btnDelete });

            var filterPanel = BuildSignalFilterPanel();

            _lblFilterStatus.Dock = DockStyle.Fill;
            _lblFilterStatus.AutoSize = false;
            _lblFilterStatus.Height = 28;
            _lblFilterStatus.Font = Typography.Caption;
            _lblFilterStatus.ForeColor = AppTheme.TextSecondary;

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
            _grid.Columns.Add("Name", "Сигнал");
            _grid.Columns.Add("ByteIdx", "Байт");
            _grid.Columns.Add("StartBit", "Бит");
            _grid.Columns.Add("Length", "Длина");
            _grid.Columns.Add("Type", "Тип");
            _grid.Columns.Add("Factor", "Масштаб");
            _grid.Columns.Add("Offset", "Смещение");
            _grid.Columns.Add("Unit", "Ед.");
            _grid.Columns.Add("Order", "Порядок");
            MessageEditFormHelpers.AddSignalColorColumn(_grid);
            foreach (DataGridViewColumn col in _grid.Columns)
                col.SortMode = DataGridViewColumnSortMode.NotSortable;
            MessageEditFormHelpers.ApplySignalListColumnWeights(_grid);
            MessageEditFormHelpers.WireSignalColorColumnPainting(_grid, GetColorForGridRow);
            AppTheme.StyleGrid(_grid, "Сигналов пока нет. Добавьте первый сигнал или измените фильтры.");
            _grid.SelectionChanged += (_, _) => UpdateSelectionActions();

            var gridHost = MessageEditFormHelpers.BuildSignalListWithPayloadGrid(_grid, _payloadGrid);
            WirePayloadSelectionSync();

            var card = new ModernCard { Dock = DockStyle.Fill, Margin = new Padding(0) };
            var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            content.Controls.Add(signalButtons, 0, 0);
            content.Controls.Add(filterPanel, 0, 1);
            content.Controls.Add(_lblFilterStatus, 0, 2);
            content.Controls.Add(gridHost, 0, 3);
            card.Controls.Add(content);
            root.Controls.Add(card, 0, 2);

            _btnSave.Text = "Применить изменения";
            _btnSave.AutoSize = true;
            _btnSave.Click += (_, _) => SaveChanges();
            _btnClose.Text = "Закрыть";
            _btnClose.AutoSize = true;
            _btnClose.Click += (_, _) => Close();
            _notice.Text = "Примените посылку, затем сохраните исходный файл.";
            _notice.Tone = StatusTone.Neutral;
            root.Controls.Add(MessageEditFormHelpers.BuildEditorFooter(_notice, _btnSave, _btnClose), 0, 3);
            Controls.Add(root);
            UpdateSelectionActions();
        }

        private Panel BuildSignalFilterPanel()
        {
            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                Padding = new Padding(0, 0, 0, 8),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true
            };

            _txtSearch.Width = 220;
            _txtSearch.PlaceholderText = "Имя параметра";
            _txtLenMin.PlaceholderText = "—";
            _txtLenMax.PlaceholderText = "—";

            void OnFilterChanged(object? s, EventArgs e) => RefreshGrid();
            _txtSearch.TextChanged += OnFilterChanged;
            _cmbType.SelectedIndexChanged += OnFilterChanged;
            _cmbOrder.SelectedIndexChanged += OnFilterChanged;
            _txtLenMin.TextChanged += OnFilterChanged;
            _txtLenMax.TextChanged += OnFilterChanged;

            panel.Controls.Add(MessageEditFormHelpers.BuildFilterField("Поиск", _txtSearch, 220));
            panel.Controls.Add(MessageEditFormHelpers.BuildFilterField("Тип", _cmbType, 120));
            panel.Controls.Add(MessageEditFormHelpers.BuildRangeFilter("Длина, бит", _txtLenMin, _txtLenMax));
            panel.Controls.Add(MessageEditFormHelpers.BuildFilterField("Порядок байтов", _cmbOrder, 132));
            var reset = new ModernButton { Text = "Сбросить", Icon = IconKind.Clear, Variant = ButtonVariant.Ghost, AutoSize = true, Margin = new Padding(8, 20, 0, 0) };
            reset.Click += (_, _) => { _txtSearch.Clear(); _cmbType.SelectedIndex = 0; _cmbOrder.SelectedIndex = 0; _txtLenMin.Clear(); _txtLenMax.Clear(); };
            panel.Controls.Add(reset);

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
            _notice.Text = "Примените посылку, затем сохраните исходный файл.";
            _notice.Tone = StatusTone.Neutral;
            UpdateTitle();
        }

        private void RefreshGrid()
        {
            _syncingPayloadSelection = true;
            try
            {
                _grid.Rows.Clear();
                string query = _txtSearch.Text.Trim();

                if (!TryGetSignalLengthFilters(out int? lenMin, out int? lenMax))
                {
                    _lblFilterStatus.Text = "Фильтр: неверное число в «Длина»";
                    _highlightedSignalName = null;
                    RefreshPayloadGrid();
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
                            "",
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
                            "",
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

                if (_highlightedSignalName != null
                    && !_rows.Any(r => r.Header.Equals(_highlightedSignalName, StringComparison.OrdinalIgnoreCase)))
                {
                    _highlightedSignalName = null;
                }

                RefreshPayloadGrid();
            }
            finally
            {
                _syncingPayloadSelection = false;
                UpdateSelectionActions();
            }
        }

        private void RefreshPayloadGrid(string? highlightName = null)
        {
            if (highlightName != null)
                _highlightedSignalName = highlightName;

            _signalColors = CanPayloadGridPalette.AssignColors(_rows.Select(r => r.Header ?? ""));
            _payloadGrid.Dlc = (int)_numDlc.Value;
            _payloadGrid.Overlays = CanPayloadGridFactory.FromDeviceRows(
                _rows,
                _highlightedSignalName,
                _signalColors);
            _grid.Invalidate();
        }

        private Color? GetColorForGridRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _grid.Rows.Count)
                return null;
            if (_grid.Rows[rowIndex].Cells["Name"].Value is not string name)
                return null;
            return _signalColors.TryGetValue(name, out Color c) ? c : null;
        }

        private void WirePayloadSelectionSync()
        {
            _grid.SelectionChanged += (_, _) =>
            {
                if (_syncingPayloadSelection) return;

                int idx = SelectedIndex();
                if (idx < 0 || idx >= _rows.Count)
                {
                    _highlightedSignalName = null;
                    RefreshPayloadGrid();
                    return;
                }

                _highlightedSignalName = _rows[idx].Header;
                RefreshPayloadGrid(_highlightedSignalName);
            };

            _payloadGrid.OverlaySelected += (_, e) =>
            {
                if (_syncingPayloadSelection) return;

                _syncingPayloadSelection = true;
                try
                {
                    if (string.IsNullOrEmpty(e.OverlayName))
                    {
                        _highlightedSignalName = null;
                        _grid.ClearSelection();
                    }
                    else
                    {
                        _highlightedSignalName = e.OverlayName;
                        MessageEditFormHelpers.SelectRowByName(_grid, e.OverlayName);
                    }

                    RefreshPayloadGrid(_highlightedSignalName);
                }
                finally
                {
                    _syncingPayloadSelection = false;
                }
            };
        }

        private int SelectedIndex() => MessageEditFormHelpers.SelectedSourceIndex(_grid);

        private void UpdateSelectionActions()
        {
            bool selected = SelectedIndex() >= 0;
            _btnEdit.Enabled = selected;
            _btnDelete.Enabled = selected;
        }

        private void SelectRowBySourceIndex(int sourceIndex)
            => MessageEditFormHelpers.SelectRowBySourceIndex(_grid, sourceIndex);

        private void AddSignal()
        {
            using var dlg = new DeviceFieldRowEditForm(null, (int)_numDlc.Value, _rows.Count, _rows);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            if (_rows.Any(x => x.Header.Equals(dlg.Row.Header, StringComparison.OrdinalIgnoreCase)))
            {
                AppDialog.Show(this, "Параметр с таким именем уже существует.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

            using var dlg = new DeviceFieldRowEditForm(
                _rows[idx],
                (int)_numDlc.Value,
                idx,
                _rows,
                _rows[idx].Header);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            if (_rows
                .Where((_, i) => i != idx)
                .Any(x => x.Header.Equals(dlg.Row.Header, StringComparison.OrdinalIgnoreCase)))
            {
                AppDialog.Show(this, "Параметр с таким именем уже существует.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

            var confirm = AppDialog.Show(
                this,
                $"Удалить параметр '{_rows[idx].Header}'?",
                "Подтверждение",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            _rows.RemoveAt(idx);
            _highlightedSignalName = null;
            MarkDirty();
            RefreshGrid();
        }

        private void SaveChanges()
        {
            if (!TryCommitChanges())
                return;

            _dirty = false;
            _saved = true;
            _notice.Text = "Изменения применены. Сохраните файл в редакторе посылок.";
            _notice.Tone = StatusTone.Success;
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
                AppDialog.Show(this, "Введите имя посылки (MessageName).", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtName.Focus();
                return false;
            }

            bool isExtended = _rbExtended.Checked;
            if (!MessageEditFormHelpers.TryParseHexId(_txtId.Text, isExtended, out uint id, out string idError))
            {
                AppDialog.Show(this, idError, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtId.Focus();
                return false;
            }

            if (_rows.Count == 0)
            {
                var cont = AppDialog.Show(
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
                        AppDialog.Show(
                            this,
                            $"Параметр '{r.Header}' выходит за пределы DLC={dlc} байт.",
                            "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return false;
                    }
                }
            }

            string deviceId = CanId.Format(id);
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
