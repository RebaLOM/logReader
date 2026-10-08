namespace logReader.UI
{
    partial class Devices_ParametrsForm
    {
        private System.ComponentModel.IContainer components = new System.ComponentModel.Container();

        protected override void Dispose(bool disposing)
        {
            if (disposing) components?.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(860, 760);
            MinimumSize = new Size(660, 520);
            Padding = new Padding(24);
            Name = "Devices_ParametrsForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Устройства и параметры";

            tabControlMain = new ModernTabControl { Dock = DockStyle.Fill, Name = "tabControlMain", TabIndex = 0 };
            tabKnown = new TabPage("Устройства") { Padding = new Padding(12), BackColor = AppTheme.Background };
            tabUnknown = new TabPage("Сверка с логом") { Padding = new Padding(12), BackColor = AppTheme.Background };
            tabControlMain.TabPages.Add(tabKnown);
            tabControlMain.TabPages.Add(tabUnknown);

            panelSearch = new ModernCard { Dock = DockStyle.Top, Height = 96, Padding = new Padding(16) };
            textBoxSearch = new ModernTextBox
            {
                Name = "textBoxSearch", TabIndex = 0,
                PlaceholderText = "Введите CAN ID или имя параметра"
            };
            textBoxSearch.TextChanged += textBoxSearch_TextChanged;
            var deviceSearchField = UiFactory.Field("ПОИСК УСТРОЙСТВ И ПАРАМЕТРОВ", textBoxSearch);
            labelSearch = deviceSearchField.Controls.OfType<Label>().First();
            panelSearch.Controls.Add(deviceSearchField);
            scrollPanel = new ModernCard
            {
                Dock = DockStyle.Fill, AutoScroll = false, Padding = new Padding(16),
                Name = "scrollPanel", TabIndex = 1
            };
            emptyDevices = new EmptyState
            {
                Dock = DockStyle.Fill, Title = "Нет устройств", Icon = IconKind.Devices, Visible = false,
                Description = "Загрузите файл посылок, чтобы выбрать устройства и параметры."
            };
            scrollPanel.Controls.Add(emptyDevices);

            buttonEnableAll = new ModernButton
            {
                Text = "Включить все", Icon = IconKind.Check, Variant = ButtonVariant.Secondary,
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, 40), Name = "buttonEnableAll", TabIndex = 0
            };
            buttonDisableAll = new ModernButton
            {
                Text = "Выключить все", Variant = ButtonVariant.Ghost,
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, 40), Name = "buttonDisableAll", TabIndex = 1
            };
            buttonEnableAll.Click += buttonEnableAll_Click;
            buttonDisableAll.Click += buttonDisableAll_Click;
            var selectionTips = new ToolTip(components);
            selectionTips.SetToolTip(buttonEnableAll, "Включить все устройства и параметры, включая скрытые поиском.");
            selectionTips.SetToolTip(buttonDisableAll, "Выключить все устройства и параметры, включая скрытые поиском.");
            panelButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(0, 12, 0, 0), WrapContents = true, BackColor = AppTheme.Background
            };
            panelButtons.Controls.Add(buttonEnableAll);
            panelButtons.Controls.Add(buttonDisableAll);
            tabKnown.Controls.Add(scrollPanel);
            tabKnown.Controls.Add(panelButtons);
            tabKnown.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 16, BackColor = AppTheme.Background });
            tabKnown.Controls.Add(panelSearch);

            panelSearchUnknown = new ModernCard { Dock = DockStyle.Top, Height = 96, Padding = new Padding(16) };
            textBoxSearchUnknown = new ModernTextBox
            {
                Name = "textBoxSearchUnknown", TabIndex = 0,
                PlaceholderText = "Поиск по ID в обеих колонках"
            };
            textBoxSearchUnknown.TextChanged += textBoxSearchUnknown_TextChanged;
            var reconciliationSearchField = UiFactory.Field("СВЕРКА CAN ID", textBoxSearchUnknown);
            labelSearchUnknown = reconciliationSearchField.Controls.OfType<Label>().First();
            panelSearchUnknown.Controls.Add(reconciliationSearchField);
            splitContainerLog = new SplitContainer
            {
                Dock = DockStyle.Fill, Name = "splitContainerLog", Size = new Size(780, 440),
                SplitterDistance = 384, SplitterWidth = 12, Panel1MinSize = 210, Panel2MinSize = 210,
                BackColor = AppTheme.Background, TabIndex = 1
            };
            panelMissingColumn = new ModernCard { Dock = DockStyle.Fill, Padding = new Padding(16) };
            panelMatchedColumn = new ModernCard { Dock = DockStyle.Fill, Padding = new Padding(16) };
            labelMissingTitle = new Label
            {
                Dock = DockStyle.Top, Height = 48, Text = "Нет в файле посылок", AutoEllipsis = true,
                Font = Typography.CardTitle, ForeColor = AppTheme.TextPrimary
            };
            labelMatchedTitle = new Label
            {
                Dock = DockStyle.Top, Height = 48, Text = "Совпадают", AutoEllipsis = true,
                Font = Typography.CardTitle, ForeColor = AppTheme.TextPrimary
            };
            listBoxMissing = CreateLogIdList("listBoxMissing");
            listBoxMatched = CreateLogIdList("listBoxMatched");
            emptyMissing = new EmptyState { Dock = DockStyle.Fill, Icon = IconKind.Check, Title = "Нет отсутствующих устройств" };
            emptyMatched = new EmptyState { Dock = DockStyle.Fill, Icon = IconKind.Devices, Title = "Нет совпадающих устройств" };
            var missingContent = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.Surface };
            var matchedContent = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.Surface };
            missingContent.Controls.Add(listBoxMissing);
            missingContent.Controls.Add(emptyMissing);
            matchedContent.Controls.Add(listBoxMatched);
            matchedContent.Controls.Add(emptyMatched);
            panelMissingColumn.Controls.Add(missingContent);
            panelMissingColumn.Controls.Add(labelMissingTitle);
            panelMatchedColumn.Controls.Add(matchedContent);
            panelMatchedColumn.Controls.Add(labelMatchedTitle);
            splitContainerLog.Panel1.Controls.Add(panelMissingColumn);
            splitContainerLog.Panel2.Controls.Add(panelMatchedColumn);
            var reconciliationGap = new Panel { Dock = DockStyle.Top, Height = 16, BackColor = AppTheme.Background };
            tabUnknown.Controls.Add(splitContainerLog);
            tabUnknown.Controls.Add(reconciliationGap);
            tabUnknown.Controls.Add(panelSearchUnknown);

            labelSelectionCount = new Label
            {
                Dock = DockStyle.Bottom, Height = 40, Font = Typography.Secondary,
                ForeColor = AppTheme.TextSecondary, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true
            };
            var applyButton = new ModernButton
            {
                Text = "OK", Icon = IconKind.Check, Variant = ButtonVariant.Primary,
                AutoSize = true, MinimumSize = new Size(140, 40), DialogResult = DialogResult.OK
            };
            applyButton.Click += (_, _) => ApplyToTarget();
            var cancelButton = new ModernButton
            {
                Text = "Отмена", Variant = ButtonVariant.Ghost,
                AutoSize = true, MinimumSize = new Size(100, 40), DialogResult = DialogResult.Cancel
            };
            AcceptButton = applyButton;
            CancelButton = cancelButton;
            var footer = UiFactory.Footer(applyButton, cancelButton);
            var header = UiFactory.Header("Устройства и параметры", "Настройте фильтры и сверьте CAN ID с логом. Изменения применяются по кнопке «OK».", IconKind.Devices);
            header.Dock = DockStyle.Top;
            Controls.Add(tabControlMain);
            Controls.Add(labelSelectionCount);
            Controls.Add(footer);
            Controls.Add(header);
            ResumeLayout(true);
        }

        private static ListBox CreateLogIdList(string name) => new ListBox
        {
            Dock = DockStyle.Fill, Name = name, BorderStyle = BorderStyle.None,
            Font = Typography.Mono, ForeColor = AppTheme.TextPrimary, BackColor = AppTheme.Surface,
            IntegralHeight = false, FormattingEnabled = true, HorizontalScrollbar = true, TabIndex = 0
        };

        private TabControl tabControlMain = null!;
        private TabPage tabKnown = null!;
        private TabPage tabUnknown = null!;
        private Panel scrollPanel = null!;
        private Panel panelButtons = null!;
        private Panel panelSearch = null!;
        private Button buttonEnableAll = null!;
        private Button buttonDisableAll = null!;
        private TextBox textBoxSearch = null!;
        private Label labelSearch = null!;
        private ListBox listBoxMissing = null!;
        private ListBox listBoxMatched = null!;
        private Panel panelSearchUnknown = null!;
        private Label labelSearchUnknown = null!;
        private TextBox textBoxSearchUnknown = null!;
        private SplitContainer splitContainerLog = null!;
        private Panel panelMissingColumn = null!;
        private Panel panelMatchedColumn = null!;
        private Label labelMissingTitle = null!;
        private Label labelMatchedTitle = null!;
        private Label labelSelectionCount = null!;
        private EmptyState emptyDevices = null!;
        private EmptyState emptyMissing = null!;
        private EmptyState emptyMatched = null!;
    }
}
