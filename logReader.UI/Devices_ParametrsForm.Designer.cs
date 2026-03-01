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
            scrollPanel = new Panel();
            panelButtons = new Panel();
            panelSearch = new Panel();
            buttonEnableAll = new Button();
            buttonDisableAll = new Button();
            textBoxSearch = new TextBox();
            labelSearch = new Label();
            panelButtons.SuspendLayout();
            panelSearch.SuspendLayout();
            SuspendLayout();
            // 
            // panelButtons — прибит к низу
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
            // panelSearch — прибит к верху
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
            // scrollPanel — Fill, занимает всё между panelSearch и panelButtons
            // 
            scrollPanel.AutoScroll = true;
            scrollPanel.Dock = DockStyle.Fill;
            scrollPanel.Name = "scrollPanel";
            scrollPanel.TabIndex = 0;
            // 
            // Devices_ParametrsForm
            // ВАЖНО: порядок добавления — Bottom, Top, потом Fill
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(580, 620);
            MinimumSize = new Size(480, 400);
            Controls.Add(scrollPanel);
            Controls.Add(panelSearch);
            Controls.Add(panelButtons);
            Name = "Devices_ParametrsForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Устройства и параметры";
            panelButtons.ResumeLayout(false);
            panelSearch.ResumeLayout(false);
            panelSearch.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private Panel scrollPanel;
        private Panel panelButtons;
        private Panel panelSearch;
        private Button buttonEnableAll;
        private Button buttonDisableAll;
        private TextBox textBoxSearch;
        private Label labelSearch;
    }
}