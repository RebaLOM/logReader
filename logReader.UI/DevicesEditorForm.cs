using System.Globalization;
using System.Linq;
using logReader;

namespace logReader.UI
{
    internal sealed class DevicesEditorForm : Form
    {
        public enum FileKind { Xlsx, Dbc, Dbf }

        private readonly string _path;
        private readonly FileKind _kind;
        private bool UsesDbcModel => _kind is FileKind.Dbc or FileKind.Dbf;

        private readonly DataGridView _grid = new();
        private readonly Button _btnAdd = new();
        private readonly Button _btnEdit = new();
        private readonly Button _btnDelete = new();
        private readonly Button _btnSave = new();
        private readonly Label _lblInfo = new();
        private readonly TextBox _txtSearch = new();
        private readonly ComboBox _cmbFormat = MessageEditFormHelpers.MakeFormatFilterCombo();
        private readonly TextBox _txtDlcMin = MessageEditFormHelpers.MakeFilterTextBox();
        private readonly TextBox _txtDlcMax = MessageEditFormHelpers.MakeFilterTextBox();
        private readonly TextBox _txtSigMin = MessageEditFormHelpers.MakeFilterTextBox();
        private readonly TextBox _txtSigMax = MessageEditFormHelpers.MakeFilterTextBox();
        private readonly Label _lblFilterStatus = new();

        private List<DeviceDefinition> _xlsxDevices = new();
        private DbcDatabase _dbcDatabase = new();
        private List<DbcMessage> _dbcMessages => _dbcDatabase.Messages;

        private bool _dirty;
        private bool _suppressClosePrompt;

        public bool Modified { get; private set; }

        // Файл не прочитан: редактор не открывается, иначе первое сохранение перезаписало бы файл пустым списком.
        public bool LoadFailed { get; private set; }

        public DevicesEditorForm(string path)
        {
            _path = path;
            string ext = Path.GetExtension(path);
            _kind = ext.Equals(".dbf", StringComparison.OrdinalIgnoreCase)
                ? FileKind.Dbf
                : ext.Equals(".dbc", StringComparison.OrdinalIgnoreCase)
                    ? FileKind.Dbc
                    : FileKind.Xlsx;

            Text = _kind switch
            {
                FileKind.Dbf => "Редактор посылок (DBF)",
                FileKind.Dbc => "Редактор посылок (DBC)",
                _ => "Редактор посылок (XLSX)"
            };
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(820, 520);
            ClientSize = new Size(1020, 650);

            Icon = Application.OpenForms.OfType<MainForm>().FirstOrDefault()?.Icon;

            BuildLayout();
            LoadFromFile();
            FormClosing += OnFormClosing;
            UiScaling.Apply(this);
            ThemeManager.Attach(this);
        }

        private void BuildLayout()
        {
            _lblInfo.Text = $"Файл: {_path}";
            _grid.Dock = DockStyle.Fill;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToResizeRows = false;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.ReadOnly = true;
            _grid.RowTemplate.Height = 30;
            _grid.ColumnHeadersHeight = 32;
            _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) EditSelected(); };
            _grid.Columns.Add("Name", "Посылка / Name");
            _grid.Columns.Add("Id", "CAN ID (hex)");
            _grid.Columns.Add("Fmt", "Формат");
            _grid.Columns.Add("Dlc", "DLC");
            _grid.Columns.Add("Count", "Сигналов");
            MessageEditFormHelpers.MakeGridColumnsNotSortable(_grid);
            MessageEditFormHelpers.ApplyDevicesListColumnWeights(_grid);
            var gridHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24, 0, 24, 0), Tag = "background" };
            gridHost.Controls.Add(_grid);

            _btnAdd.Text = "Добавить посылку";
            _btnAdd.Click += (_, _) => AddNew();
            _btnEdit.Text = "Изменить";
            _btnEdit.Click += (_, _) => EditSelected();
            _btnDelete.Text = "Удалить";
            _btnDelete.Click += (_, _) => DeleteSelected();
            _btnSave.Text = "Сохранить изменения";
            _btnSave.Click += (_, _) => SaveToDisk();

            _lblFilterStatus.Dock = DockStyle.Top;
            _lblFilterStatus.Height = 28;
            _lblFilterStatus.Padding = new Padding(24, 4, 24, 4);
            _lblFilterStatus.Tag = "muted";
            var type = _kind.ToString().ToUpperInvariant();
            Controls.Add(gridHost);
            Controls.Add(_lblFilterStatus);
            Controls.Add(BuildMessageFilterPanel());
            Controls.Add(DialogLayout.Actions(_btnAdd, _btnEdit, _btnDelete));
            Controls.Add(DialogLayout.Header(this, $"Конфигурация / {type}", "Посылки и сигналы", _lblInfo));
            Controls.Add(DialogLayout.Footer(_btnSave, "Правки сохраняются в исходный файл."));
        }

        private Panel BuildMessageFilterPanel()
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Top, Height = 74, ColumnCount = 8, RowCount = 2,
                Padding = new Padding(24, 0, 24, 8), Tag = "background"
            };
            for (int column = 0; column < 8; column++)
                panel.ColumnStyles.Add(column % 2 == 0
                    ? new ColumnStyle(SizeType.AutoSize) : new ColumnStyle(SizeType.Percent, 25));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            _txtSearch.PlaceholderText = "Имя посылки или CAN ID…";
            foreach (var box in new[] { _txtDlcMin, _txtDlcMax, _txtSigMin, _txtSigMax }) box.PlaceholderText = "—";
            foreach (var control in new Control[] { _txtSearch, _cmbFormat, _txtDlcMin, _txtDlcMax, _txtSigMin, _txtSigMax })
            { control.Dock = DockStyle.Fill; control.Margin = new Padding(6, 2, 16, 2); }
            void OnFilterChanged(object? sender, EventArgs e) => RefreshGrid();
            _txtSearch.TextChanged += OnFilterChanged;
            _cmbFormat.SelectedIndexChanged += OnFilterChanged;
            _txtDlcMin.TextChanged += OnFilterChanged;
            _txtDlcMax.TextChanged += OnFilterChanged;
            _txtSigMin.TextChanged += OnFilterChanged;
            _txtSigMax.TextChanged += OnFilterChanged;
            panel.Controls.Add(MessageEditFormHelpers.MakeLabel("Поиск"), 0, 0);
            panel.Controls.Add(_txtSearch, 1, 0); panel.SetColumnSpan(_txtSearch, 3);
            panel.Controls.Add(MessageEditFormHelpers.MakeLabel("Формат"), 4, 0);
            panel.Controls.Add(_cmbFormat, 5, 0); panel.SetColumnSpan(_cmbFormat, 3);
            panel.Controls.Add(MessageEditFormHelpers.MakeLabel("DLC от"), 0, 1);
            panel.Controls.Add(_txtDlcMin, 1, 1);
            panel.Controls.Add(MessageEditFormHelpers.MakeLabel("до"), 2, 1);
            panel.Controls.Add(_txtDlcMax, 3, 1);
            panel.Controls.Add(MessageEditFormHelpers.MakeLabel("Сигналов от"), 4, 1);
            panel.Controls.Add(_txtSigMin, 5, 1);
            panel.Controls.Add(MessageEditFormHelpers.MakeLabel("до"), 6, 1);
            panel.Controls.Add(_txtSigMax, 7, 1);
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
                    _dbcDatabase = _kind == FileKind.Dbf
                        ? DbfFile.ReadDatabase(_path)
                        : DbcFile.ReadDatabase(_path);
                }
                else
                {
                    _xlsxDevices = DeviceExcelFile.ReadAllDevices(_path);
                }
                RefreshGrid();

                if (UsesDbcModel && _dbcDatabase.PreservedLineCount > 0)
                    _lblInfo.Text = $"Файл: {_path}   (прочие строки файла — {_dbcDatabase.PreservedLineCount} — сохраняются без изменений)";
            }
            catch (Exception ex)
            {
                LoadFailed = true;
                ThemedMessageBox.Show(this, "Ошибка чтения файла: " + ex.Message + "\nРедактор не будет открыт, файл не изменён.",
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                    ThemedMessageBox.Show(this,
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
                    ThemedMessageBox.Show(this,
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
                if (!ConfirmClassicDlc(_dbcMessages[idx].Dlc)) return;
                using var dlg = new DbcMessageEditForm(_dbcMessages[idx]);
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                if (_dbcMessages
                    .Where((_, i) => i != idx)
                    .Any(x => x.Name.Equals(dlg.Message.Name, StringComparison.OrdinalIgnoreCase)
                              || (x.Id == dlg.Message.Id && x.IsExtended == dlg.Message.IsExtended)))
                {
                    ThemedMessageBox.Show(this,
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
                if (!ConfirmClassicDlc(dev.Dlc)) return;
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

            var confirm = ThemedMessageBox.Show(
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

        // Редактор посылки рассчитан на DLC 1..8: открыть CAN FD-посылку значило бы молча обрезать её до 8 байт.
        private bool ConfirmClassicDlc(int dlc)
        {
            if (dlc <= 8) return true;
            ThemedMessageBox.Show(this,
                $"Посылка с DLC = {dlc} (CAN FD) не редактируется в этой версии: форма поддерживает 1–8 байт.\n" +
                "Посылка сохраняется в файле без изменений и используется при обработке.",
                "CAN FD", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        private void MarkDirty()
        {
            _dirty = true;
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
                ThemedMessageBox.Show(this, "Изменения сохранены.", "Сохранение", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                        DbfFile.WriteDatabase(_path, _dbcDatabase);
                    else
                        DbcFile.WriteDatabase(_path, _dbcDatabase);
                }
                else
                    DeviceExcelFile.WriteAllDevices(_path, _xlsxDevices);

                Modified = true;
                _dirty = false;
                UpdateTitle();
                return true;
            }
            catch (Exception ex)
            {
                ThemedMessageBox.Show(this, "Ошибка сохранения: " + ex.Message + "\nФайл на диске не изменён, правки остаются в редакторе.",
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
