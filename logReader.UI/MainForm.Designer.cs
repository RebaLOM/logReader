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
            labelComposites = new Label();
            labelResult = new Label();
            labelFilterStatus = new Label();
            buttonOpenOutput = new Button();
            textBoxCanLog = new TextBox();
            textBoxDevices = new TextBox();
            textBoxComposites = new TextBox();
            textBoxOutput = new TextBox();
            buttonCANlog = new Button();
            buttonViewLog = new Button();
            buttonDevices = new Button();
            buttonComposites = new Button();
            buttonCompositesCreateOrAdd = new Button();
            buttonOutput = new Button();
            textBoxLog = new TextBox();
            buttonProcess = new Button();
            buttonHelp = new Button();
            buttonTrcToAsc = new Button();
            buttonDevicesParams = new Button();
            buttonDevicesCreateOrAdd = new Button();
            buttonSaveOptions = new Button();
            SuspendLayout();
            // 
            // labelCANlog
            // 
            labelCANlog.AutoSize = true;
            labelCANlog.Location = new Point(12, 9);
            labelCANlog.Name = "labelCANlog";
            labelCANlog.Size = new Size(128, 15);
            labelCANlog.TabIndex = 15;
            labelCANlog.Text = "Файл или папка логов (.csv | .trc | .asc | .txt CANfox)";
            // 
            // labelDevices
            // 
            labelDevices.AutoSize = true;
            labelDevices.Location = new Point(12, 92);
            labelDevices.Name = "labelDevices";
            labelDevices.Size = new Size(120, 15);
            labelDevices.TabIndex = 14;
            labelDevices.Text = "Файл посылок (.xlsx | .dbc | .dbf)";
            // 
            // labelComposites
            // 
            labelComposites.AutoSize = true;
            labelComposites.Location = new Point(12, 175);
            labelComposites.Name = "labelComposites";
            labelComposites.Size = new Size(120, 15);
            labelComposites.TabIndex = 18;
            labelComposites.Text = "Файл составных параметров (.xlsx, опционально)";
            // 
            // labelResult
            // 
            labelResult.AutoSize = true;
            labelResult.Location = new Point(12, 258);
            labelResult.Name = "labelResult";
            labelResult.Size = new Size(74, 15);
            labelResult.TabIndex = 13;
            labelResult.Text = "Сохранить в";
            // 
            // labelFilterStatus
            // 
            labelFilterStatus.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            labelFilterStatus.AutoEllipsis = true;
            labelFilterStatus.AutoSize = false;
            labelFilterStatus.ForeColor = Color.DarkGray;
            labelFilterStatus.Location = new Point(285, 143);
            labelFilterStatus.Name = "labelFilterStatus";
            labelFilterStatus.Size = new Size(407, 15);
            labelFilterStatus.TabIndex = 22;
            labelFilterStatus.Text = "Фильтры: не заданы";
            // 
            // buttonOpenOutput
            // 
            buttonOpenOutput.Location = new Point(93, 305);
            buttonOpenOutput.Name = "buttonOpenOutput";
            buttonOpenOutput.Size = new Size(75, 23);
            buttonOpenOutput.TabIndex = 5;
            buttonOpenOutput.Text = "Открыть";
            buttonOpenOutput.UseVisualStyleBackColor = true;
            buttonOpenOutput.Visible = false;
            buttonOpenOutput.Click += buttonOpenOutput_Click;
            // 
            // textBoxCanLog
            // 
            textBoxCanLog.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            textBoxCanLog.Location = new Point(12, 27);
            textBoxCanLog.Name = "textBoxCanLog";
            textBoxCanLog.Size = new Size(776, 23);
            textBoxCanLog.TabIndex = 12;
            // 
            // textBoxDevices
            // 
            textBoxDevices.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            textBoxDevices.Location = new Point(12, 110);
            textBoxDevices.Name = "textBoxDevices";
            textBoxDevices.Size = new Size(776, 23);
            textBoxDevices.TabIndex = 11;
            textBoxDevices.TextChanged += textBoxDevices_TextChanged;
            // 
            // textBoxComposites
            // 
            textBoxComposites.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            textBoxComposites.Location = new Point(12, 193);
            textBoxComposites.Name = "textBoxComposites";
            textBoxComposites.Size = new Size(776, 23);
            textBoxComposites.TabIndex = 19;
            textBoxComposites.TextChanged += textBoxComposites_TextChanged;
            // 
            // textBoxOutput
            // 
            textBoxOutput.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            textBoxOutput.Location = new Point(12, 276);
            textBoxOutput.Name = "textBoxOutput";
            textBoxOutput.Size = new Size(776, 23);
            textBoxOutput.TabIndex = 10;
            textBoxOutput.TextChanged += textBoxOutput_TextChanged;
            // 
            // buttonCANlog
            // 
            buttonCANlog.Location = new Point(12, 56);
            buttonCANlog.Name = "buttonCANlog";
            buttonCANlog.Size = new Size(75, 23);
            buttonCANlog.TabIndex = 9;
            buttonCANlog.Text = "Обзор";
            buttonCANlog.UseVisualStyleBackColor = true;
            buttonCANlog.Click += buttonCANlog_Click;
            // 
            // buttonViewLog
            // 
            buttonViewLog.Location = new Point(93, 56);
            buttonViewLog.Name = "buttonViewLog";
            buttonViewLog.Size = new Size(100, 23);
            buttonViewLog.TabIndex = 8;
            buttonViewLog.Text = "Посылки";
            buttonViewLog.UseVisualStyleBackColor = true;
            buttonViewLog.Click += buttonViewLog_Click;
            // 
            // buttonDevices
            // 
            buttonDevices.Location = new Point(12, 139);
            buttonDevices.Name = "buttonDevices";
            buttonDevices.Size = new Size(75, 23);
            buttonDevices.TabIndex = 7;
            buttonDevices.Text = "Обзор";
            buttonDevices.UseVisualStyleBackColor = true;
            buttonDevices.Click += buttonDevices_Click;
            // 
            // buttonComposites
            // 
            buttonComposites.Location = new Point(12, 222);
            buttonComposites.Name = "buttonComposites";
            buttonComposites.Size = new Size(75, 23);
            buttonComposites.TabIndex = 20;
            buttonComposites.Text = "Обзор";
            buttonComposites.UseVisualStyleBackColor = true;
            buttonComposites.Click += buttonComposites_Click;
            // 
            // buttonCompositesCreateOrAdd
            // 
            buttonCompositesCreateOrAdd.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            buttonCompositesCreateOrAdd.Location = new Point(698, 222);
            buttonCompositesCreateOrAdd.Name = "buttonCompositesCreateOrAdd";
            buttonCompositesCreateOrAdd.Size = new Size(90, 23);
            buttonCompositesCreateOrAdd.TabIndex = 21;
            buttonCompositesCreateOrAdd.Text = "Создать .xlsx";
            buttonCompositesCreateOrAdd.UseVisualStyleBackColor = true;
            buttonCompositesCreateOrAdd.Click += buttonCompositesCreateOrAdd_Click;
            // 
            // buttonOutput
            // 
            buttonOutput.Location = new Point(12, 305);
            buttonOutput.Name = "buttonOutput";
            buttonOutput.Size = new Size(75, 23);
            buttonOutput.TabIndex = 6;
            buttonOutput.Text = "Обзор";
            buttonOutput.UseVisualStyleBackColor = true;
            buttonOutput.Click += buttonOutput_Click;
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
            textBoxLog.TabIndex = 4;
            // 
            // buttonProcess
            // 
            buttonProcess.Location = new Point(12, 391);
            buttonProcess.Name = "buttonProcess";
            buttonProcess.Size = new Size(94, 23);
            buttonProcess.TabIndex = 3;
            buttonProcess.Text = "Обработать";
            buttonProcess.UseVisualStyleBackColor = true;
            buttonProcess.Click += buttonProcess_Click;
            // 
            // buttonHelp
            // 
            buttonHelp.Location = new Point(112, 391);
            buttonHelp.Name = "buttonHelp";
            buttonHelp.Size = new Size(75, 23);
            buttonHelp.TabIndex = 2;
            buttonHelp.Text = "Помощь";
            buttonHelp.UseVisualStyleBackColor = true;
            buttonHelp.Click += buttonHelp_Click;
            // 
            // buttonTrcToAsc
            // 
            buttonTrcToAsc.Location = new Point(200, 391);
            buttonTrcToAsc.Name = "buttonTrcToAsc";
            buttonTrcToAsc.Size = new Size(120, 23);
            buttonTrcToAsc.TabIndex = 17;
            buttonTrcToAsc.Text = "Смена формата";
            buttonTrcToAsc.UseVisualStyleBackColor = true;
            buttonTrcToAsc.Click += buttonFormatConvert_Click;
            // 
            // buttonDevicesParams
            // 
            buttonDevicesParams.Location = new Point(93, 139);
            buttonDevicesParams.Name = "buttonDevicesParams";
            buttonDevicesParams.Size = new Size(186, 23);
            buttonDevicesParams.TabIndex = 1;
            buttonDevicesParams.Text = "Устройства и параметры";
            buttonDevicesParams.UseVisualStyleBackColor = true;
            buttonDevicesParams.Click += buttonDevicesParams_Click;
            // 
            // buttonDevicesCreateOrAdd
            // 
            buttonDevicesCreateOrAdd.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            buttonDevicesCreateOrAdd.Location = new Point(698, 139);
            buttonDevicesCreateOrAdd.Name = "buttonDevicesCreateOrAdd";
            buttonDevicesCreateOrAdd.Size = new Size(90, 23);
            buttonDevicesCreateOrAdd.TabIndex = 16;
            buttonDevicesCreateOrAdd.Text = "Создать .xlsx";
            buttonDevicesCreateOrAdd.UseVisualStyleBackColor = true;
            buttonDevicesCreateOrAdd.Click += buttonDevicesCreateOrAdd_Click;
            // 
            // buttonSaveOptions
            // 
            buttonSaveOptions.Location = new Point(12, 334);
            buttonSaveOptions.Name = "buttonSaveOptions";
            buttonSaveOptions.Size = new Size(186, 23);
            buttonSaveOptions.TabIndex = 0;
            buttonSaveOptions.Text = "Параметры сохранения";
            buttonSaveOptions.UseVisualStyleBackColor = true;
            buttonSaveOptions.Click += buttonSaveOptions_Click;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 533);
            Controls.Add(buttonSaveOptions);
            Controls.Add(labelFilterStatus);
            Controls.Add(buttonDevicesCreateOrAdd);
            Controls.Add(buttonDevicesParams);
            Controls.Add(buttonTrcToAsc);
            Controls.Add(buttonHelp);
            Controls.Add(buttonProcess);
            Controls.Add(textBoxLog);
            Controls.Add(buttonOpenOutput);
            Controls.Add(buttonOutput);
            Controls.Add(buttonCompositesCreateOrAdd);
            Controls.Add(buttonComposites);
            Controls.Add(buttonDevices);
            Controls.Add(buttonViewLog);
            Controls.Add(buttonCANlog);
            Controls.Add(textBoxOutput);
            Controls.Add(textBoxComposites);
            Controls.Add(textBoxDevices);
            Controls.Add(textBoxCanLog);
            Controls.Add(labelResult);
            Controls.Add(labelComposites);
            Controls.Add(labelDevices);
            Controls.Add(labelCANlog);
            Icon = (Icon)resources.GetObject("$this.Icon");
            MinimumSize = new Size(600, 503);
            Name = "MainForm";
            Text = "LOGER";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label labelCANlog;
        private Label labelDevices;
        private Label labelComposites;
        private Label labelResult;
        private Label labelFilterStatus;
        private TextBox textBoxCanLog;
        private TextBox textBoxDevices;
        private TextBox textBoxComposites;
        private TextBox textBoxOutput;
        private Button buttonCANlog;
        private Button buttonViewLog;
        private Button buttonDevices;
        private Button buttonComposites;
        private Button buttonCompositesCreateOrAdd;
        private Button buttonOutput;
        private Button buttonOpenOutput;
        private TextBox textBoxLog;
        private Button buttonProcess;
        private Button buttonHelp;
        private Button buttonTrcToAsc;
        private Button buttonDevicesParams;
        private Button buttonDevicesCreateOrAdd;
        private Button buttonSaveOptions;
    }
}
