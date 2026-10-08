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
            rootLayout = new TableLayoutPanel();
            headerPanel = new Panel();
            brandAccent = new Panel();
            labelBrand = new Label();
            buttonThemeToggle = new Button();
            bodyLayout = new TableLayoutPanel();
            navPanel = new Panel();
            navProcess = new Controls.NavigationItem();
            navHelp = new Controls.NavigationItem();
            navConvert = new Controls.NavigationItem();
            contentSplit = new SplitContainer();
            workScroll = new Panel();
            cardsHost = new Panel();
            cardLogs = new Controls.ModernCard();
            labelCANlog = new Label();
            textBoxCanLog = new TextBox();
            buttonCANlog = new Button();
            buttonViewLog = new Button();
            cardDevices = new Controls.ModernCard();
            labelDevices = new Label();
            textBoxDevices = new TextBox();
            buttonDevices = new Button();
            buttonDevicesParams = new Button();
            buttonDevicesCreateOrAdd = new Button();
            labelFilterStatus = new Label();
            cardComposites = new Controls.ModernCard();
            labelComposites = new Label();
            textBoxComposites = new TextBox();
            buttonComposites = new Button();
            buttonCompositesCreateOrAdd = new Button();
            cardOutput = new Controls.ModernCard();
            labelResult = new Label();
            textBoxOutput = new TextBox();
            buttonOutput = new Button();
            buttonOpenOutput = new Button();
            buttonSaveOptions = new Button();
            actionBar = new Panel();
            buttonProcess = new Button();
            buttonCancel = new Button();
            buttonHelp = new Button();
            buttonTrcToAsc = new Button();
            progressBarProcess = new ProgressBar();
            labelProgress = new Label();
            textBoxLog = new TextBox();
            statusBar = new Panel();
            statusBadge = new Controls.StatusBadge();
            rootLayout.SuspendLayout();
            headerPanel.SuspendLayout();
            bodyLayout.SuspendLayout();
            navPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)contentSplit).BeginInit();
            contentSplit.Panel1.SuspendLayout();
            contentSplit.Panel2.SuspendLayout();
            contentSplit.SuspendLayout();
            workScroll.SuspendLayout();
            cardsHost.SuspendLayout();
            cardLogs.SuspendLayout();
            cardDevices.SuspendLayout();
            cardComposites.SuspendLayout();
            cardOutput.SuspendLayout();
            actionBar.SuspendLayout();
            statusBar.SuspendLayout();
            SuspendLayout();
            // 
            // rootLayout
            // 
            rootLayout.ColumnCount = 1;
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            rootLayout.Controls.Add(headerPanel, 0, 0);
            rootLayout.Controls.Add(bodyLayout, 0, 1);
            rootLayout.Controls.Add(statusBar, 0, 2);
            rootLayout.Dock = DockStyle.Fill;
            rootLayout.Name = "rootLayout";
            rootLayout.RowCount = 3;
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            // 
            // headerPanel
            // 
            headerPanel.Controls.Add(buttonThemeToggle);
            headerPanel.Controls.Add(labelBrand);
            headerPanel.Controls.Add(brandAccent);
            headerPanel.Dock = DockStyle.Fill;
            headerPanel.Name = "headerPanel";
            headerPanel.Padding = new Padding(16, 10, 16, 10);
            // 
            // brandAccent
            // 
            brandAccent.Location = new Point(16, 14);
            brandAccent.Name = "brandAccent";
            brandAccent.Size = new Size(4, 24);
            // 
            // labelBrand
            // 
            labelBrand.AutoSize = true;
            labelBrand.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            labelBrand.Location = new Point(28, 10);
            labelBrand.Name = "labelBrand";
            labelBrand.Text = "LOGER";
            // 
            // buttonThemeToggle — позиция в LayoutHeader(); справа в шапке рядом с brand.
            // 
            buttonThemeToggle.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            buttonThemeToggle.Location = new Point(748, 10);
            buttonThemeToggle.AccessibleName = "Тёмная тема";
            buttonThemeToggle.Name = "buttonThemeToggle";
            buttonThemeToggle.Size = new Size(130, 28);
            buttonThemeToggle.TabIndex = 50;
            buttonThemeToggle.Text = "Тёмная тема";
            buttonThemeToggle.Click += buttonThemeToggle_Click;
            // 
            // bodyLayout
            // 
            bodyLayout.ColumnCount = 2;
            bodyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 148F));
            bodyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            bodyLayout.Controls.Add(navPanel, 0, 0);
            bodyLayout.Controls.Add(contentSplit, 1, 0);
            bodyLayout.Dock = DockStyle.Fill;
            bodyLayout.Name = "bodyLayout";
            bodyLayout.RowCount = 1;
            bodyLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            // 
            // navPanel
            // 
            // Порядок Add + Dock.Top: последний добавленный — сверху.
            navPanel.Controls.Add(navConvert);
            navPanel.Controls.Add(navHelp);
            navPanel.Controls.Add(navProcess);
            navPanel.Dock = DockStyle.Fill;
            navPanel.Name = "navPanel";
            navPanel.Padding = new Padding(8, 12, 8, 8);
            // 
            // navProcess — Name = AutomationId; AccessibleName = видимый текст.
            // 
            navProcess.AccessibleName = "Обработка";
            navProcess.Dock = DockStyle.Top;
            navProcess.Name = "navProcess";
            navProcess.Selected = true;
            navProcess.Text = "Обработка";
            navProcess.Click += navProcess_Click;
            // 
            // navHelp
            // 
            navHelp.AccessibleName = "Справка";
            navHelp.Dock = DockStyle.Top;
            navHelp.Name = "navHelp";
            navHelp.Text = "Справка";
            navHelp.Click += navHelp_Click;
            // 
            // navConvert
            // 
            navConvert.AccessibleName = "Конвертация";
            navConvert.Dock = DockStyle.Top;
            navConvert.Name = "navConvert";
            navConvert.Text = "Конвертация";
            navConvert.Click += navConvert_Click;
            // 
            // contentSplit
            // 
            contentSplit.Dock = DockStyle.Fill;
            contentSplit.Orientation = Orientation.Horizontal;
            contentSplit.Panel1.Controls.Add(workScroll);
            contentSplit.Panel1MinSize = 320;
            contentSplit.Panel2.Controls.Add(textBoxLog);
            contentSplit.Panel2MinSize = 80;
            contentSplit.Name = "contentSplit";
            contentSplit.SplitterDistance = 420;
            contentSplit.SplitterWidth = 6;
            contentSplit.TabStop = false;
            // 
            // workScroll
            // 
            workScroll.AutoScroll = true;
            workScroll.Controls.Add(cardsHost);
            workScroll.Dock = DockStyle.Fill;
            workScroll.Name = "workScroll";
            workScroll.Padding = new Padding(12, 8, 12, 8);
            // 
            // cardsHost
            // 
            cardsHost.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            cardsHost.Controls.Add(actionBar);
            cardsHost.Controls.Add(cardOutput);
            cardsHost.Controls.Add(cardComposites);
            cardsHost.Controls.Add(cardDevices);
            cardsHost.Controls.Add(cardLogs);
            cardsHost.Location = new Point(12, 8);
            cardsHost.Name = "cardsHost";
            cardsHost.Size = new Size(700, 520);
            // 
            // cardLogs
            // 
            cardLogs.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            cardLogs.Controls.Add(buttonViewLog);
            cardLogs.Controls.Add(buttonCANlog);
            cardLogs.Controls.Add(textBoxCanLog);
            cardLogs.Controls.Add(labelCANlog);
            cardLogs.Location = new Point(0, 0);
            cardLogs.Name = "cardLogs";
            cardLogs.Size = new Size(700, 100);
            cardLogs.Title = "1. Источник логов";
            // 
            // labelCANlog
            // 
            labelCANlog.AutoSize = true;
            labelCANlog.Location = new Point(16, 36);
            labelCANlog.Name = "labelCANlog";
            labelCANlog.Text = "Файл или папка (.csv | .trc | .asc | .txt CANfox)";
            // 
            // textBoxCanLog
            // 
            textBoxCanLog.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            textBoxCanLog.Location = new Point(16, 56);
            textBoxCanLog.Name = "textBoxCanLog";
            textBoxCanLog.Size = new Size(520, 23);
            textBoxCanLog.TabIndex = 0;
            // 
            // buttonCANlog
            // 
            buttonCANlog.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            buttonCANlog.Location = new Point(544, 54);
            buttonCANlog.Name = "buttonCANlog";
            buttonCANlog.Size = new Size(72, 28);
            buttonCANlog.TabIndex = 1;
            buttonCANlog.Text = "Обзор";
            buttonCANlog.Click += buttonCANlog_Click;
            // 
            // buttonViewLog
            // 
            buttonViewLog.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            buttonViewLog.Location = new Point(620, 54);
            buttonViewLog.Name = "buttonViewLog";
            buttonViewLog.Size = new Size(64, 28);
            buttonViewLog.TabIndex = 2;
            buttonViewLog.Text = "Посылки";
            buttonViewLog.Click += buttonViewLog_Click;
            // 
            // cardDevices
            // 
            cardDevices.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            cardDevices.Controls.Add(labelFilterStatus);
            cardDevices.Controls.Add(buttonDevicesCreateOrAdd);
            cardDevices.Controls.Add(buttonDevicesParams);
            cardDevices.Controls.Add(buttonDevices);
            cardDevices.Controls.Add(textBoxDevices);
            cardDevices.Controls.Add(labelDevices);
            cardDevices.Location = new Point(0, 108);
            cardDevices.Name = "cardDevices";
            cardDevices.Size = new Size(700, 124);
            cardDevices.Title = "2. Посылки и фильтры";
            // 
            // labelDevices
            // 
            labelDevices.AutoSize = true;
            labelDevices.Location = new Point(16, 36);
            labelDevices.Name = "labelDevices";
            labelDevices.Text = "Файл посылок (.xlsx | .dbc | .dbf)";
            // 
            // textBoxDevices
            // 
            textBoxDevices.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            textBoxDevices.Location = new Point(16, 56);
            textBoxDevices.Name = "textBoxDevices";
            textBoxDevices.Size = new Size(520, 23);
            textBoxDevices.TabIndex = 3;
            textBoxDevices.TextChanged += textBoxDevices_TextChanged;
            // 
            // buttonDevices
            // 
            buttonDevices.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            buttonDevices.Location = new Point(544, 54);
            buttonDevices.Name = "buttonDevices";
            buttonDevices.Size = new Size(72, 28);
            buttonDevices.TabIndex = 4;
            buttonDevices.Text = "Обзор";
            buttonDevices.Click += buttonDevices_Click;
            // 
            // buttonDevicesParams
            // 
            buttonDevicesParams.AccessibleName = "Устройства и параметры";
            buttonDevicesParams.Location = new Point(16, 86);
            buttonDevicesParams.Name = "buttonDevicesParams";
            buttonDevicesParams.Size = new Size(190, 28);
            buttonDevicesParams.TabIndex = 5;
            buttonDevicesParams.Text = "Устройства и параметры";
            buttonDevicesParams.Click += buttonDevicesParams_Click;
            // 
            // buttonDevicesCreateOrAdd
            // 
            buttonDevicesCreateOrAdd.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            buttonDevicesCreateOrAdd.Location = new Point(620, 54);
            buttonDevicesCreateOrAdd.Name = "buttonDevicesCreateOrAdd";
            buttonDevicesCreateOrAdd.Size = new Size(64, 28);
            buttonDevicesCreateOrAdd.TabIndex = 6;
            buttonDevicesCreateOrAdd.Text = "Создать";
            buttonDevicesCreateOrAdd.Click += buttonDevicesCreateOrAdd_Click;
            // 
            // labelFilterStatus
            // 
            labelFilterStatus.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            labelFilterStatus.AutoEllipsis = true;
            labelFilterStatus.Location = new Point(196, 92);
            labelFilterStatus.Name = "labelFilterStatus";
            labelFilterStatus.Size = new Size(488, 16);
            labelFilterStatus.Text = "Фильтры: не заданы";
            // 
            // cardComposites
            // 
            cardComposites.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            cardComposites.Controls.Add(buttonCompositesCreateOrAdd);
            cardComposites.Controls.Add(buttonComposites);
            cardComposites.Controls.Add(textBoxComposites);
            cardComposites.Controls.Add(labelComposites);
            cardComposites.Location = new Point(0, 240);
            cardComposites.Name = "cardComposites";
            cardComposites.Size = new Size(700, 100);
            cardComposites.Title = "3. Составные параметры (опционально)";
            // 
            // labelComposites
            // 
            labelComposites.AutoSize = true;
            labelComposites.Location = new Point(16, 36);
            labelComposites.Name = "labelComposites";
            labelComposites.Text = "Файл составных параметров (.xlsx)";
            // 
            // textBoxComposites
            // 
            textBoxComposites.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            textBoxComposites.Location = new Point(16, 56);
            textBoxComposites.Name = "textBoxComposites";
            textBoxComposites.Size = new Size(520, 23);
            textBoxComposites.TabIndex = 7;
            textBoxComposites.TextChanged += textBoxComposites_TextChanged;
            // 
            // buttonComposites
            // 
            buttonComposites.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            buttonComposites.Location = new Point(544, 54);
            buttonComposites.Name = "buttonComposites";
            buttonComposites.Size = new Size(72, 28);
            buttonComposites.TabIndex = 8;
            buttonComposites.Text = "Обзор";
            buttonComposites.Click += buttonComposites_Click;
            // 
            // buttonCompositesCreateOrAdd
            // 
            buttonCompositesCreateOrAdd.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            buttonCompositesCreateOrAdd.Location = new Point(620, 54);
            buttonCompositesCreateOrAdd.Name = "buttonCompositesCreateOrAdd";
            buttonCompositesCreateOrAdd.Size = new Size(64, 28);
            buttonCompositesCreateOrAdd.TabIndex = 9;
            buttonCompositesCreateOrAdd.Text = "Создать";
            buttonCompositesCreateOrAdd.Click += buttonCompositesCreateOrAdd_Click;
            // 
            // cardOutput
            // 
            cardOutput.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            cardOutput.Controls.Add(buttonSaveOptions);
            cardOutput.Controls.Add(buttonOpenOutput);
            cardOutput.Controls.Add(buttonOutput);
            cardOutput.Controls.Add(textBoxOutput);
            cardOutput.Controls.Add(labelResult);
            cardOutput.Location = new Point(0, 348);
            cardOutput.Name = "cardOutput";
            cardOutput.Size = new Size(700, 100);
            cardOutput.Title = "4. Выходной файл";
            // 
            // labelResult
            // 
            labelResult.AutoSize = true;
            labelResult.Location = new Point(16, 36);
            labelResult.Name = "labelResult";
            labelResult.Text = "Сохранить в";
            // 
            // textBoxOutput
            // 
            textBoxOutput.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            textBoxOutput.Location = new Point(16, 56);
            textBoxOutput.Name = "textBoxOutput";
            textBoxOutput.Size = new Size(448, 23);
            textBoxOutput.TabIndex = 10;
            textBoxOutput.TextChanged += textBoxOutput_TextChanged;
            // 
            // buttonOutput
            // 
            buttonOutput.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            buttonOutput.Location = new Point(472, 54);
            buttonOutput.Name = "buttonOutput";
            buttonOutput.Size = new Size(72, 28);
            buttonOutput.TabIndex = 11;
            buttonOutput.Text = "Обзор";
            buttonOutput.Click += buttonOutput_Click;
            // 
            // buttonOpenOutput
            // 
            buttonOpenOutput.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            buttonOpenOutput.Location = new Point(548, 54);
            buttonOpenOutput.Name = "buttonOpenOutput";
            buttonOpenOutput.Size = new Size(64, 28);
            buttonOpenOutput.TabIndex = 12;
            buttonOpenOutput.Text = "Открыть";
            buttonOpenOutput.Visible = false;
            buttonOpenOutput.Click += buttonOpenOutput_Click;
            // 
            // buttonSaveOptions
            // 
            buttonSaveOptions.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            buttonSaveOptions.Location = new Point(616, 54);
            buttonSaveOptions.Name = "buttonSaveOptions";
            buttonSaveOptions.Size = new Size(68, 28);
            buttonSaveOptions.TabIndex = 13;
            buttonSaveOptions.Text = "Параметры";
            buttonSaveOptions.Click += buttonSaveOptions_Click;
            // 
            // actionBar
            // 
            actionBar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            actionBar.Controls.Add(labelProgress);
            actionBar.Controls.Add(progressBarProcess);
            actionBar.Controls.Add(buttonTrcToAsc);
            actionBar.Controls.Add(buttonHelp);
            actionBar.Controls.Add(buttonCancel);
            actionBar.Controls.Add(buttonProcess);
            actionBar.Location = new Point(0, 456);
            actionBar.Name = "actionBar";
            actionBar.Size = new Size(700, 56);
            // 
            // buttonProcess
            // 
            buttonProcess.Location = new Point(0, 8);
            buttonProcess.Name = "buttonProcess";
            buttonProcess.Size = new Size(120, 32);
            buttonProcess.TabIndex = 14;
            buttonProcess.Text = "Обработать";
            buttonProcess.Click += buttonProcess_Click;
            // 
            // buttonCancel
            // 
            buttonCancel.Enabled = false;
            buttonCancel.Location = new Point(128, 8);
            buttonCancel.Name = "buttonCancel";
            buttonCancel.Size = new Size(88, 32);
            buttonCancel.TabIndex = 15;
            buttonCancel.Text = "Отмена";
            buttonCancel.Visible = false;
            buttonCancel.Click += buttonCancel_Click;
            // 
            // buttonHelp
            // 
            buttonHelp.Location = new Point(224, 8);
            buttonHelp.Name = "buttonHelp";
            buttonHelp.Size = new Size(88, 32);
            buttonHelp.TabIndex = 16;
            buttonHelp.Text = "Помощь";
            buttonHelp.Visible = false;
            buttonHelp.Click += buttonHelp_Click;
            // 
            // buttonTrcToAsc
            // 
            buttonTrcToAsc.Location = new Point(320, 8);
            buttonTrcToAsc.Name = "buttonTrcToAsc";
            buttonTrcToAsc.Size = new Size(120, 32);
            buttonTrcToAsc.TabIndex = 17;
            buttonTrcToAsc.Text = "Смена формата";
            buttonTrcToAsc.Visible = false;
            buttonTrcToAsc.Click += buttonFormatConvert_Click;
            // 
            // progressBarProcess
            // 
            progressBarProcess.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            progressBarProcess.Location = new Point(224, 14);
            progressBarProcess.Maximum = 1000;
            progressBarProcess.Name = "progressBarProcess";
            progressBarProcess.Size = new Size(280, 12);
            progressBarProcess.TabStop = false;
            progressBarProcess.Visible = false;
            // 
            // labelProgress
            // 
            labelProgress.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            labelProgress.AutoEllipsis = true;
            labelProgress.Location = new Point(512, 12);
            labelProgress.Name = "labelProgress";
            labelProgress.Size = new Size(180, 20);
            labelProgress.Visible = false;
            // 
            // textBoxLog
            // 
            textBoxLog.AccessibleName = "Журнал обработки";
            textBoxLog.Dock = DockStyle.Fill;
            textBoxLog.Multiline = true;
            textBoxLog.Name = "textBoxLog";
            textBoxLog.PlaceholderText = "Журнал обработки появится здесь";
            textBoxLog.ReadOnly = true;
            textBoxLog.ScrollBars = ScrollBars.Vertical;
            textBoxLog.TabIndex = 18;
            // 
            // statusBar
            // 
            statusBar.Controls.Add(statusBadge);
            statusBar.Dock = DockStyle.Fill;
            statusBar.Name = "statusBar";
            statusBar.Padding = new Padding(12, 4, 12, 4);
            // 
            // statusBadge
            // 
            statusBadge.Dock = DockStyle.Fill;
            statusBadge.Name = "statusBadge";
            statusBadge.Text = "Готово к обработке";
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            // Высота: карточки (~520) + журнал + шапка/статус без прокрутки до «Обработать».
            ClientSize = new Size(920, 760);
            Controls.Add(rootLayout);
            Icon = (Icon)resources.GetObject("$this.Icon");
            MinimumSize = new Size(780, 640);
            Name = "MainForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "LOGER";
            rootLayout.ResumeLayout(false);
            headerPanel.ResumeLayout(false);
            headerPanel.PerformLayout();
            bodyLayout.ResumeLayout(false);
            navPanel.ResumeLayout(false);
            contentSplit.Panel1.ResumeLayout(false);
            contentSplit.Panel2.ResumeLayout(false);
            contentSplit.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)contentSplit).EndInit();
            contentSplit.ResumeLayout(false);
            workScroll.ResumeLayout(false);
            cardsHost.ResumeLayout(false);
            cardLogs.ResumeLayout(false);
            cardLogs.PerformLayout();
            cardDevices.ResumeLayout(false);
            cardDevices.PerformLayout();
            cardComposites.ResumeLayout(false);
            cardComposites.PerformLayout();
            cardOutput.ResumeLayout(false);
            cardOutput.PerformLayout();
            actionBar.ResumeLayout(false);
            statusBar.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private TableLayoutPanel rootLayout;
        private TableLayoutPanel bodyLayout;
        private Panel headerPanel;
        private Panel brandAccent;
        private Label labelBrand;
        private Button buttonThemeToggle;
        private Panel navPanel;
        private Controls.NavigationItem navProcess;
        private Controls.NavigationItem navHelp;
        private Controls.NavigationItem navConvert;
        private SplitContainer contentSplit;
        private Panel workScroll;
        private Panel cardsHost;
        private Controls.ModernCard cardLogs;
        private Controls.ModernCard cardDevices;
        private Controls.ModernCard cardComposites;
        private Controls.ModernCard cardOutput;
        private Panel actionBar;
        private Panel statusBar;
        private Controls.StatusBadge statusBadge;
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
