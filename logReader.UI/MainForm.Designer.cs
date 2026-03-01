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
            buttonOpenOutput = new Button();
            textBoxCanLog = new TextBox();
            textBoxDevices = new TextBox();
            textBoxOutput = new TextBox();
            buttonCANlog = new Button();
            buttonViewLog = new Button();
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
            labelCANlog.Text = "Файл логов (.csv | .trc)";
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
            // buttonViewLog
            // 
            buttonViewLog.Location = new Point(93, 56);
            buttonViewLog.Name = "buttonViewLog";
            buttonViewLog.Size = new Size(100, 23);
            buttonViewLog.Text = "Посылки";
            buttonViewLog.UseVisualStyleBackColor = true;
            buttonViewLog.Click += buttonViewLog_Click;
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
            // buttonOpenOutput — появляется после успешной обработки
            // 
            buttonOpenOutput.Location = new Point(93, 222);
            buttonOpenOutput.Name = "buttonOpenOutput";
            buttonOpenOutput.Size = new Size(75, 23);
            buttonOpenOutput.Text = "Открыть";
            buttonOpenOutput.Visible = false;
            buttonOpenOutput.UseVisualStyleBackColor = true;
            buttonOpenOutput.Click += buttonOpenOutput_Click;
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
            textBoxOutput.TextChanged += textBoxOutput_TextChanged;
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
            labelFilterStatus.Location = new Point(285, 143);
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
            // buttonDevicesParams — рядом с кнопкой Обзор у файла посылок
            // 
            buttonDevicesParams.Location = new Point(93, 139);
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
            Controls.Add(buttonOpenOutput);
            Controls.Add(buttonOutput);
            Controls.Add(buttonDevices);
            Controls.Add(buttonViewLog);
            Controls.Add(buttonCANlog);
            Controls.Add(textBoxOutput);
            Controls.Add(textBoxDevices);
            Controls.Add(textBoxCanLog);
            Controls.Add(labelResult);
            Controls.Add(labelDevices);
            Controls.Add(labelCANlog);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "MainForm";
            Text = "LogReader";
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
        private Button buttonViewLog;
        private Button buttonDevices;
        private Button buttonOutput;
        private Button buttonOpenOutput;
        private TextBox textBoxLog;
        private Button buttonProcess;
        private Button buttonHelp;
        private Button buttonDevicesParams;
    }
}