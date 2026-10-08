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
            contentSplit = new SplitContainer();
            workRoot = new Panel();
            headerPanel = new Panel();
            brandAccent = new Panel();
            labelBrand = new Label();
            buttonThemeToggle = new Button();
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
            buttonCancel = new Button();
            progressBarProcess = new ProgressBar();
            labelProgress = new Label();
            ((System.ComponentModel.ISupportInitialize)contentSplit).BeginInit();
            contentSplit.Panel1.SuspendLayout();
            contentSplit.Panel2.SuspendLayout();
            contentSplit.SuspendLayout();
            workRoot.SuspendLayout();
            headerPanel.SuspendLayout();
            SuspendLayout();
            // 
            // contentSplit
            // 
            contentSplit.Dock = DockStyle.Fill;
            contentSplit.FixedPanel = FixedPanel.None;
            contentSplit.Location = new Point(0, 0);
            contentSplit.Name = "contentSplit";
            contentSplit.Orientation = Orientation.Horizontal;
            contentSplit.Panel1.AutoScroll = true;
            contentSplit.Panel1.Controls.Add(workRoot);
            contentSplit.Panel1MinSize = 420;
            contentSplit.Panel2.Controls.Add(textBoxLog);
            contentSplit.Panel2MinSize = 80;
            contentSplit.Size = new Size(800, 560);
            contentSplit.SplitterDistance = 450;
            contentSplit.SplitterWidth = 6;
            contentSplit.TabIndex = 0;
            contentSplit.TabStop = false;
            // 
            // workRoot
            // 
            workRoot.Controls.Add(labelProgress);
            workRoot.Controls.Add(progressBarProcess);
            workRoot.Controls.Add(buttonCancel);
            workRoot.Controls.Add(buttonSaveOptions);
            workRoot.Controls.Add(labelFilterStatus);
            workRoot.Controls.Add(buttonDevicesCreateOrAdd);
            workRoot.Controls.Add(buttonDevicesParams);
            workRoot.Controls.Add(buttonTrcToAsc);
            workRoot.Controls.Add(buttonHelp);
            workRoot.Controls.Add(buttonProcess);
            workRoot.Controls.Add(buttonOpenOutput);
            workRoot.Controls.Add(buttonOutput);
            workRoot.Controls.Add(buttonCompositesCreateOrAdd);
            workRoot.Controls.Add(buttonComposites);
            workRoot.Controls.Add(buttonDevices);
            workRoot.Controls.Add(buttonViewLog);
            workRoot.Controls.Add(buttonCANlog);
            workRoot.Controls.Add(textBoxOutput);
            workRoot.Controls.Add(textBoxComposites);
            workRoot.Controls.Add(textBoxDevices);
            workRoot.Controls.Add(textBoxCanLog);
            workRoot.Controls.Add(labelResult);
            workRoot.Controls.Add(labelComposites);
            workRoot.Controls.Add(labelDevices);
            workRoot.Controls.Add(labelCANlog);
            workRoot.Controls.Add(headerPanel);
            workRoot.Dock = DockStyle.Top;
            workRoot.Location = new Point(0, 0);
            workRoot.Name = "workRoot";
            workRoot.Size = new Size(800, 448);
            // 
            // headerPanel
            // 
            headerPanel.Controls.Add(buttonThemeToggle);
            headerPanel.Controls.Add(labelBrand);
            headerPanel.Controls.Add(brandAccent);
            headerPanel.Dock = DockStyle.Top;
            headerPanel.Height = 44;
            headerPanel.Name = "headerPanel";
            // 
            // brandAccent
            // 
            brandAccent.Location = new Point(12, 10);
            brandAccent.Name = "brandAccent";
            brandAccent.Size = new Size(4, 24);
            // 
            // labelBrand
            // 
            labelBrand.AutoSize = true;
            labelBrand.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            labelBrand.Location = new Point(24, 8);
            labelBrand.Name = "labelBrand";
            labelBrand.Size = new Size(72, 25);
            labelBrand.Text = "LOGER";
            // 
            // buttonThemeToggle
            // 
            buttonThemeToggle.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            buttonThemeToggle.Location = new Point(668, 8);
            buttonThemeToggle.Name = "buttonThemeToggle";
            buttonThemeToggle.Size = new Size(120, 28);
            buttonThemeToggle.TabIndex = 50;
            buttonThemeToggle.Text = "Светлая тема";
            buttonThemeToggle.Click += buttonThemeToggle_Click;
            // 
            // labelCANlog
            // 
            labelCANlog.AutoSize = true;
            labelCANlog.Location = new Point(12, 56);
            labelCANlog.Name = "labelCANlog";
            labelCANlog.Size = new Size(128, 15);
            labelCANlog.TabIndex = 100;
            labelCANlog.Text = "Файл или папка логов (.csv | .trc | .asc | .txt CANfox)";
            // 
            // labelDevices
            // 
            labelDevices.AutoSize = true;
            labelDevices.Location = new Point(12, 139);
            labelDevices.Name = "labelDevices";
            labelDevices.Size = new Size(120, 15);
            labelDevices.TabIndex = 101;
            labelDevices.Text = "Файл посылок (.xlsx | .dbc | .dbf)";
            // 
            // labelComposites
            // 
            labelComposites.AutoSize = true;
            labelComposites.Location = new Point(12, 222);
            labelComposites.Name = "labelComposites";
            labelComposites.Size = new Size(120, 15);
            labelComposites.TabIndex = 102;
            labelComposites.Text = "Файл составных параметров (.xlsx, опционально)";
            // 
            // labelResult
            // 
            labelResult.AutoSize = true;
            labelResult.Location = new Point(12, 305);
            labelResult.Name = "labelResult";
            labelResult.Size = new Size(74, 15);
            labelResult.TabIndex = 103;
            labelResult.Text = "Сохранить в";
            // 
            // labelFilterStatus
            // 
            labelFilterStatus.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            labelFilterStatus.AutoEllipsis = true;
            labelFilterStatus.AutoSize = false;
            labelFilterStatus.Location = new Point(285, 190);
            labelFilterStatus.Name = "labelFilterStatus";
            labelFilterStatus.Size = new Size(407, 15);
            labelFilterStatus.TabIndex = 104;
            labelFilterStatus.Text = "Фильтры: не заданы";
            // 
            // buttonOpenOutput
            // 
            buttonOpenOutput.Location = new Point(93, 352);
            buttonOpenOutput.Name = "buttonOpenOutput";
            buttonOpenOutput.Size = new Size(75, 28);
            buttonOpenOutput.TabIndex = 12;
            buttonOpenOutput.Text = "Открыть";
            buttonOpenOutput.Visible = false;
            buttonOpenOutput.Click += buttonOpenOutput_Click;
            // 
            // textBoxCanLog
            // 
            textBoxCanLog.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            textBoxCanLog.Location = new Point(12, 74);
            textBoxCanLog.Name = "textBoxCanLog";
            textBoxCanLog.Size = new Size(776, 23);
            textBoxCanLog.TabIndex = 0;
            // 
            // textBoxDevices
            // 
            textBoxDevices.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            textBoxDevices.Location = new Point(12, 157);
            textBoxDevices.Name = "textBoxDevices";
            textBoxDevices.Size = new Size(776, 23);
            textBoxDevices.TabIndex = 3;
            textBoxDevices.TextChanged += textBoxDevices_TextChanged;
            // 
            // textBoxComposites
            // 
            textBoxComposites.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            textBoxComposites.Location = new Point(12, 240);
            textBoxComposites.Name = "textBoxComposites";
            textBoxComposites.Size = new Size(776, 23);
            textBoxComposites.TabIndex = 7;
            textBoxComposites.TextChanged += textBoxComposites_TextChanged;
            // 
            // textBoxOutput
            // 
            textBoxOutput.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            textBoxOutput.Location = new Point(12, 323);
            textBoxOutput.Name = "textBoxOutput";
            textBoxOutput.Size = new Size(776, 23);
            textBoxOutput.TabIndex = 10;
            textBoxOutput.TextChanged += textBoxOutput_TextChanged;
            // 
            // buttonCANlog
            // 
            buttonCANlog.Location = new Point(12, 103);
            buttonCANlog.Name = "buttonCANlog";
            buttonCANlog.Size = new Size(75, 28);
            buttonCANlog.TabIndex = 1;
            buttonCANlog.Text = "Обзор";
            buttonCANlog.Click += buttonCANlog_Click;
            // 
            // buttonViewLog
            // 
            buttonViewLog.Location = new Point(93, 103);
            buttonViewLog.Name = "buttonViewLog";
            buttonViewLog.Size = new Size(100, 28);
            buttonViewLog.TabIndex = 2;
            buttonViewLog.Text = "Посылки";
            buttonViewLog.Click += buttonViewLog_Click;
            // 
            // buttonDevices
            // 
            buttonDevices.Location = new Point(12, 186);
            buttonDevices.Name = "buttonDevices";
            buttonDevices.Size = new Size(75, 28);
            buttonDevices.TabIndex = 4;
            buttonDevices.Text = "Обзор";
            buttonDevices.Click += buttonDevices_Click;
            // 
            // buttonComposites
            // 
            buttonComposites.Location = new Point(12, 269);
            buttonComposites.Name = "buttonComposites";
            buttonComposites.Size = new Size(75, 28);
            buttonComposites.TabIndex = 8;
            buttonComposites.Text = "Обзор";
            buttonComposites.Click += buttonComposites_Click;
            // 
            // buttonCompositesCreateOrAdd
            // 
            buttonCompositesCreateOrAdd.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            buttonCompositesCreateOrAdd.Location = new Point(698, 269);
            buttonCompositesCreateOrAdd.Name = "buttonCompositesCreateOrAdd";
            buttonCompositesCreateOrAdd.Size = new Size(90, 28);
            buttonCompositesCreateOrAdd.TabIndex = 9;
            buttonCompositesCreateOrAdd.Text = "Создать .xlsx";
            buttonCompositesCreateOrAdd.Click += buttonCompositesCreateOrAdd_Click;
            // 
            // buttonOutput
            // 
            buttonOutput.Location = new Point(12, 352);
            buttonOutput.Name = "buttonOutput";
            buttonOutput.Size = new Size(75, 28);
            buttonOutput.TabIndex = 11;
            buttonOutput.Text = "Обзор";
            buttonOutput.Click += buttonOutput_Click;
            // 
            // textBoxLog
            // 
            textBoxLog.Dock = DockStyle.Fill;
            textBoxLog.Location = new Point(0, 0);
            textBoxLog.Multiline = true;
            textBoxLog.Name = "textBoxLog";
            textBoxLog.ReadOnly = true;
            textBoxLog.ScrollBars = ScrollBars.Vertical;
            textBoxLog.Size = new Size(800, 104);
            textBoxLog.TabIndex = 18;
            // 
            // buttonProcess
            // 
            buttonProcess.Location = new Point(12, 412);
            buttonProcess.Name = "buttonProcess";
            buttonProcess.Size = new Size(110, 28);
            buttonProcess.TabIndex = 14;
            buttonProcess.Text = "Обработать";
            buttonProcess.Click += buttonProcess_Click;
            // 
            // buttonHelp
            // 
            buttonHelp.Location = new Point(128, 412);
            buttonHelp.Name = "buttonHelp";
            buttonHelp.Size = new Size(75, 28);
            buttonHelp.TabIndex = 16;
            buttonHelp.Text = "Помощь";
            buttonHelp.Click += buttonHelp_Click;
            // 
            // buttonTrcToAsc
            // 
            buttonTrcToAsc.Location = new Point(209, 412);
            buttonTrcToAsc.Name = "buttonTrcToAsc";
            buttonTrcToAsc.Size = new Size(120, 28);
            buttonTrcToAsc.TabIndex = 17;
            buttonTrcToAsc.Text = "Смена формата";
            buttonTrcToAsc.Click += buttonFormatConvert_Click;
            // 
            // buttonDevicesParams
            // 
            buttonDevicesParams.Location = new Point(93, 186);
            buttonDevicesParams.Name = "buttonDevicesParams";
            buttonDevicesParams.Size = new Size(186, 28);
            buttonDevicesParams.TabIndex = 5;
            buttonDevicesParams.Text = "Устройства и параметры";
            buttonDevicesParams.Click += buttonDevicesParams_Click;
            // 
            // buttonDevicesCreateOrAdd
            // 
            buttonDevicesCreateOrAdd.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            buttonDevicesCreateOrAdd.Location = new Point(698, 186);
            buttonDevicesCreateOrAdd.Name = "buttonDevicesCreateOrAdd";
            buttonDevicesCreateOrAdd.Size = new Size(90, 28);
            buttonDevicesCreateOrAdd.TabIndex = 6;
            buttonDevicesCreateOrAdd.Text = "Создать .xlsx";
            buttonDevicesCreateOrAdd.Click += buttonDevicesCreateOrAdd_Click;
            // 
            // buttonSaveOptions
            // 
            buttonSaveOptions.Location = new Point(174, 352);
            buttonSaveOptions.Name = "buttonSaveOptions";
            buttonSaveOptions.Size = new Size(186, 28);
            buttonSaveOptions.TabIndex = 13;
            buttonSaveOptions.Text = "Параметры сохранения";
            buttonSaveOptions.Click += buttonSaveOptions_Click;
            // 
            // progressBarProcess
            // 
            progressBarProcess.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            progressBarProcess.Location = new Point(12, 388);
            progressBarProcess.Maximum = 1000;
            progressBarProcess.Name = "progressBarProcess";
            progressBarProcess.Size = new Size(776, 14);
            progressBarProcess.TabStop = false;
            progressBarProcess.Visible = false;
            // 
            // buttonCancel
            // 
            buttonCancel.Enabled = false;
            buttonCancel.Location = new Point(335, 412);
            buttonCancel.Name = "buttonCancel";
            buttonCancel.Size = new Size(90, 28);
            buttonCancel.TabIndex = 15;
            buttonCancel.Text = "Отмена";
            buttonCancel.Visible = false;
            buttonCancel.Click += buttonCancel_Click;
            // 
            // labelProgress
            // 
            labelProgress.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            labelProgress.AutoEllipsis = true;
            labelProgress.Location = new Point(431, 416);
            labelProgress.Name = "labelProgress";
            labelProgress.Size = new Size(357, 20);
            labelProgress.Visible = false;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 560);
            Controls.Add(contentSplit);
            Icon = (Icon)resources.GetObject("$this.Icon");
            MinimumSize = new Size(640, 520);
            Name = "MainForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "LOGER";
            contentSplit.Panel1.ResumeLayout(false);
            contentSplit.Panel2.ResumeLayout(false);
            contentSplit.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)contentSplit).EndInit();
            contentSplit.ResumeLayout(false);
            workRoot.ResumeLayout(false);
            workRoot.PerformLayout();
            headerPanel.ResumeLayout(false);
            headerPanel.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private SplitContainer contentSplit;
        private Panel workRoot;
        private Panel headerPanel;
        private Panel brandAccent;
        private Label labelBrand;
        private Button buttonThemeToggle;
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
        private Button buttonCancel;
        private ProgressBar progressBarProcess;
        private Label labelProgress;
    }
}
