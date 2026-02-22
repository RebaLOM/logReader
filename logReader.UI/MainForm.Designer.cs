namespace logReader.UI
{
    partial class MainForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            labelCANlog = new Label();
            labelDevices = new Label();
            labelResult = new Label();
            labelFilterStatus = new Label();
            textBoxCanLog = new TextBox();
            textBoxDevices = new TextBox();
            textBoxOutput = new TextBox();
            buttonCANlog = new Button();
            buttonDevices = new Button();
            buttonOutput = new Button();
            textBoxLog = new TextBox();
            buttonProcess = new Button();
            buttonHelp = new Button();
            buttonDevicesParams = new Button();
            SuspendLayout();
            // 
            // labelCANlog
            // 
            labelCANlog.AutoSize = true;
            labelCANlog.Location = new Point(12, 9);
            labelCANlog.Name = "labelCANlog";
            labelCANlog.Text = "Файл CAN-логов (.csv)";
            // 
            // textBoxCanLog
            // 
            textBoxCanLog.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            textBoxCanLog.Location = new Point(12, 27);
            textBoxCanLog.Name = "textBoxCanLog";
            textBoxCanLog.Size = new Size(776, 23);
            // 
            // buttonCANlog
            // 
            buttonCANlog.Location = new Point(12, 56);
            buttonCANlog.Name = "buttonCANlog";
            buttonCANlog.Size = new Size(75, 23);
            buttonCANlog.Text = "Обзор";
            buttonCANlog.UseVisualStyleBackColor = true;
            buttonCANlog.Click += buttonCANlog_Click;
            // 
            // labelDevices
            // 
            labelDevices.AutoSize = true;
            labelDevices.Location = new Point(12, 92);
            labelDevices.Name = "labelDevices";
            labelDevices.Text = "Файл посылок (.xlsx)";
            // 
            // textBoxDevices
            // 
            textBoxDevices.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            textBoxDevices.Location = new Point(12, 110);
            textBoxDevices.Name = "textBoxDevices";
            textBoxDevices.TextChanged += textBoxDevices_TextChanged;
            textBoxDevices.Size = new Size(776, 23);
            // 
            // buttonDevices
            // 
            buttonDevices.Location = new Point(12, 139);
            buttonDevices.Name = "buttonDevices";
            buttonDevices.Size = new Size(75, 23);
            buttonDevices.Text = "Обзор";
            buttonDevices.UseVisualStyleBackColor = true;
            buttonDevices.Click += buttonDevices_Click;
            // 
            // labelResult
            // 
            labelResult.AutoSize = true;
            labelResult.Location = new Point(12, 175);
            labelResult.Name = "labelResult";
            labelResult.Text = "Сохранить в";
            // 
            // textBoxOutput
            // 
            textBoxOutput.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            textBoxOutput.Location = new Point(12, 193);
            textBoxOutput.Name = "textBoxOutput";
            textBoxOutput.Size = new Size(776, 23);
            // 
            // buttonOutput
            // 
            buttonOutput.Location = new Point(12, 222);
            buttonOutput.Name = "buttonOutput";
            buttonOutput.Size = new Size(75, 23);
            buttonOutput.Text = "Обзор";
            buttonOutput.UseVisualStyleBackColor = true;
            buttonOutput.Click += buttonOutput_Click;
            // 
            // labelFilterStatus — показывает сколько устройств/параметров отключено
            // 
            labelFilterStatus.AutoSize = true;
            labelFilterStatus.Location = new Point(389, 312);
            labelFilterStatus.Name = "labelFilterStatus";
            labelFilterStatus.Text = "Фильтры: не заданы";
            labelFilterStatus.ForeColor = Color.DarkGray;
            labelFilterStatus.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            // 
            // buttonProcess
            // 
            buttonProcess.Location = new Point(12, 308);
            buttonProcess.Name = "buttonProcess";
            buttonProcess.Size = new Size(94, 23);
            buttonProcess.Text = "Обработать";
            buttonProcess.UseVisualStyleBackColor = true;
            buttonProcess.Click += buttonProcess_Click;
            // 
            // buttonHelp
            // 
            buttonHelp.Location = new Point(112, 308);
            buttonHelp.Name = "buttonHelp";
            buttonHelp.Size = new Size(75, 23);
            buttonHelp.Text = "Помощь";
            buttonHelp.UseVisualStyleBackColor = true;
            buttonHelp.Click += buttonHelp_Click;
            // 
            // buttonDevicesParams
            // 
            buttonDevicesParams.Location = new Point(193, 308);
            buttonDevicesParams.Name = "buttonDevicesParams";
            buttonDevicesParams.Size = new Size(186, 23);
            buttonDevicesParams.Text = "Устройства и параметры";
            buttonDevicesParams.UseVisualStyleBackColor = true;
            buttonDevicesParams.Click += buttonDevicesParams_Click;
            // 
            // textBoxLog
            // 
            textBoxLog.Dock = DockStyle.Bottom;
            textBoxLog.Location = new Point(0, 350);
            textBoxLog.Multiline = true;
            textBoxLog.Name = "textBoxLog";
            textBoxLog.ReadOnly = true;
            textBoxLog.ScrollBars = ScrollBars.Vertical;
            textBoxLog.Size = new Size(800, 100);
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(labelFilterStatus);
            Controls.Add(buttonDevicesParams);
            Controls.Add(buttonHelp);
            Controls.Add(buttonProcess);
            Controls.Add(textBoxLog);
            Controls.Add(buttonOutput);
            Controls.Add(buttonDevices);
            Controls.Add(buttonCANlog);
            Controls.Add(textBoxOutput);
            Controls.Add(textBoxDevices);
            Controls.Add(textBoxCanLog);
            Controls.Add(labelResult);
            Controls.Add(labelDevices);
            Controls.Add(labelCANlog);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "MainForm";
            Text = "CanReader";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label labelCANlog;
        private Label labelDevices;
        private Label labelResult;
        private Label labelFilterStatus;
        private TextBox textBoxCanLog;
        private TextBox textBoxDevices;
        private TextBox textBoxOutput;
        private Button buttonCANlog;
        private Button buttonDevices;
        private Button buttonOutput;
        private TextBox textBoxLog;
        private Button buttonProcess;
        private Button buttonHelp;
        private Button buttonDevicesParams;
    }
}