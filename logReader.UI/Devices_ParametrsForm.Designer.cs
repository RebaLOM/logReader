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
            buttonDisableAll = new Button();
            buttonEnableAll = new Button();
            panelButtons.SuspendLayout();
            SuspendLayout();
            // 
            // scrollPanel
            // 
            scrollPanel.AutoScroll = true;
            scrollPanel.Dock = DockStyle.Fill;
            scrollPanel.Location = new Point(0, 0);
            scrollPanel.Name = "scrollPanel";
            scrollPanel.Padding = new Padding(4);
            scrollPanel.TabIndex = 0;
            // 
            // panelButtons
            // 
            panelButtons.Controls.Add(buttonEnableAll);
            panelButtons.Controls.Add(buttonDisableAll);
            panelButtons.Dock = DockStyle.Bottom;
            panelButtons.Height = 48;
            panelButtons.Name = "panelButtons";
            panelButtons.BackColor = Color.FromArgb(240, 240, 240);
            panelButtons.TabIndex = 1;
            // 
            // buttonEnableAll
            // 
            buttonEnableAll.Location = new Point(12, 10);
            buttonEnableAll.Name = "buttonEnableAll";
            buttonEnableAll.Size = new Size(130, 28);
            buttonEnableAll.TabIndex = 0;
            buttonEnableAll.Text = "✔ Включить все";
            buttonEnableAll.UseVisualStyleBackColor = true;
            buttonEnableAll.Click += buttonEnableAll_Click;
            // 
            // buttonDisableAll
            // 
            buttonDisableAll.Location = new Point(150, 10);
            buttonDisableAll.Name = "buttonDisableAll";
            buttonDisableAll.Size = new Size(130, 28);
            buttonDisableAll.TabIndex = 1;
            buttonDisableAll.Text = "✖ Выключить все";
            buttonDisableAll.UseVisualStyleBackColor = true;
            buttonDisableAll.Click += buttonDisableAll_Click;
            // 
            // Devices_ParametrsForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(560, 600);
            MinimumSize = new Size(480, 400);
            Controls.Add(scrollPanel);
            Controls.Add(panelButtons);
            Name = "Devices_ParametrsForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Устройства и параметры";
            panelButtons.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private Panel scrollPanel;
        private Panel panelButtons;
        private Button buttonEnableAll;
        private Button buttonDisableAll;
    }
}