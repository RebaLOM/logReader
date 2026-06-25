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
            panelSearch = new Panel();
            textBoxSearch = new TextBox();
            splitContainer = new SplitContainer();
            treeViewTopics = new TreeView();
            richTextBoxHelp = new RichTextBox();
            panelSearch.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer).BeginInit();
            splitContainer.Panel1.SuspendLayout();
            splitContainer.Panel2.SuspendLayout();
            splitContainer.SuspendLayout();
            SuspendLayout();
            // 
            // panelSearch
            // 
            panelSearch.Controls.Add(textBoxSearch);
            panelSearch.Dock = DockStyle.Top;
            panelSearch.Padding = new Padding(8, 8, 8, 4);
            panelSearch.Size = new Size(900, 40);
            panelSearch.TabIndex = 0;
            // 
            // textBoxSearch
            // 
            textBoxSearch.Dock = DockStyle.Fill;
            textBoxSearch.Location = new Point(8, 8);
            textBoxSearch.Name = "textBoxSearch";
            textBoxSearch.PlaceholderText = "Поиск по справке…";
            textBoxSearch.Size = new Size(884, 23);
            textBoxSearch.TabIndex = 0;
            textBoxSearch.TextChanged += textBoxSearch_TextChanged;
            // 
            // splitContainer
            // 
            splitContainer.Dock = DockStyle.Fill;
            splitContainer.FixedPanel = FixedPanel.Panel1;
            splitContainer.Location = new Point(0, 40);
            splitContainer.Name = "splitContainer";
            // 
            // splitContainer.Panel1
            // 
            splitContainer.Panel1.Controls.Add(treeViewTopics);
            splitContainer.Panel1.Padding = new Padding(4, 0, 0, 4);
            // 
            // splitContainer.Panel2
            // 
            splitContainer.Panel2.Controls.Add(richTextBoxHelp);
            splitContainer.Panel2.Padding = new Padding(0, 0, 4, 4);
            splitContainer.Size = new Size(900, 560);
            splitContainer.SplitterDistance = 260;
            splitContainer.TabIndex = 1;
            // 
            // treeViewTopics
            // 
            treeViewTopics.Dock = DockStyle.Fill;
            treeViewTopics.HideSelection = false;
            treeViewTopics.Location = new Point(4, 0);
            treeViewTopics.Name = "treeViewTopics";
            treeViewTopics.ShowLines = true;
            treeViewTopics.ShowPlusMinus = true;
            treeViewTopics.Size = new Size(256, 556);
            treeViewTopics.TabIndex = 0;
            treeViewTopics.AfterSelect += treeViewTopics_AfterSelect;
            // 
            // richTextBoxHelp
            // 
            richTextBoxHelp.BackColor = Color.White;
            richTextBoxHelp.BorderStyle = BorderStyle.None;
            richTextBoxHelp.Dock = DockStyle.Fill;
            richTextBoxHelp.Location = new Point(0, 0);
            richTextBoxHelp.Name = "richTextBoxHelp";
            richTextBoxHelp.ReadOnly = true;
            richTextBoxHelp.Size = new Size(632, 556);
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
            MinimumSize = new Size(720, 480);
            Name = "HelpForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Помощь";
            Load += HelpForm_Load;
            panelSearch.ResumeLayout(false);
            panelSearch.PerformLayout();
            splitContainer.Panel1.ResumeLayout(false);
            splitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer).EndInit();
            splitContainer.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private Panel panelSearch;
        private TextBox textBoxSearch;
        private SplitContainer splitContainer;
        private TreeView treeViewTopics;
        private RichTextBox richTextBoxHelp;
    }
}
