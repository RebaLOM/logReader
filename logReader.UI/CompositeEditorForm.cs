using System.Globalization;
using System.Linq;
using logReader;

namespace logReader.UI
{
    // Как и редактор посылок: правки в памяти, запись — «Сохранить изменения» или «Да» при закрытии.
    internal sealed class CompositeEditorForm : Form
    {
        private readonly string _path;

        private readonly DataGridView _grid = new();
        private readonly Button _btnAdd = new();
        private readonly Button _btnEdit = new();
        private readonly Button _btnDelete = new();
        private readonly Button _btnSave = new();
        private readonly Label _lblInfo = new();

        private List<CompositeSignal> _signals = new();
        private List<int> _skippedRows = new();
        private bool _dirty;
        private bool _suppressClosePrompt;

        public bool Modified { get; private set; }

        public bool LoadFailed { get; private set; }

        private const string BaseTitle = "Редактор составных параметров (XLSX)";

        public CompositeEditorForm(string path)
        {
            _path = path;

            Text = BaseTitle;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            MinimumSize = new Size(820, 480);
            ClientSize = new Size(820, 480);
            Icon = Application.OpenForms.OfType<MainForm>().FirstOrDefault()?.Icon;

            BuildLayout();
            LoadFromFile();
            FormClosing += OnFormClosing;
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
            _grid.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Enter) { EditSelected(); e.Handled = true; }
                else if (e.KeyCode == Keys.Delete) { DeleteSelected(); e.Handled = true; }
            };

            _grid.Columns.Add("Block", "Блок");
            _grid.Columns.Add("Param", "Параметр");
            _grid.Columns.Add("Bits", "Бит");
            _grid.Columns.Add("Trigger", "Триггер");
            _grid.Columns.Add("Sources", "Источники (Source:Byte.BitStart+Len)");
            _grid.Columns["Block"]!.FillWeight = 14;
            _grid.Columns["Param"]!.FillWeight = 26;
            _grid.Columns["Bits"]!.FillWeight = 8;
            _grid.Columns["Trigger"]!.FillWeight = 14;
            _grid.Columns["Sources"]!.FillWeight = 38;

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
            _btnAdd.Text = "&Добавить";
            _btnAdd.AutoSize = true;
            _btnAdd.Click += (_, _) => AddNew();
            _btnEdit.Text = "&Изменить";
            _btnEdit.AutoSize = true;
            _btnEdit.Click += (_, _) => EditSelected();
            _btnDelete.Text = "&Удалить";
            _btnDelete.AutoSize = true;
            _btnDelete.Click += (_, _) => DeleteSelected();
            topBtns.Controls.AddRange(new Control[] { _btnAdd, _btnEdit, _btnDelete });

            var bottom = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(12),
                AutoSize = true,
                WrapContents = false
            };
            _btnSave.Text = "&Сохранить изменения";
            _btnSave.AutoSize = true;
            _btnSave.Click += (_, _) => SaveToDisk();
            bottom.Controls.Add(_btnSave);

            Controls.Add(gridHost);
            Controls.Add(topBtns);
            Controls.Add(_lblInfo);
            Controls.Add(bottom);
        }

        private void LoadFromFile()
        {
            try
            {
                _skippedRows = new List<int>();
                _signals = CompositeExcelFile.ReadAll(_path, null, _skippedRows);
                RefreshGrid();
                if (_skippedRows.Count > 0)
                    _lblInfo.Text = $"Файл: {_path}   (некорректных строк: {_skippedRows.Count} — см. предупреждение при сохранении)";
            }
            catch (Exception ex)
            {
                LoadFailed = true;
                MessageBox.Show(this, "Ошибка чтения файла: " + ex.Message + "\nРедактор не будет открыт, файл не изменён.",
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshGrid()
        {
            _grid.Rows.Clear();
            foreach (var sig in _signals)
            {
                int totalBits = sig.Pieces.Sum(p => p.BitLen);
                string trigger = string.IsNullOrWhiteSpace(sig.TriggerId) ? sig.ResolveDefaultTriggerId() : sig.TriggerId;
                string sources = string.Join(" + ",
                    sig.Pieces.Select(p => $"{p.SourceId}:{p.Byte}.{p.BitStart}+{p.BitLen}"));

                _grid.Rows.Add(
                    sig.Block,
                    sig.Param,
                    totalBits.ToString(CultureInfo.InvariantCulture),
                    trigger,
                    sources);
            }
        }

        private int SelectedIndex() => _grid.CurrentRow?.Index ?? -1;

        private bool IsDuplicate(CompositeSignal candidate, int exceptIndex)
            => _signals.Where((_, i) => i != exceptIndex).Any(s =>
                s.Block.Equals(candidate.Block, StringComparison.OrdinalIgnoreCase)
                && s.Param.Equals(candidate.Param, StringComparison.OrdinalIgnoreCase));

        private void AddNew()
        {
            using var dlg = new CompositeParamEditForm(null);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            if (IsDuplicate(dlg.Signal, -1))
            {
                MessageBox.Show(this, "Параметр с таким именем уже есть в этом блоке.",
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _signals.Add(dlg.Signal);
            MarkDirty();
            RefreshGrid();
            SelectRow(_signals.Count - 1);
        }

        private void EditSelected()
        {
            int idx = SelectedIndex();
            if (idx < 0 || idx >= _signals.Count) return;

            using var dlg = new CompositeParamEditForm(_signals[idx]);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            if (IsDuplicate(dlg.Signal, idx))
            {
                MessageBox.Show(this, "Параметр с таким именем уже есть в этом блоке.",
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _signals[idx] = dlg.Signal;
            MarkDirty();
            RefreshGrid();
            SelectRow(idx);
        }

        private void DeleteSelected()
        {
            int idx = SelectedIndex();
            if (idx < 0 || idx >= _signals.Count) return;

            var sig = _signals[idx];
            var confirm = MessageBox.Show(
                this,
                $"Удалить параметр '{sig.Param}' (блок {sig.Block})?",
                "Подтверждение",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            _signals.RemoveAt(idx);
            MarkDirty();
            RefreshGrid();
        }

        private void SelectRow(int idx)
        {
            if (idx < 0 || idx >= _grid.Rows.Count) return;
            _grid.ClearSelection();
            _grid.Rows[idx].Selected = true;
            _grid.CurrentCell = _grid.Rows[idx].Cells[0];
        }

        private void MarkDirty()
        {
            _dirty = true;
            Text = BaseTitle + " *";
        }

        private void SaveToDisk()
        {
            if (TrySaveAll())
                MessageBox.Show(this, "Изменения сохранены.", "Сохранение", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            if (_skippedRows.Count > 0)
            {
                var answer = MessageBox.Show(this,
                    "В файле есть некорректные строки, которые не были загружены (строки " +
                    string.Join(", ", _skippedRows.Take(20)) + (_skippedRows.Count > 20 ? ", …" : "") + ").\n" +
                    "При сохранении они будут удалены. Продолжить?",
                    "Подтверждение", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (answer != DialogResult.Yes) return false;
            }

            try
            {
                EnsureFileNotLocked();
                CompositeExcelFile.WriteAll(_path, _signals);
                Modified = true;
                _dirty = false;
                _skippedRows.Clear();
                Text = BaseTitle;
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                MessageBox.Show(this, "Ошибка сохранения: " + ex.Message + "\nФайл на диске не изменён, правки остаются в редакторе.",
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
