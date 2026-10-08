namespace logReader.UI
{
    partial class HelpForm
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
            panelHeader = new Panel();
            headerBrandMark = new Panel();
            labelHelpTitle = new Label();
            panelSearch = new Panel();
            textBoxSearch = new TextBox();
            splitContainer = new SplitContainer();
            treeViewTopics = new TreeView();
            richTextBoxHelp = new RichTextBox();
            panelHeader.SuspendLayout();
            panelSearch.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer).BeginInit();
            splitContainer.Panel1.SuspendLayout();
            splitContainer.Panel2.SuspendLayout();
            splitContainer.SuspendLayout();
            SuspendLayout();
            // 
            // panelHeader
            // 
            panelHeader.Controls.Add(labelHelpTitle);
            panelHeader.Controls.Add(headerBrandMark);
            panelHeader.Dock = DockStyle.Top;
            panelHeader.Name = "panelHeader";
            panelHeader.Padding = new Padding(16, 12, 16, 12);
            panelHeader.Size = new Size(900, 48);
            panelHeader.TabIndex = 0;
            // 
            // headerBrandMark
            // 
            headerBrandMark.Location = new Point(16, 18);
            headerBrandMark.Name = "headerBrandMark";
            headerBrandMark.Size = new Size(12, 12);
            headerBrandMark.TabIndex = 0;
            // 
            // labelHelpTitle
            // 
            labelHelpTitle.AutoSize = true;
            labelHelpTitle.Location = new Point(36, 14);
            labelHelpTitle.Name = "labelHelpTitle";
            labelHelpTitle.Size = new Size(62, 20);
            labelHelpTitle.TabIndex = 1;
            labelHelpTitle.Text = "Помощь";
            // 
            // panelSearch
            // 
            panelSearch.Controls.Add(textBoxSearch);
            panelSearch.Dock = DockStyle.Top;
            panelSearch.Name = "panelSearch";
            panelSearch.Padding = new Padding(16, 8, 16, 12);
            panelSearch.Size = new Size(900, 44);
            panelSearch.TabIndex = 1;
            // 
            // textBoxSearch
            // 
            textBoxSearch.AccessibleName = "Поиск по справке";
            textBoxSearch.Dock = DockStyle.Fill;
            textBoxSearch.Location = new Point(16, 8);
            textBoxSearch.Name = "textBoxSearch";
            textBoxSearch.PlaceholderText = "Поиск по справке…";
            textBoxSearch.Size = new Size(868, 23);
            textBoxSearch.TabIndex = 0;
            textBoxSearch.TextChanged += textBoxSearch_TextChanged;
            // 
            // splitContainer
            // 
            splitContainer.Dock = DockStyle.Fill;
            splitContainer.FixedPanel = FixedPanel.Panel1;
            splitContainer.Location = new Point(0, 92);
            splitContainer.Name = "splitContainer";
            // 
            // splitContainer.Panel1
            // 
            splitContainer.Panel1.Controls.Add(treeViewTopics);
            splitContainer.Panel1.Padding = new Padding(8, 0, 0, 8);
            // 
            // splitContainer.Panel2
            // 
            splitContainer.Panel2.Controls.Add(richTextBoxHelp);
            splitContainer.Panel2.Padding = new Padding(0, 0, 8, 8);
            splitContainer.Size = new Size(900, 508);
            splitContainer.SplitterDistance = 300;
            splitContainer.Panel1MinSize = 220;
            splitContainer.TabIndex = 2;
            // 
            // treeViewTopics
            // 
            treeViewTopics.Dock = DockStyle.Fill;
            treeViewTopics.FullRowSelect = true;
            treeViewTopics.HideSelection = false;
            treeViewTopics.Location = new Point(8, 0);
            treeViewTopics.Name = "treeViewTopics";
            treeViewTopics.ShowLines = false;
            treeViewTopics.ShowPlusMinus = true;
            treeViewTopics.Size = new Size(252, 500);
            treeViewTopics.TabIndex = 0;
            treeViewTopics.AfterSelect += treeViewTopics_AfterSelect;
            // 
            // richTextBoxHelp
            // 
            richTextBoxHelp.BorderStyle = BorderStyle.None;
            richTextBoxHelp.Dock = DockStyle.Fill;
            richTextBoxHelp.Location = new Point(0, 0);
            richTextBoxHelp.Name = "richTextBoxHelp";
            richTextBoxHelp.ReadOnly = true;
            richTextBoxHelp.Size = new Size(624, 500);
            richTextBoxHelp.TabIndex = 0;
            richTextBoxHelp.Text = "";
            // 
            // HelpForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(900, 600);
            Controls.Add(splitContainer);
            Controls.Add(panelSearch);
            Controls.Add(panelHeader);
            MinimumSize = new Size(720, 480);
            Name = "HelpForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Помощь";
            Load += HelpForm_Load;
            panelHeader.ResumeLayout(false);
            panelHeader.PerformLayout();
            panelSearch.ResumeLayout(false);
            panelSearch.PerformLayout();
            splitContainer.Panel1.ResumeLayout(false);
            splitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer).EndInit();
            splitContainer.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private Panel panelHeader;
        private Panel headerBrandMark;
        private Label labelHelpTitle;
        private Panel panelSearch;
        private TextBox textBoxSearch;
        private SplitContainer splitContainer;
        private TreeView treeViewTopics;
        private RichTextBox richTextBoxHelp;
    }
}
