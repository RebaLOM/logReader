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
            buttonEnableAll = new Button();
            buttonDisableAll = new Button();
            panelButtons.SuspendLayout();
            SuspendLayout();
            // 
            // panelButtons — прибит к низу, создаём первым чтобы Fill правильно отработал
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
            buttonDisableAll.Size = new Size(140, 28);
            buttonDisableAll.TabIndex = 1;
            buttonDisableAll.Text = "✖ Выключить все";
            buttonDisableAll.UseVisualStyleBackColor = true;
            buttonDisableAll.Click += buttonDisableAll_Click;
            // 
            // scrollPanel — Fill, занимает всё оставшееся пространство
            // 
            scrollPanel.AutoScroll = true;
            scrollPanel.Dock = DockStyle.Fill;
            scrollPanel.Name = "scrollPanel";
            scrollPanel.TabIndex = 0;
            // 
            // Devices_ParametrsForm
            // ВАЖНО: Bottom-docked панель должна быть добавлена ДО Fill-панели
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(580, 620);
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