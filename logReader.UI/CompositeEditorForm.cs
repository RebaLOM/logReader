using System.Globalization;
using System.Linq;
using logReader;

namespace logReader.UI
{
    internal sealed class CompositeEditorForm : Form
    {
        private readonly string _path;

        private readonly DataGridView _grid = new();
        private readonly Button _btnAdd = new();
        private readonly Button _btnEdit = new();
        private readonly Button _btnDelete = new();
        private readonly Button _btnClose = new();
        private readonly Label _lblInfo = new();

        private List<CompositeSignal> _signals = new();

        public bool Modified { get; private set; }

        public CompositeEditorForm(string path)
        {
            _path = path;

            Text = "Редактор составных параметров (XLSX)";
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi;
            MinimumSize = new Size(820, 480);
            ClientSize = new Size(820, 480);
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
                _signals = CompositeExcelFile.ReadAll(_path);
                RefreshGrid();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Ошибка чтения файла: " + ex.Message, "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
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

        private void AddNew()
        {
            using var dlg = new CompositeParamEditForm(null);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            if (_signals.Any(s =>
                s.Block.Equals(dlg.Signal.Block, StringComparison.OrdinalIgnoreCase)
                && s.Param.Equals(dlg.Signal.Param, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(this, "Параметр с таким именем уже есть в этом блоке.",
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var backup = new List<CompositeSignal>(_signals);
            _signals.Add(dlg.Signal);
            if (!TrySaveAll()) _signals = backup;
            RefreshGrid();
        }

        private void EditSelected()
        {
            int idx = SelectedIndex();
            if (idx < 0 || idx >= _signals.Count) return;

            using var dlg = new CompositeParamEditForm(_signals[idx]);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            if (_signals.Where((_, i) => i != idx).Any(s =>
                s.Block.Equals(dlg.Signal.Block, StringComparison.OrdinalIgnoreCase)
                && s.Param.Equals(dlg.Signal.Param, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(this, "Параметр с таким именем уже есть в этом блоке.",
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var backup = new List<CompositeSignal>(_signals);
            _signals[idx] = dlg.Signal;
            if (!TrySaveAll()) _signals = backup;
            RefreshGrid();
            if (idx < _grid.Rows.Count) _grid.Rows[idx].Selected = true;
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

            var backup = new List<CompositeSignal>(_signals);
            _signals.RemoveAt(idx);
            if (!TrySaveAll()) _signals = backup;
            RefreshGrid();
        }

        private bool TrySaveAll()
        {
            try
            {
                EnsureFileNotLocked();
                CompositeExcelFile.WriteAll(_path, _signals);
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
