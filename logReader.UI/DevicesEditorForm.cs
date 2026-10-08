using System.Globalization;
using System.Linq;
using logReader;
using logReader.UI.Controls;
using logReader.UI.Icons;
using logReader.UI.Theme;

namespace logReader.UI
{
    internal sealed class DevicesEditorForm : Form
    {
        public enum FileKind { Xlsx, Dbc, Dbf }

        private readonly string _path;
        private readonly FileKind _kind;
        private bool UsesDbcModel => _kind is FileKind.Dbc or FileKind.Dbf;

        private readonly DataGridView _grid = new();
        private readonly Button _btnAdd = new ModernButton { Icon = IconKind.Plus, Variant = ButtonVariant.Primary };
        private readonly Button _btnEdit = new ModernButton { Icon = IconKind.Edit, Variant = ButtonVariant.Secondary };
        private readonly Button _btnDelete = new ModernButton { Icon = IconKind.Trash, Variant = ButtonVariant.Danger };
        private readonly Button _btnSave = new ModernButton { Icon = IconKind.Save, Variant = ButtonVariant.Primary };
        private readonly Button _btnClose = new ModernButton { Icon = IconKind.Close, Variant = ButtonVariant.Ghost };
        private readonly InlineNotice _notice = new();
        private readonly Label _lblInfo = new();
        private readonly TextBox _txtSearch = new ModernTextBox();
        private readonly ComboBox _cmbFormat = MessageEditFormHelpers.MakeFormatFilterCombo();
        private readonly TextBox _txtDlcMin = MessageEditFormHelpers.MakeFilterTextBox();
        private readonly TextBox _txtDlcMax = MessageEditFormHelpers.MakeFilterTextBox();
        private readonly TextBox _txtSigMin = MessageEditFormHelpers.MakeFilterTextBox();
        private readonly TextBox _txtSigMax = MessageEditFormHelpers.MakeFilterTextBox();
        private readonly Label _lblFilterStatus = new();

        private List<DeviceDefinition> _xlsxDevices = new();
        private List<DbcMessage> _dbcMessages = new();

        private bool _dirty;
        private bool _suppressClosePrompt;

        public bool Modified { get; private set; }

        public DevicesEditorForm(string path)
        {
            _path = path;
            string ext = Path.GetExtension(path);
            _kind = ext.Equals(".dbf", StringComparison.OrdinalIgnoreCase)
                ? FileKind.Dbf
                : ext.Equals(".dbc", StringComparison.OrdinalIgnoreCase)
                    ? FileKind.Dbc
                    : FileKind.Xlsx;

            SuspendLayout();
            Text = _kind switch
            {
                FileKind.Dbf => "Редактор посылок (DBF)",
                FileKind.Dbc => "Редактор посылок (DBC)",
                _ => "Редактор посылок (XLSX)"
            };
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            MinimumSize = new Size(960, 640);
            ClientSize = new Size(1080, 740);

            Icon = Application.OpenForms.OfType<MainForm>().FirstOrDefault()?.Icon;

            BuildLayout();
            ResumeLayout(true);
            AppTheme.Apply(this);
            LoadFromFile();
            FormClosing += OnFormClosing;
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, Padding = new Padding(24),
                ColumnCount = 1, RowCount = 4
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(UiFactory.Header("Посылки", $"{Path.GetFileName(_path)} · {_kind.ToString().ToUpperInvariant()}", IconKind.Devices), 0, 0);

            _lblInfo.Dock = DockStyle.Fill;
            _lblInfo.AutoEllipsis = true;
            _lblInfo.Height = 24;
            _lblInfo.Text = _path;
            _lblInfo.Font = Typography.Caption;
            _lblInfo.ForeColor = AppTheme.TextMuted;
            _lblInfo.Margin = new Padding(0, 0, 0, 16);
            root.Controls.Add(_lblInfo, 0, 1);

            _grid.Dock = DockStyle.Fill;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToResizeRows = false;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.ReadOnly = true;
            _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) EditSelected(); };

            _grid.Columns.Add("Name", "Посылка");
            _grid.Columns.Add("Id", "ID (hex)");
            _grid.Columns.Add("Fmt", "Формат");
            _grid.Columns.Add("Dlc", "DLC");
            _grid.Columns.Add("Count", "Сигналов");
            MessageEditFormHelpers.MakeGridColumnsNotSortable(_grid);
            MessageEditFormHelpers.ApplyDevicesListColumnWeights(_grid);
            AppTheme.StyleGrid(_grid, "Посылок пока нет. Добавьте первую посылку или измените фильтры.");
            _grid.SelectionChanged += (_, _) => UpdateSelectionActions();

            var topBtns = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                Padding = new Padding(0, 0, 0, 12),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };
            _btnAdd.Text = "Добавить посылку";
            _btnAdd.AutoSize = true;
            _btnAdd.Click += (_, _) => AddNew();

            _btnEdit.Text = "Изменить";
            _btnEdit.AutoSize = true;
            _btnEdit.Click += (_, _) => EditSelected();

            _btnDelete.Text = "Удалить";
            _btnDelete.AutoSize = true;
            _btnDelete.Click += (_, _) => DeleteSelected();

            topBtns.Controls.AddRange(new Control[] { _btnAdd, _btnEdit, _btnDelete });

            var filterPanel = BuildMessageFilterPanel();

            _lblFilterStatus.Dock = DockStyle.Fill;
            _lblFilterStatus.AutoSize = false;
            _lblFilterStatus.Height = 28;
            _lblFilterStatus.Font = Typography.Caption;
            _lblFilterStatus.ForeColor = AppTheme.TextSecondary;
            var card = new ModernCard { Dock = DockStyle.Fill, Margin = new Padding(0) };
            var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            content.Controls.Add(topBtns, 0, 0);
            content.Controls.Add(filterPanel, 0, 1);
            content.Controls.Add(_lblFilterStatus, 0, 2);
            content.Controls.Add(_grid, 0, 3);
            card.Controls.Add(content);
            root.Controls.Add(card, 0, 2);

            _btnSave.Text = "Сохранить изменения";
            _btnSave.AutoSize = true;
            _btnSave.Enabled = false;
            _btnSave.Click += (_, _) => SaveToDisk();
            _btnClose.Text = "Закрыть";
            _btnClose.AutoSize = true;
            _btnClose.Click += (_, _) => Close();
            _notice.Text = "Изменения сохраняются в исходный файл.";
            _notice.Tone = StatusTone.Neutral;
            root.Controls.Add(MessageEditFormHelpers.BuildEditorFooter(_notice, _btnSave, _btnClose), 0, 3);
            Controls.Add(root);
            CancelButton = _btnClose;
            UpdateSelectionActions();
        }

        private Panel BuildMessageFilterPanel()
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
            _txtSearch.PlaceholderText = "Имя или CAN ID";

            _txtDlcMin.PlaceholderText = "—";
            _txtDlcMax.PlaceholderText = "—";
            _txtSigMin.PlaceholderText = "—";
            _txtSigMax.PlaceholderText = "—";

            void OnFilterChanged(object? s, EventArgs e) => RefreshGrid();
            _txtSearch.TextChanged += OnFilterChanged;
            _cmbFormat.SelectedIndexChanged += OnFilterChanged;
            _txtDlcMin.TextChanged += OnFilterChanged;
            _txtDlcMax.TextChanged += OnFilterChanged;
            _txtSigMin.TextChanged += OnFilterChanged;
            _txtSigMax.TextChanged += OnFilterChanged;

            panel.Controls.Add(MessageEditFormHelpers.BuildFilterField("Поиск", _txtSearch, 220));
            panel.Controls.Add(MessageEditFormHelpers.BuildFilterField("Формат", _cmbFormat, 132));
            panel.Controls.Add(MessageEditFormHelpers.BuildRangeFilter("DLC, байт", _txtDlcMin, _txtDlcMax));
            panel.Controls.Add(MessageEditFormHelpers.BuildRangeFilter("Сигналов", _txtSigMin, _txtSigMax));
            var reset = new ModernButton { Text = "Сбросить", Icon = IconKind.Clear, Variant = ButtonVariant.Ghost, AutoSize = true, Margin = new Padding(8, 20, 0, 0) };
            reset.Click += (_, _) =>
            {
                _txtSearch.Clear();
                _cmbFormat.SelectedIndex = 0;
                _txtDlcMin.Clear(); _txtDlcMax.Clear();
                _txtSigMin.Clear(); _txtSigMax.Clear();
            };
            panel.Controls.Add(reset);

            return panel;
        }

        private bool TryGetMessageFilters(out int? dlcMin, out int? dlcMax, out int? sigMin, out int? sigMax)
        {
            dlcMin = dlcMax = sigMin = sigMax = null;
            if (!MessageEditFormHelpers.TryParseOptionalInt(_txtDlcMin.Text, out dlcMin)) return false;
            if (!MessageEditFormHelpers.TryParseOptionalInt(_txtDlcMax.Text, out dlcMax)) return false;
            if (!MessageEditFormHelpers.TryParseOptionalInt(_txtSigMin.Text, out sigMin)) return false;
            if (!MessageEditFormHelpers.TryParseOptionalInt(_txtSigMax.Text, out sigMax)) return false;
            return true;
        }

        private bool PassesFormatFilter(bool isExtended)
        {
            return _cmbFormat.SelectedIndex switch
            {
                1 => !isExtended,
                2 => isExtended,
                _ => true
            };
        }

        private void LoadFromFile()
        {
            try
            {
                if (UsesDbcModel)
                {
                    _dbcMessages = _kind == FileKind.Dbf
                        ? DbfFile.Read(_path)
                        : DbcFile.Read(_path);
                }
                else
                {
                    _xlsxDevices = DeviceExcelFile.ReadAllDevices(_path);
                }
                RefreshGrid();
            }
            catch (Exception ex)
            {
                AppDialog.Show(this, "Ошибка чтения файла: " + ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshGrid()
        {
            _grid.Rows.Clear();
            string query = _txtSearch.Text.Trim();

            if (!TryGetMessageFilters(out int? dlcMin, out int? dlcMax, out int? sigMin, out int? sigMax))
            {
                _lblFilterStatus.Text = "Фильтр: неверное число в DLC или «Сигналов»";
                return;
            }

            int total;
            int shown = 0;

            if (UsesDbcModel)
            {
                total = _dbcMessages.Count;
                for (int i = 0; i < _dbcMessages.Count; i++)
                {
                    var m = _dbcMessages[i];
                    if (!PassesFormatFilter(m.IsExtended)) continue;
                    if (!MessageEditFormHelpers.InOptionalRange(m.Dlc, dlcMin, dlcMax)) continue;
                    if (!MessageEditFormHelpers.InOptionalRange(m.Signals.Count, sigMin, sigMax)) continue;
                    string idHex = CanId.Format(m.Id, m.IsExtended);
                    if (!MessageEditFormHelpers.TextMatchesQuery(m.Name, query)
                        && !MessageEditFormHelpers.IdMatchesQuery(idHex, query))
                        continue;

                    int rowIdx = _grid.Rows.Add(
                        m.Name,
                        idHex,
                        m.IsExtended ? "Extended" : "Standard",
                        m.Dlc.ToString(CultureInfo.InvariantCulture),
                        m.Signals.Count.ToString(CultureInfo.InvariantCulture));
                    _grid.Rows[rowIdx].Tag = i;
                    shown++;
                }
            }
            else
            {
                total = _xlsxDevices.Count;
                for (int i = 0; i < _xlsxDevices.Count; i++)
                {
                    var d = _xlsxDevices[i];
                    string displayName = string.IsNullOrWhiteSpace(d.MessageName) ? d.DeviceId : d.MessageName;
                    if (!PassesFormatFilter(d.Extended)) continue;
                    if (!MessageEditFormHelpers.InOptionalRange(d.Dlc, dlcMin, dlcMax)) continue;
                    if (!MessageEditFormHelpers.InOptionalRange(d.Rows.Count, sigMin, sigMax)) continue;
                    if (!MessageEditFormHelpers.TextMatchesQuery(displayName, query)
                        && !MessageEditFormHelpers.TextMatchesQuery(d.MessageName, query)
                        && !MessageEditFormHelpers.IdMatchesQuery(d.DeviceId, query))
                        continue;

                    int rowIdx = _grid.Rows.Add(
                        displayName,
                        d.DeviceId,
                        d.Extended ? "Extended" : "Standard",
                        d.Dlc.ToString(CultureInfo.InvariantCulture),
                        d.Rows.Count.ToString(CultureInfo.InvariantCulture));
                    _grid.Rows[rowIdx].Tag = i;
                    shown++;
                }
            }

            _lblFilterStatus.Text = shown == total
                ? $"Показано: {shown}"
                : $"Показано: {shown} из {total}";
            UpdateSelectionActions();
        }

        private int SelectedIndex() => MessageEditFormHelpers.SelectedSourceIndex(_grid);

        private void UpdateSelectionActions()
        {
            bool selected = SelectedIndex() >= 0;
            _btnEdit.Enabled = selected;
            _btnDelete.Enabled = selected;
        }

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

        private void AddNew()
        {
            if (UsesDbcModel)
            {
                using var dlg = new DbcMessageEditForm(null);
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                if (_dbcMessages.Any(x =>
                    x.Name.Equals(dlg.Message.Name, StringComparison.OrdinalIgnoreCase)
                    || (x.Id == dlg.Message.Id && x.IsExtended == dlg.Message.IsExtended)))
                {
                    AppDialog.Show(this,
                        "Посылка с таким именем или ID уже существует.",
                        "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                _dbcMessages.Add(dlg.Message);
                MarkDirty();
                RefreshGrid();
                SelectRowBySourceIndex(_dbcMessages.Count - 1);
            }
            else
            {
                using var dlg = new XlsxMessageEditForm(null);
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                if (_xlsxDevices.Any(x => x.DeviceId.Equals(dlg.Definition.DeviceId, StringComparison.OrdinalIgnoreCase)))
                {
                    AppDialog.Show(this,
                        "Посылка с таким ID уже существует.",
                        "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                _xlsxDevices.Add(dlg.Definition);
                MarkDirty();
                RefreshGrid();
                SelectRowBySourceIndex(_xlsxDevices.Count - 1);
            }
        }

        private void EditSelected()
        {
            int idx = SelectedIndex();
            if (idx < 0) return;

            if (UsesDbcModel)
            {
                if (idx >= _dbcMessages.Count) return;
                using var dlg = new DbcMessageEditForm(_dbcMessages[idx]);
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                if (_dbcMessages
                    .Where((_, i) => i != idx)
                    .Any(x => x.Name.Equals(dlg.Message.Name, StringComparison.OrdinalIgnoreCase)
                              || (x.Id == dlg.Message.Id && x.IsExtended == dlg.Message.IsExtended)))
                {
                    AppDialog.Show(this,
                        "Посылка с таким именем или ID уже существует.",
                        "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                _dbcMessages[idx] = dlg.Message;
                MarkDirty();
                RefreshGrid();
                SelectRowBySourceIndex(idx);
            }
            else
            {
                if (idx >= _xlsxDevices.Count) return;
                var dev = _xlsxDevices[idx];
                using var dlg = new XlsxMessageEditForm(dev, deviceIdReadOnly: true);
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                _xlsxDevices[idx] = dlg.Definition;
                MarkDirty();
                RefreshGrid();
                SelectRowBySourceIndex(idx);
            }
        }

        private void DeleteSelected()
        {
            int idx = SelectedIndex();
            if (idx < 0) return;

            string name;
            if (UsesDbcModel)
            {
                if (idx >= _dbcMessages.Count) return;
                var m = _dbcMessages[idx];
                name = $"{m.Name} (ID={m.Id:X})";
            }
            else
            {
                if (idx >= _xlsxDevices.Count) return;
                var dx = _xlsxDevices[idx];
                name = string.IsNullOrWhiteSpace(dx.MessageName) ? dx.DeviceId : $"{dx.MessageName} (ID={dx.DeviceId})";
            }

            var confirm = AppDialog.Show(
                this,
                $"Удалить посылку '{name}'?",
                "Подтверждение",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            if (UsesDbcModel)
            {
                if (idx >= _dbcMessages.Count) return;
                _dbcMessages.RemoveAt(idx);
            }
            else
            {
                if (idx >= _xlsxDevices.Count) return;
                _xlsxDevices.RemoveAt(idx);
            }

            MarkDirty();
            RefreshGrid();
        }

        private void MarkDirty()
        {
            _dirty = true;
            _btnSave.Enabled = true;
            _notice.Text = "Есть несохранённые изменения.";
            _notice.Tone = StatusTone.Warning;
            UpdateTitle();
        }

        private void UpdateTitle()
        {
            string baseTitle = _kind switch
            {
                FileKind.Dbf => "Редактор посылок (DBF)",
                FileKind.Dbc => "Редактор посылок (DBC)",
                _ => "Редактор посылок (XLSX)"
            };
            Text = _dirty ? baseTitle + " *" : baseTitle;
        }

        private void SaveToDisk()
        {
            if (TrySaveAll())
            {
                _notice.Text = "Изменения сохранены.";
                _notice.Tone = StatusTone.Success;
            }
        }

        private void OnFormClosing(object? sender, FormClosingEventArgs e)
        {
            MessageEditFormHelpers.ResolveFormCloseWithDirty(
                this,
                e,
                _dirty,
                _suppressClosePrompt,
                _path,
                () =>
                {
                    if (!TrySaveAll())
                        return false;
                    _suppressClosePrompt = true;
                    DialogResult = DialogResult.OK;
                    return true;
                },
                DialogResult.OK);
        }

        private bool TrySaveAll()
        {
            try
            {
                EnsureFileNotLocked();

                if (UsesDbcModel)
                {
                    if (_kind == FileKind.Dbf)
                        DbfFile.Write(_path, _dbcMessages);
                    else
                        DbcFile.Write(_path, _dbcMessages);
                }
                else
                    DeviceExcelFile.WriteAllDevices(_path, _xlsxDevices);

                Modified = true;
                _dirty = false;
                _btnSave.Enabled = false;
                UpdateTitle();
                return true;
            }
            catch (Exception ex)
            {
                AppDialog.Show(this, "Ошибка сохранения: " + ex.Message + "\nИзменения отменены.",
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private void EnsureFileNotLocked()
        {
            if (!File.Exists(_path)) return;
            using var fs = new FileStream(_path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
    }
}
