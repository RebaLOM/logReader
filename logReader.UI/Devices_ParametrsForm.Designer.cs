namespace logReader.UI
{
    partial class Devices_ParametrsForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            tabControlMain = new TabControl();
            tabKnown = new TabPage();
            tabUnknown = new TabPage();
            scrollPanel = new Panel();
            panelButtons = new Panel();
            panelSearch = new Panel();
            buttonEnableAll = new Button();
            buttonDisableAll = new Button();
            textBoxSearch = new TextBox();
            labelSearch = new Label();
            listBoxMissing = new ListBox();
            listBoxMatched = new ListBox();
            panelSearchUnknown = new Panel();
            labelSearchUnknown = new Label();
            textBoxSearchUnknown = new TextBox();
            splitContainerLog = new SplitContainer();
            panelMissingColumn = new Panel();
            panelMatchedColumn = new Panel();
            labelMissingTitle = new Label();
            labelMatchedTitle = new Label();
            
            tabControlMain.SuspendLayout();
            tabKnown.SuspendLayout();
            tabUnknown.SuspendLayout();
            panelButtons.SuspendLayout();
            panelSearch.SuspendLayout();
            panelSearchUnknown.SuspendLayout();
            splitContainerLog.Panel1.SuspendLayout();
            splitContainerLog.Panel2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainerLog).BeginInit();
            splitContainerLog.SuspendLayout();
            panelMissingColumn.SuspendLayout();
            panelMatchedColumn.SuspendLayout();
            SuspendLayout();
            // 
            // tabControlMain
            // 
            tabControlMain.Controls.Add(tabKnown);
            tabControlMain.Controls.Add(tabUnknown);
            tabControlMain.Dock = DockStyle.Fill;
            tabControlMain.Location = new Point(0, 0);
            tabControlMain.Name = "tabControlMain";
            tabControlMain.SelectedIndex = 0;
            tabControlMain.Size = new Size(580, 620);
            tabControlMain.TabIndex = 0;
            // 
            // tabKnown
            // 
            tabKnown.Controls.Add(scrollPanel);
            tabKnown.Controls.Add(panelSearch);
            tabKnown.Controls.Add(panelButtons);
            tabKnown.Location = new Point(4, 24);
            tabKnown.Name = "tabKnown";
            tabKnown.Padding = new Padding(3);
            tabKnown.Size = new Size(572, 592);
            tabKnown.TabIndex = 0;
            tabKnown.Text = "Устройства";
            tabKnown.UseVisualStyleBackColor = true;
            // 
            // tabUnknown
            // 
            tabUnknown.Controls.Add(splitContainerLog);
            tabUnknown.Controls.Add(panelSearchUnknown);
            tabUnknown.Location = new Point(4, 24);
            tabUnknown.Name = "tabUnknown";
            tabUnknown.Padding = new Padding(3);
            tabUnknown.Size = new Size(572, 592);
            tabUnknown.TabIndex = 1;
            tabUnknown.Text = "Сверка с логом";
            tabUnknown.UseVisualStyleBackColor = true;
            // 
            // panelSearchUnknown
            // 
            panelSearchUnknown.Controls.Add(labelSearchUnknown);
            panelSearchUnknown.Controls.Add(textBoxSearchUnknown);
            panelSearchUnknown.Dock = DockStyle.Top;
            panelSearchUnknown.Height = 44;
            panelSearchUnknown.Name = "panelSearchUnknown";
            panelSearchUnknown.BackColor = Color.FromArgb(248, 248, 248);
            panelSearchUnknown.TabIndex = 0;
            // 
            // labelSearchUnknown
            // 
            labelSearchUnknown.AutoSize = true;
            labelSearchUnknown.Location = new Point(10, 13);
            labelSearchUnknown.Name = "labelSearchUnknown";
            labelSearchUnknown.Text = "Поиск:";
            // 
            // textBoxSearchUnknown
            // 
            textBoxSearchUnknown.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            textBoxSearchUnknown.Location = new Point(60, 10);
            textBoxSearchUnknown.Name = "textBoxSearchUnknown";
            textBoxSearchUnknown.PlaceholderText = "Поиск по ID устройства...";
            textBoxSearchUnknown.Size = new Size(490, 23);
            textBoxSearchUnknown.TabIndex = 1;
            textBoxSearchUnknown.TextChanged += textBoxSearchUnknown_TextChanged;
            // 
            // splitContainerLog
            // 
            splitContainerLog.Dock = DockStyle.Fill;
            splitContainerLog.Location = new Point(3, 47);
            splitContainerLog.Name = "splitContainerLog";
            splitContainerLog.Size = new Size(566, 542);
            splitContainerLog.SplitterDistance = 283;
            splitContainerLog.TabIndex = 1;
            // 
            // panelMissingColumn
            // 
            panelMissingColumn.Controls.Add(listBoxMissing);
            panelMissingColumn.Controls.Add(labelMissingTitle);
            panelMissingColumn.Dock = DockStyle.Fill;
            panelMissingColumn.Name = "panelMissingColumn";
            panelMissingColumn.Padding = new Padding(6, 4, 3, 4);
            panelMissingColumn.TabIndex = 0;
            // 
            // labelMissingTitle
            // 
            labelMissingTitle.Dock = DockStyle.Top;
            labelMissingTitle.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold, GraphicsUnit.Point);
            labelMissingTitle.ForeColor = Color.FromArgb(64, 64, 64);
            labelMissingTitle.Name = "labelMissingTitle";
            labelMissingTitle.Padding = new Padding(0, 0, 0, 4);
            labelMissingTitle.Size = new Size(268, 40);
            labelMissingTitle.TabIndex = 0;
            labelMissingTitle.Text = "В логе, но нет в файле посылок:";
            // 
            // listBoxMissing
            // 
            listBoxMissing.BorderStyle = BorderStyle.FixedSingle;
            listBoxMissing.Dock = DockStyle.Fill;
            listBoxMissing.Font = new Font("Consolas", 11.25F, FontStyle.Regular, GraphicsUnit.Point);
            listBoxMissing.FormattingEnabled = true;
            listBoxMissing.ItemHeight = 18;
            listBoxMissing.Name = "listBoxMissing";
            listBoxMissing.TabIndex = 1;
            // 
            // panelMatchedColumn
            // 
            panelMatchedColumn.Controls.Add(listBoxMatched);
            panelMatchedColumn.Controls.Add(labelMatchedTitle);
            panelMatchedColumn.Dock = DockStyle.Fill;
            panelMatchedColumn.Name = "panelMatchedColumn";
            panelMatchedColumn.Padding = new Padding(3, 4, 6, 4);
            panelMatchedColumn.TabIndex = 0;
            // 
            // labelMatchedTitle
            // 
            labelMatchedTitle.Dock = DockStyle.Top;
            labelMatchedTitle.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold, GraphicsUnit.Point);
            labelMatchedTitle.ForeColor = Color.FromArgb(64, 64, 64);
            labelMatchedTitle.Name = "labelMatchedTitle";
            labelMatchedTitle.Padding = new Padding(0, 0, 0, 4);
            labelMatchedTitle.Size = new Size(268, 40);
            labelMatchedTitle.TabIndex = 0;
            labelMatchedTitle.Text = "Есть и в логе, и в файле посылок:";
            // 
            // listBoxMatched
            // 
            listBoxMatched.BorderStyle = BorderStyle.FixedSingle;
            listBoxMatched.Dock = DockStyle.Fill;
            listBoxMatched.Font = new Font("Consolas", 11.25F, FontStyle.Regular, GraphicsUnit.Point);
            listBoxMatched.FormattingEnabled = true;
            listBoxMatched.ItemHeight = 18;
            listBoxMatched.Name = "listBoxMatched";
            listBoxMatched.TabIndex = 1;
            splitContainerLog.Panel1.Controls.Add(panelMissingColumn);
            splitContainerLog.Panel2.Controls.Add(panelMatchedColumn);
            // 
            // panelButtons
            // 
            panelButtons.Controls.Add(buttonEnableAll);
            panelButtons.Controls.Add(buttonDisableAll);
            panelButtons.Dock = DockStyle.Bottom;
            panelButtons.Height = 48;
            panelButtons.Name = "panelButtons";
            panelButtons.BackColor = Color.FromArgb(240, 240, 240);
            panelButtons.TabIndex = 2;
            // 
            // buttonEnableAll
            // 
            buttonEnableAll.Location = new Point(12, 10);
            buttonEnableAll.Name = "buttonEnableAll";
            buttonEnableAll.Size = new Size(130, 28);
            buttonEnableAll.TabIndex = 0;
            buttonEnableAll.Text = "Включить все";
            buttonEnableAll.UseVisualStyleBackColor = true;
            buttonEnableAll.Click += buttonEnableAll_Click;
            // 
            // buttonDisableAll
            // 
            buttonDisableAll.Location = new Point(150, 10);
            buttonDisableAll.Name = "buttonDisableAll";
            buttonDisableAll.Size = new Size(140, 28);
            buttonDisableAll.TabIndex = 1;
            buttonDisableAll.Text = "Выключить все";
            buttonDisableAll.UseVisualStyleBackColor = true;
            buttonDisableAll.Click += buttonDisableAll_Click;
            // 
            // panelSearch
            // 
            panelSearch.Controls.Add(labelSearch);
            panelSearch.Controls.Add(textBoxSearch);
            panelSearch.Dock = DockStyle.Top;
            panelSearch.Height = 44;
            panelSearch.Name = "panelSearch";
            panelSearch.BackColor = Color.FromArgb(248, 248, 248);
            panelSearch.TabIndex = 1;
            // 
            // labelSearch
            // 
            labelSearch.Text = "Поиск:";
            labelSearch.Location = new Point(10, 13);
            labelSearch.AutoSize = true;
            labelSearch.Name = "labelSearch";
            // 
            // textBoxSearch
            // 
            textBoxSearch.Location = new Point(60, 10);
            textBoxSearch.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            textBoxSearch.Size = new Size(490, 23);
            textBoxSearch.Name = "textBoxSearch";
            textBoxSearch.PlaceholderText = "Поиск по ID устройства...";
            textBoxSearch.TabIndex = 0;
            textBoxSearch.TextChanged += textBoxSearch_TextChanged;
            // 
            // scrollPanel
            // 
            scrollPanel.AutoScroll = true;
            scrollPanel.Dock = DockStyle.Fill;
            scrollPanel.Name = "scrollPanel";
            scrollPanel.TabIndex = 0;
            // 
            // Devices_ParametrsForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(580, 620);
            MinimumSize = new Size(480, 400);
            Controls.Add(tabControlMain);
            Name = "Devices_ParametrsForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Устройства и параметры";
            tabControlMain.ResumeLayout(false);
            tabKnown.ResumeLayout(false);
            tabUnknown.ResumeLayout(false);
            panelButtons.ResumeLayout(false);
            panelSearch.ResumeLayout(false);
            panelSearch.PerformLayout();
            panelSearchUnknown.ResumeLayout(false);
            panelSearchUnknown.PerformLayout();
            splitContainerLog.Panel1.ResumeLayout(false);
            splitContainerLog.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainerLog).EndInit();
            splitContainerLog.ResumeLayout(false);
            panelMissingColumn.ResumeLayout(false);
            panelMatchedColumn.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private TabControl tabControlMain;
        private TabPage tabKnown;
        private TabPage tabUnknown;
        private Panel scrollPanel;
        private Panel panelButtons;
        private Panel panelSearch;
        private Button buttonEnableAll;
        private Button buttonDisableAll;
        private TextBox textBoxSearch;
        private Label labelSearch;
        private ListBox listBoxMissing;
        private ListBox listBoxMatched;
        private Panel panelSearchUnknown;
        private Label labelSearchUnknown;
        private TextBox textBoxSearchUnknown;
        private SplitContainer splitContainerLog;
        private Panel panelMissingColumn;
        private Panel panelMatchedColumn;
        private Label labelMissingTitle;
        private Label labelMatchedTitle;
    }
}