using System.Linq;

namespace logReader.UI
{
    internal sealed class FileKindPromptForm : Form
    {
        public enum FileKind { None, Xlsx, Dbc, Dbf }

        private readonly Dictionary<FileKind, FormatChoiceCard> _choices = new();
        private readonly ModernButton _createButton;
        private FileKind _pendingKind = FileKind.Xlsx;

        public FileKind SelectedKind { get; private set; } = FileKind.None;

        public FileKindPromptForm()
        {
            SuspendLayout();
            Text = "Новый файл посылок";
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(824, 648);
            MinimumSize = new Size(656, 480);
            Icon = Application.OpenForms.OfType<MainForm>().FirstOrDefault()?.Icon;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24),
                Margin = Padding.Empty,
                RowCount = 3,
                ColumnCount = 1
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            var header = UiFactory.Header("Новый файл посылок",
                "Выберите формат описания устройств и параметров CAN-шины.", IconKind.Plus);
            header.Margin = new Padding(0, 0, 0, 20);
            root.Controls.Add(header, 0, 0);

            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Margin = Padding.Empty };
            var choices = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 3,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = new Padding(0, 0, 8, 0)
            };
            choices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            choices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            choices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));
            choices.RowStyles.Add(new RowStyle(SizeType.Absolute, 304));
            choices.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            AddChoice(choices, 0, FileKind.Xlsx, "Excel", ".xlsx", IconKind.Table,
                "Таблица параметров", "Числовые сигналы и BIN-поля. Удобно редактировать в Excel.");
            AddChoice(choices, 1, FileKind.Dbc, "DBC", ".dbc", IconKind.Signal,
                "Описание CAN-шины", "Стандартный текстовый формат посылок и числовых сигналов.");
            AddChoice(choices, 2, FileKind.Dbf, "DBF", ".dbf", IconKind.Devices,
                "База BUSMASTER", "Текстовый формат для BUSMASTER. Содержит числовые сигналы.");

            var note = new InlineNotice
            {
                Text = "На следующем шаге выберите имя и папку. После создания откроется редактор посылок.",
                Tone = StatusTone.Info,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 16, 0, 0),
                MinimumSize = new Size(0, 64)
            };
            choices.Controls.Add(note, 0, 1);
            choices.SetColumnSpan(note, 3);
            scroll.Controls.Add(choices);
            root.Controls.Add(scroll, 0, 1);

            _createButton = new ModernButton
            {
                Text = "Продолжить с XLSX",
                Icon = IconKind.ArrowRight,
                Variant = ButtonVariant.Primary,
                AutoSize = true,
                MinimumSize = new Size(192, 40)
            };
            _createButton.Click += (_, _) =>
            {
                SelectedKind = _pendingKind;
                DialogResult = DialogResult.OK;
                Close();
            };
            var cancelButton = new ModernButton
            {
                Text = "Отмена",
                Variant = ButtonVariant.Ghost,
                AutoSize = true,
                MinimumSize = new Size(100, 40),
                DialogResult = DialogResult.Cancel
            };
            var footer = UiFactory.Footer(_createButton, cancelButton);
            footer.Margin = new Padding(0, 20, 0, 0);
            root.Controls.Add(footer, 0, 2);
            Controls.Add(root);
            AcceptButton = _createButton;
            CancelButton = cancelButton;
            SelectChoice(FileKind.Xlsx);
            ResumeLayout(true);
            AppTheme.Apply(this);
        }

        private void AddChoice(TableLayoutPanel layout, int column, FileKind kind, string title,
            string extension, IconKind icon, string heading, string description)
        {
            var card = new FormatChoiceCard(title, extension, icon, heading, description)
            {
                Dock = DockStyle.Fill,
                Margin = column == 2 ? Padding.Empty : new Padding(0, 0, 12, 0)
            };
            card.SelectRequested += (_, _) => SelectChoice(kind);
            _choices.Add(kind, card);
            layout.Controls.Add(card, column, 0);
        }

        private void SelectChoice(FileKind kind)
        {
            _pendingKind = kind;
            foreach (var choice in _choices)
                choice.Value.SetSelected(choice.Key == kind);
            _createButton.Text = kind switch
            {
                FileKind.Dbc => "Продолжить с DBC",
                FileKind.Dbf => "Продолжить с DBF",
                _ => "Продолжить с XLSX"
            };
        }

        private sealed class FormatChoiceCard : ModernCard
        {
            private readonly ModernButton _selectButton;
            private readonly StatusBadge _selection;
            private readonly IconView _illustration;

            internal event EventHandler? SelectRequested;

            internal FormatChoiceCard(string title, string extension, IconKind icon, string heading, string description)
            {
                Cursor = Cursors.Hand;
                var layout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    RowCount = 6,
                    Margin = Padding.Empty
                };
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
                _illustration = new IconView
                {
                    Icon = icon,
                    ForeColor = AppTheme.TextSecondary,
                    Size = new Size(40, 40),
                    Margin = new Padding(0, 0, 0, 8)
                };
                layout.Controls.Add(_illustration, 0, 0);
                _selectButton = new ModernButton
                {
                    Text = title,
                    Variant = ButtonVariant.Secondary,
                    Dock = DockStyle.Fill,
                    Margin = Padding.Empty,
                    AccessibleName = $"Выбрать {title} {extension}"
                };
                _selectButton.Click += (_, _) => SelectRequested?.Invoke(this, EventArgs.Empty);
                layout.Controls.Add(_selectButton, 0, 1);
                layout.Controls.Add(new Label
                {
                    Text = extension,
                    AutoSize = true,
                    Font = Typography.Mono,
                    ForeColor = AppTheme.TextMuted,
                    Margin = new Padding(0, 8, 0, 8)
                }, 0, 2);
                layout.Controls.Add(new Label
                {
                    Text = heading,
                    AutoSize = true,
                    Dock = DockStyle.Fill,
                    Font = Typography.CardTitle,
                    ForeColor = AppTheme.TextPrimary,
                    Margin = new Padding(0, 0, 0, 8)
                }, 0, 3);
                layout.Controls.Add(new Label
                {
                    Text = description,
                    AutoSize = false,
                    Dock = DockStyle.Fill,
                    Font = Typography.Secondary,
                    ForeColor = AppTheme.TextSecondary,
                    Margin = Padding.Empty
                }, 0, 4);
                _selection = new StatusBadge
                {
                    Text = "Выбрать",
                    Tone = StatusTone.Neutral,
                    Dock = DockStyle.Fill,
                    Margin = new Padding(0, 8, 0, 0)
                };
                layout.Controls.Add(_selection, 0, 5);
                AttachSelection(layout);
                Controls.Add(layout);
                Click += (_, _) => SelectRequested?.Invoke(this, EventArgs.Empty);
            }

            private void AttachSelection(Control root)
            {
                if (root != _selectButton)
                    root.Click += (_, _) => SelectRequested?.Invoke(this, EventArgs.Empty);
                foreach (Control child in root.Controls)
                    AttachSelection(child);
            }

            internal void SetSelected(bool selected)
            {
                _selectButton.Variant = selected ? ButtonVariant.Primary : ButtonVariant.Secondary;
                _selection.Text = selected ? "Выбрано" : "Выбрать";
                _selection.Tone = selected ? StatusTone.Info : StatusTone.Neutral;
                _illustration.ForeColor = selected ? AppTheme.Primary : AppTheme.TextSecondary;
                AccessibleDescription = selected ? "Выбранный формат" : "Нажмите, чтобы выбрать формат";
            }
        }
    }
}
