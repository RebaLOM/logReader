using System.Globalization;
using System.Linq;
using logReader;

namespace logReader.UI
{
    internal sealed class DevicesEditorForm : Form
    {
        public enum FileKind { Xlsx, Dbc }

        private readonly string _path;
        private readonly FileKind _kind;

        private readonly DataGridView _grid = new();
        private readonly Button _btnAdd = new();
        private readonly Button _btnEdit = new();
        private readonly Button _btnDelete = new();
        private readonly Button _btnClose = new();
        private readonly Label _lblInfo = new();

        private List<DeviceDefinition> _xlsxDevices = new();
        private List<DbcMessage> _dbcMessages = new();

        public bool Modified { get; private set; }

        public DevicesEditorForm(string path)
        {
            _path = path;
            string ext = Path.GetExtension(path);
            _kind = ext.Equals(".dbc", StringComparison.OrdinalIgnoreCase)
                ? FileKind.Dbc
                : FileKind.Xlsx;

            Text = _kind == FileKind.Dbc
                ? "Редактор посылок (DBC)"
                : "Редактор посылок (XLSX)";
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi;
            MinimumSize = new Size(760, 480);
            ClientSize = new Size(760, 480);

            Icon = Application.OpenForms.OfType<MainForm>().FirstOrDefault()?.Icon;

            BuildLayout();
            LoadFromFile();
        }

        private void BuildLayout()
        {
            _lblInfo.Dock = DockStyle.Top;
            _lblInfo.AutoSize = false;
            _lblInfo.Height = 28;
            _lblInfo.Padding = new Padding(12, 8, 12, 0);
            _lblInfo.Text = $"Файл: {_path}";
            _lblInfo.ForeColor = Color.DimGray;

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

            _grid.Columns.Add("Name", "Name");
            _grid.Columns.Add("Id", "ID (hex)");
            _grid.Columns.Add("Fmt", "Формат");
            _grid.Columns.Add("Dlc", "DLC");
            _grid.Columns.Add("Count", "Сигналов");
            _grid.Columns["Name"]!.FillWeight = 35;
            _grid.Columns["Id"]!.FillWeight = 18;
            MessageEditFormHelpers.MakeGridColumnsNotSortable(_grid);

            var gridHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 0, 12, 6) };
            gridHost.Controls.Add(_grid);

            var topBtns = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(12, 6, 12, 6),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };
            _btnAdd.Text = "Добавить";
            _btnAdd.AutoSize = true;
            _btnAdd.Click += (_, _) => AddNew();

            _btnEdit.Text = "Изменить";
            _btnEdit.AutoSize = true;
            _btnEdit.Click += (_, _) => EditSelected();

            _btnDelete.Text = "Удалить";
            _btnDelete.AutoSize = true;
            _btnDelete.Click += (_, _) => DeleteSelected();

            topBtns.Controls.AddRange(new Control[] { _btnAdd, _btnEdit, _btnDelete });

            var bottom = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(12),
                Height = 54,
                WrapContents = false
            };
            _btnClose.Text = "Закрыть";
            _btnClose.Width = 100;
            _btnClose.DialogResult = DialogResult.OK;
            bottom.Controls.Add(_btnClose);

            Controls.Add(gridHost);
            Controls.Add(topBtns);
            Controls.Add(_lblInfo);
            Controls.Add(bottom);

            CancelButton = _btnClose;
        }

        private void LoadFromFile()
        {
            try
            {
                if (_kind == FileKind.Dbc)
                {
                    _dbcMessages = DbcFile.Read(_path);
                }
                else
                {
                    _xlsxDevices = DeviceExcelFile.ReadAllDevices(_path);
                }
                RefreshGrid();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Ошибка чтения файла: " + ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshGrid()
        {
            _grid.Rows.Clear();

            if (_kind == FileKind.Dbc)
            {
                foreach (var m in _dbcMessages)
                {
                    _grid.Rows.Add(
                        m.Name,
                        m.Id.ToString("X", CultureInfo.InvariantCulture),
                        m.IsExtended ? "Extended" : "Standard",
                        m.Dlc.ToString(CultureInfo.InvariantCulture),
                        m.Signals.Count.ToString(CultureInfo.InvariantCulture));
                }
            }
            else
            {
                foreach (var d in _xlsxDevices)
                {
                    string displayName = string.IsNullOrWhiteSpace(d.MessageName) ? d.DeviceId : d.MessageName;
                    _grid.Rows.Add(
                        displayName,
                        d.DeviceId,
                        d.Extended ? "Extended" : "Standard",
                        d.Dlc.ToString(CultureInfo.InvariantCulture),
                        d.Rows.Count.ToString(CultureInfo.InvariantCulture));
                }
            }
        }

        private int SelectedIndex()
        {
            if (_grid.CurrentRow == null) return -1;
            return _grid.CurrentRow.Index;
        }

        private void AddNew()
        {
            if (_kind == FileKind.Dbc)
            {
                using var dlg = new DbcMessageEditForm(null);
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                if (_dbcMessages.Any(x =>
                    x.Name.Equals(dlg.Message.Name, StringComparison.OrdinalIgnoreCase)
                    || (x.Id == dlg.Message.Id && x.IsExtended == dlg.Message.IsExtended)))
                {
                    MessageBox.Show(this,
                        "Посылка с таким именем или ID уже существует.",
                        "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var backup = new List<DbcMessage>(_dbcMessages);
                _dbcMessages.Add(dlg.Message);
                if (!TrySaveAll()) { _dbcMessages = backup; }
                RefreshGrid();
            }
            else
            {
                using var dlg = new XlsxMessageEditForm(null);
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                if (_xlsxDevices.Any(x => x.DeviceId.Equals(dlg.Definition.DeviceId, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show(this,
                        "Посылка с таким ID уже существует.",
                        "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var backup = new List<DeviceDefinition>(_xlsxDevices);
                _xlsxDevices.Add(dlg.Definition);
                if (!TrySaveAll()) { _xlsxDevices = backup; }
                RefreshGrid();
            }
        }

        private void EditSelected()
        {
            int idx = SelectedIndex();
            if (idx < 0) return;

            if (_kind == FileKind.Dbc)
            {
                if (idx >= _dbcMessages.Count) return;
                using var dlg = new DbcMessageEditForm(_dbcMessages[idx]);
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                if (_dbcMessages
                    .Where((_, i) => i != idx)
                    .Any(x => x.Name.Equals(dlg.Message.Name, StringComparison.OrdinalIgnoreCase)
                              || (x.Id == dlg.Message.Id && x.IsExtended == dlg.Message.IsExtended)))
                {
                    MessageBox.Show(this,
                        "Посылка с таким именем или ID уже существует.",
                        "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var backup = new List<DbcMessage>(_dbcMessages);
                _dbcMessages[idx] = dlg.Message;
                if (!TrySaveAll()) { _dbcMessages = backup; }
                RefreshGrid();
                if (idx < _grid.Rows.Count) _grid.Rows[idx].Selected = true;
            }
            else
            {
                if (idx >= _xlsxDevices.Count) return;
                var dev = _xlsxDevices[idx];
                using var dlg = new XlsxMessageEditForm(dev, deviceIdReadOnly: true);
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                var backup = new List<DeviceDefinition>(_xlsxDevices);
                _xlsxDevices[idx] = dlg.Definition;
                if (!TrySaveAll()) { _xlsxDevices = backup; }
                RefreshGrid();
                if (idx < _grid.Rows.Count) _grid.Rows[idx].Selected = true;
            }
        }

        private void DeleteSelected()
        {
            int idx = SelectedIndex();
            if (idx < 0) return;

            string name;
            if (_kind == FileKind.Dbc)
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

            var confirm = MessageBox.Show(
                this,
                $"Удалить посылку '{name}'?",
                "Подтверждение",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            if (_kind == FileKind.Dbc)
            {
                var backup = new List<DbcMessage>(_dbcMessages);
                _dbcMessages.RemoveAt(idx);
                if (!TrySaveAll()) { _dbcMessages = backup; }
            }
            else
            {
                var backup = new List<DeviceDefinition>(_xlsxDevices);
                _xlsxDevices.RemoveAt(idx);
                if (!TrySaveAll()) { _xlsxDevices = backup; }
            }

            RefreshGrid();
        }

        // При ошибке вызывающий код откатывает список из снимка.
        private bool TrySaveAll()
        {
            try
            {
                EnsureFileNotLocked();

                if (_kind == FileKind.Dbc)
                    DbcFile.Write(_path, _dbcMessages);
                else
                    DeviceExcelFile.WriteAllDevices(_path, _xlsxDevices);

                Modified = true;
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Ошибка сохранения: " + ex.Message + "\nИзменения отменены.",
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
