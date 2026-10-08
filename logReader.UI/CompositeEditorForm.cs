using System.Globalization;
using System.Linq;
using logReader;
using logReader.UI.Controls;
using logReader.UI.Icons;
using logReader.UI.Theme;

namespace logReader.UI
{
    internal sealed class CompositeEditorForm : Form
    {
        private readonly string _path;

        private readonly DataGridView _grid = new();
        private readonly Button _btnAdd = new ModernButton { Icon = IconKind.Plus, Variant = ButtonVariant.Primary };
        private readonly Button _btnEdit = new ModernButton { Icon = IconKind.Edit, Variant = ButtonVariant.Secondary };
        private readonly Button _btnDelete = new ModernButton { Icon = IconKind.Trash, Variant = ButtonVariant.Danger };
        private readonly Button _btnClose = new ModernButton { Icon = IconKind.Close, Variant = ButtonVariant.Ghost };
        private readonly Label _lblInfo = new();
        private readonly TextBox _txtSearch = new ModernTextBox();
        private readonly Label _lblCount = new();
        private readonly InlineNotice _notice = new();

        private List<CompositeSignal> _signals = new();

        public bool Modified { get; private set; }

        public CompositeEditorForm(string path)
        {
            _path = path;

            SuspendLayout();
            Text = "Редактор составных параметров (XLSX)";
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            MinimumSize = new Size(960, 600);
            ClientSize = new Size(1080, 700);
            Icon = Application.OpenForms.OfType<MainForm>().FirstOrDefault()?.Icon;

            BuildLayout();
            ResumeLayout(true);
            AppTheme.Apply(this);
            LoadFromFile();
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
            _btnAdd.Text = "Добавить параметр";
            _btnAdd.AutoSize = true;
            _btnAdd.Click += (_, _) => AddNew();
            _btnEdit.Text = "Изменить";
            _btnEdit.AutoSize = true;
            _btnEdit.Click += (_, _) => EditSelected();
            _btnDelete.Text = "Удалить";
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

            _btnClose.Text = "Закрыть";
            _btnClose.AutoSize = true;
            _btnClose.DialogResult = DialogResult.OK;
            _notice.Text = "Изменения сохраняются сразу после подтверждения.";
            _notice.Tone = StatusTone.Info;
            root.Controls.Add(MessageEditFormHelpers.BuildEditorFooter(_notice, _btnClose), 0, 3);
            Controls.Add(root);

            CancelButton = _btnClose;
            UpdateSelectionActions();
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
                AppDialog.Show(this, "Ошибка чтения файла: " + ex.Message, "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
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

        private void AddNew()
        {
            using var dlg = new CompositeParamEditForm(null);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            if (_signals.Any(s =>
                s.Block.Equals(dlg.Signal.Block, StringComparison.OrdinalIgnoreCase)
                && s.Param.Equals(dlg.Signal.Param, StringComparison.OrdinalIgnoreCase)))
            {
                AppDialog.Show(this, "Параметр с таким именем уже есть в этом блоке.",
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
                AppDialog.Show(this, "Параметр с таким именем уже есть в этом блоке.",
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var backup = new List<CompositeSignal>(_signals);
            _signals[idx] = dlg.Signal;
            if (!TrySaveAll()) _signals = backup;
            RefreshGrid();
            MessageEditFormHelpers.SelectRowBySourceIndex(_grid, idx);
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
                _notice.Text = "Изменения сохранены.";
                _notice.Tone = StatusTone.Success;
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
