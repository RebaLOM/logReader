using System.Globalization;
using System.Linq;
using logReader;
using logReader.UI.Controls;
using logReader.UI.Icons;
using logReader.UI.Theme;

namespace logReader.UI
{
    // Как и редактор посылок: правки в памяти, запись — «Сохранить изменения» или «Да» при закрытии.
    internal sealed class CompositeEditorForm : Form
    {
        private readonly string _path;

        private readonly DataGridView _grid = new();
        private readonly Button _btnAdd = new ModernButton { Icon = IconKind.Plus, Variant = ButtonVariant.Primary };
        private readonly Button _btnEdit = new ModernButton { Icon = IconKind.Edit, Variant = ButtonVariant.Secondary };
        private readonly Button _btnDelete = new ModernButton { Icon = IconKind.Trash, Variant = ButtonVariant.Danger };
        private readonly Button _btnSave = new ModernButton { Icon = IconKind.Save, Variant = ButtonVariant.Primary };
        private readonly Button _btnClose = new ModernButton { Icon = IconKind.Close, Variant = ButtonVariant.Ghost };
        private readonly Label _lblInfo = new();
        private readonly TextBox _txtSearch = new ModernTextBox();
        private readonly Label _lblCount = new();
        private readonly InlineNotice _notice = new();

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

            SuspendLayout();
            Text = BaseTitle;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            MinimumSize = new Size(960, 600);
            ClientSize = new Size(1080, 700);
            Icon = Application.OpenForms.OfType<MainForm>().FirstOrDefault()?.Icon;

            BuildLayout();
            ResumeLayout(true);
            AppTheme.Apply(this);
            LoadFromFile();
            FormClosing += OnFormClosing;
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 4 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(UiFactory.Header("Составные параметры", $"{Path.GetFileName(_path)} · XLSX", IconKind.Signal), 0, 0);
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
            _grid.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Enter) { EditSelected(); e.Handled = true; }
                else if (e.KeyCode == Keys.Delete) { DeleteSelected(); e.Handled = true; }
            };

            _grid.Columns.Add("Block", "Блок");
            _grid.Columns.Add("Param", "Параметр");
            _grid.Columns.Add("Bits", "Биты");
            _grid.Columns.Add("Trigger", "Триггер");
            _grid.Columns.Add("Sources", "Источники");
            _grid.Columns["Block"]!.FillWeight = 14;
            _grid.Columns["Param"]!.FillWeight = 26;
            _grid.Columns["Bits"]!.FillWeight = 8;
            _grid.Columns["Trigger"]!.FillWeight = 14;
            _grid.Columns["Sources"]!.FillWeight = 38;

            MessageEditFormHelpers.MakeGridColumnsNotSortable(_grid);
            AppTheme.StyleGrid(_grid, "Составных параметров пока нет. Добавьте первый параметр или измените поиск.");
            _grid.SelectionChanged += (_, _) => UpdateSelectionActions();

            var topBtns = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                Padding = new Padding(0, 0, 0, 12),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };
            _btnAdd.Text = "&Добавить параметр";
            _btnAdd.AutoSize = true;
            _btnAdd.Click += (_, _) => AddNew();
            _btnEdit.Text = "&Изменить";
            _btnEdit.AutoSize = true;
            _btnEdit.Click += (_, _) => EditSelected();
            _btnDelete.Text = "&Удалить";
            _btnDelete.AutoSize = true;
            _btnDelete.Click += (_, _) => DeleteSelected();
            topBtns.Controls.AddRange(new Control[] { _btnAdd, _btnEdit, _btnDelete });

            var card = new ModernCard { Dock = DockStyle.Fill, Margin = new Padding(0) };
            var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _txtSearch.PlaceholderText = "Блок, параметр или источник";
            _txtSearch.Width = 320;
            _txtSearch.TextChanged += (_, _) => RefreshGrid();
            _lblCount.Dock = DockStyle.Fill;
            _lblCount.Font = Typography.Caption;
            _lblCount.ForeColor = AppTheme.TextSecondary;
            _lblCount.Height = 28;
            content.Controls.Add(topBtns, 0, 0);
            content.Controls.Add(UiFactory.Field("Поиск", _txtSearch), 0, 1);
            content.Controls.Add(_lblCount, 0, 2);
            content.Controls.Add(_grid, 0, 3);
            card.Controls.Add(content);
            root.Controls.Add(card, 0, 2);

            _btnSave.Text = "&Сохранить изменения";
            _btnSave.AutoSize = true;
            _btnSave.Enabled = false;
            _btnSave.Click += (_, _) => SaveToDisk();
            _btnClose.Text = "Закрыть";
            _btnClose.AutoSize = true;
            _btnClose.DialogResult = DialogResult.OK;
            _notice.Text = "Изменения сохраняются в исходный файл по кнопке «Сохранить».";
            _notice.Tone = StatusTone.Neutral;
            root.Controls.Add(MessageEditFormHelpers.BuildEditorFooter(_notice, _btnSave, _btnClose), 0, 3);
            Controls.Add(root);

            CancelButton = _btnClose;
            UpdateSelectionActions();
        }

        private void LoadFromFile()
        {
            try
            {
                _skippedRows = new List<int>();
                _signals = CompositeExcelFile.ReadAll(_path, null, _skippedRows);
                RefreshGrid();
                if (_skippedRows.Count > 0)
                {
                    _lblInfo.Text = $"Файл: {_path}   (некорректных строк: {_skippedRows.Count} — см. предупреждение при сохранении)";
                    _notice.Text = $"Не загружено некорректных строк: {_skippedRows.Count}. При сохранении потребуется подтверждение их удаления.";
                    _notice.Tone = StatusTone.Warning;
                    _btnSave.Enabled = true;
                }
            }
            catch (Exception ex)
            {
                LoadFailed = true;
                AppDialog.Show(this, "Ошибка чтения файла: " + ex.Message + "\nРедактор не будет открыт, файл не изменён.",
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshGrid()
        {
            _grid.Rows.Clear();
            string query = _txtSearch.Text.Trim();
            int shown = 0;
            for (int i = 0; i < _signals.Count; i++)
            {
                var sig = _signals[i];
                int totalBits = sig.Pieces.Sum(p => p.BitLen);
                string trigger = string.IsNullOrWhiteSpace(sig.TriggerId) ? sig.ResolveDefaultTriggerId() : sig.TriggerId;
                string sources = string.Join(" + ",
                    sig.Pieces.Select(p => $"{p.SourceId}:{p.Byte}.{p.BitStart}+{p.BitLen}"));

                if (!MessageEditFormHelpers.TextMatchesQuery(sig.Block, query)
                    && !MessageEditFormHelpers.TextMatchesQuery(sig.Param, query)
                    && !MessageEditFormHelpers.TextMatchesQuery(sources, query)) continue;

                int rowIndex = _grid.Rows.Add(
                    sig.Block,
                    sig.Param,
                    totalBits.ToString(CultureInfo.InvariantCulture),
                    trigger,
                    sources);
                _grid.Rows[rowIndex].Tag = i;
                shown++;
            }
            _lblCount.Text = shown == _signals.Count ? $"Показано: {shown}" : $"Показано: {shown} из {_signals.Count}";
            UpdateSelectionActions();
        }

        private int SelectedIndex() => MessageEditFormHelpers.SelectedSourceIndex(_grid);

        private void UpdateSelectionActions()
        {
            bool selected = SelectedIndex() >= 0;
            _btnEdit.Enabled = selected;
            _btnDelete.Enabled = selected;
        }

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
                AppDialog.Show(this, "Параметр с таким именем уже есть в этом блоке.",
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
                AppDialog.Show(this, "Параметр с таким именем уже есть в этом блоке.",
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
            var confirm = AppDialog.Show(
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
            MessageEditFormHelpers.SelectRowBySourceIndex(_grid, idx);
        }

        private void MarkDirty()
        {
            _dirty = true;
            _btnSave.Enabled = !LoadFailed;
            _notice.Text = "Есть несохранённые изменения.";
            _notice.Tone = StatusTone.Warning;
            Text = BaseTitle + " *";
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
            if (LoadFailed) return false;
            if (_skippedRows.Count > 0)
            {
                var answer = AppDialog.Show(this,
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
                _notice.Text = "Изменения сохранены.";
                _notice.Tone = StatusTone.Success;
                _dirty = false;
                _skippedRows.Clear();
                _btnSave.Enabled = false;
                _lblInfo.Text = _path;
                Text = BaseTitle;
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                AppDialog.Show(this, "Ошибка сохранения: " + ex.Message + "\nФайл на диске не изменён, правки остаются в редакторе.",
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
