namespace logReader.UI
{
    partial class CanLogViewForm
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
            panelTop = new Panel();
            labelSearch = new Label();
            textBoxSearch = new TextBox();
            labelCount = new Label();
            scrollPanel = new Panel();
            panelTop.SuspendLayout();
            SuspendLayout();
            // 
            // panelTop
            // 
            panelTop.BackColor = Color.FromArgb(248, 248, 248);
            panelTop.Controls.Add(labelSearch);
            panelTop.Controls.Add(textBoxSearch);
            panelTop.Controls.Add(labelCount);
            panelTop.Dock = DockStyle.Top;
            panelTop.Location = new Point(0, 0);
            panelTop.Name = "panelTop";
            panelTop.Size = new Size(560, 44);
            panelTop.TabIndex = 0;
            // 
            // labelSearch
            // 
            labelSearch.AutoSize = true;
            labelSearch.Location = new Point(10, 13);
            labelSearch.Name = "labelSearch";
            labelSearch.Size = new Size(45, 15);
            labelSearch.TabIndex = 0;
            labelSearch.Text = "Поиск:";
            // 
            // textBoxSearch
            // 
            textBoxSearch.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            textBoxSearch.Location = new Point(58, 10);
            textBoxSearch.Name = "textBoxSearch";
            textBoxSearch.PlaceholderText = "Поиск по ID посылки...";
            textBoxSearch.Size = new Size(700, 23);
            textBoxSearch.TabIndex = 0;
            textBoxSearch.TextChanged += textBoxSearch_TextChanged;
            // 
            // labelCount
            // 
            labelCount.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            labelCount.AutoSize = true;
            labelCount.ForeColor = Color.DimGray;
            labelCount.Location = new Point(410, 13);
            labelCount.Name = "labelCount";
            labelCount.Size = new Size(0, 15);
            labelCount.TabIndex = 1;
            // 
            // scrollPanel
            // 
            scrollPanel.AutoScroll = true;
            scrollPanel.Dock = DockStyle.Fill;
            scrollPanel.Location = new Point(0, 44);
            scrollPanel.Name = "scrollPanel";
            scrollPanel.Size = new Size(560, 476);
            scrollPanel.TabIndex = 1;
            // 
            // CanLogViewForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(560, 520);
            Controls.Add(scrollPanel);
            Controls.Add(panelTop);
            MinimumSize = new Size(420, 340);
            Name = "CanLogViewForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Посылки CAN-лога";
            panelTop.ResumeLayout(false);
            panelTop.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private Panel panelTop;
        private Label labelSearch;
        private TextBox textBoxSearch;
        private Label labelCount;
        private Panel scrollPanel;
    }
}