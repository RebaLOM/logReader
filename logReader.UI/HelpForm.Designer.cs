namespace logReader.UI
{
    partial class HelpForm
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
            ClientSize = new Size(1100, 760);
            MinimumSize = new Size(800, 540);
            Padding = new Padding(24);
            Name = "HelpForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Справка LOGER";

            panelSearch = new ModernCard { Dock = DockStyle.Top, Height = 96, Padding = new Padding(16) };
            textBoxSearch = new ModernTextBox
            {
                Dock = DockStyle.Top, Name = "textBoxSearch", TabIndex = 0,
                PlaceholderText = "Поиск по разделам и тексту справки"
            };
            textBoxSearch.TextChanged += textBoxSearch_TextChanged;
            panelSearch.Controls.Add(UiFactory.Field("ПОИСК В СПРАВКЕ", textBoxSearch));
            splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel1, Name = "splitContainer",
                Size = new Size(1052, 540), SplitterDistance = 286, SplitterWidth = 16,
                Panel1MinSize = 220, Panel2MinSize = 360, BackColor = AppTheme.Background, TabIndex = 1
            };
            var navigation = new ModernCard { Dock = DockStyle.Fill, Padding = new Padding(16) };
            var caption = new Label
            {
                Dock = DockStyle.Top, Text = "РАЗДЕЛЫ СПРАВКИ", Height = 32,
                Font = Typography.Caption, ForeColor = AppTheme.TextSecondary
            };
            treeViewTopics = new TreeView
            {
                Dock = DockStyle.Fill, Name = "treeViewTopics", HideSelection = false,
                ShowLines = false, ShowRootLines = false, ShowPlusMinus = true,
                BorderStyle = BorderStyle.None, BackColor = AppTheme.Surface, ForeColor = AppTheme.TextPrimary,
                Font = Typography.Body, ItemHeight = 30, Indent = 16, FullRowSelect = true, TabIndex = 0
            };
            treeViewTopics.AfterSelect += treeViewTopics_AfterSelect;
            navigation.Controls.Add(treeViewTopics);
            navigation.Controls.Add(caption);
            var article = new ModernCard { Dock = DockStyle.Fill, Padding = new Padding(24) };
            richTextBoxHelp = new RichTextBox
            {
                Dock = DockStyle.Fill, Name = "richTextBoxHelp", ReadOnly = true,
                BorderStyle = BorderStyle.None, BackColor = AppTheme.Surface,
                ForeColor = AppTheme.TextPrimary, Font = Typography.Body,
                DetectUrls = false, HideSelection = false, ScrollBars = RichTextBoxScrollBars.Vertical,
                TabIndex = 0
            };
            emptyHelp = new EmptyState
            {
                Dock = DockStyle.Fill, Icon = IconKind.Search, Title = "Ничего не найдено",
                Description = "Попробуйте другое слово или очистите строку поиска.", Visible = false
            };
            article.Controls.Add(richTextBoxHelp);
            article.Controls.Add(emptyHelp);
            splitContainer.Panel1.Controls.Add(navigation);
            splitContainer.Panel2.Controls.Add(article);
            labelTopicCount = new Label
            {
                Dock = DockStyle.Bottom, Height = 36, TextAlign = ContentAlignment.MiddleLeft,
                Font = Typography.Secondary, ForeColor = AppTheme.TextSecondary
            };
            var gap = new Panel { Dock = DockStyle.Top, Height = 16, BackColor = AppTheme.Background };
            var header = UiFactory.Header("Справка", "Форматы логов, настройка устройств и работа с результатами", IconKind.Help);
            header.Dock = DockStyle.Top;
            Controls.Add(splitContainer);
            Controls.Add(labelTopicCount);
            Controls.Add(gap);
            Controls.Add(panelSearch);
            Controls.Add(header);
            Load += HelpForm_Load;
            ResumeLayout(true);
        }

        private Panel panelSearch = null!;
        private TextBox textBoxSearch = null!;
        private SplitContainer splitContainer = null!;
        private TreeView treeViewTopics = null!;
        private RichTextBox richTextBoxHelp = null!;
        private Label labelTopicCount = null!;
        private EmptyState emptyHelp = null!;
    }
}
