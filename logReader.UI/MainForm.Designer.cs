#nullable enable
namespace logReader.UI
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer? components;
        protected override void Dispose(bool disposing)
        {
            if (disposing) { ReleaseWorkspace(); components?.Dispose(); }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = WorkspaceFont(9F);
            ClientSize = new Size(1240, 820);
            MinimumSize = new Size(1040, 740);
            StartPosition = FormStartPosition.CenterScreen;
            Name = "MainForm";
            Text = "LOGER 2.0 — CAN workspace";
            KeyPreview = true;
            var resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            Icon = resources.GetObject("$this.Icon") as Icon;
            textBoxCanLog = WorkspacePath("textBoxCanLog", 0);
            textBoxDevices = WorkspacePath("textBoxDevices", 1);
            textBoxComposites = WorkspacePath("textBoxComposites", 2);
            textBoxOutput = WorkspacePath("textBoxOutput", 3);
            textBoxDevices.TextChanged += textBoxDevices_TextChanged;
            textBoxComposites.TextChanged += textBoxComposites_TextChanged;
            textBoxOutput.TextChanged += textBoxOutput_TextChanged;
            labelCANlog = WorkspaceLabel("Файл или папка логов", "labelCANlog");
            labelDevices = WorkspaceLabel("Описание CAN-посылок", "labelDevices");
            labelComposites = WorkspaceLabel("Составные параметры", "labelComposites");
            labelResult = WorkspaceLabel("Путь сохранения", "labelResult");
            labelFilterStatus = WorkspaceLabel("Файл посылок не загружен", "labelFilterStatus", "muted");
            labelFilterStatus.AutoSize = false;
            labelFilterStatus.Height = 30;
            labelFilterStatus.Dock = DockStyle.Fill;
            labelFilterStatus.AutoEllipsis = true;
            labelProgress = WorkspaceLabel("", "labelProgress", "muted");
            labelProgress.AutoSize = false;
            labelProgress.Height = 36;
            labelProgress.Dock = DockStyle.Top;
            labelProgress.Visible = false;
            buttonCANlog = WorkspaceButton("Выбрать источник…", "buttonCANlog", buttonCANlog_Click);
            buttonViewLog = WorkspaceButton("Просмотр пакетов", "buttonViewLog", buttonViewLog_Click);
            buttonDevices = WorkspaceButton("Выбрать файл…", "buttonDevices", buttonDevices_Click);
            buttonDevicesParams = WorkspaceButton("Устройства и параметры", "buttonDevicesParams", buttonDevicesParams_Click);
            buttonDevicesCreateOrAdd = WorkspaceButton("Создать…", "buttonDevicesCreateOrAdd", buttonDevicesCreateOrAdd_Click);
            buttonComposites = WorkspaceButton("Выбрать файл…", "buttonComposites", buttonComposites_Click);
            buttonCompositesCreateOrAdd = WorkspaceButton("Создать .xlsx", "buttonCompositesCreateOrAdd", buttonCompositesCreateOrAdd_Click);
            buttonOutput = WorkspaceButton("Выбрать путь…", "buttonOutput", buttonOutput_Click);
            buttonSaveOptions = WorkspaceButton("Параметры экспорта", "buttonSaveOptions", buttonSaveOptions_Click);
            buttonOpenOutput = WorkspaceButton("Открыть результат ↗", "buttonOpenOutput", buttonOpenOutput_Click);
            buttonProcess = WorkspaceButton("Обработать", "buttonProcess", buttonProcess_Click, "primary");
            buttonProcess.Dock = DockStyle.Top;
            buttonProcess.Height = 46;
            buttonCancel = WorkspaceButton("Отменить операцию", "buttonCancel", buttonCancel_Click);
            buttonCancel.Dock = DockStyle.Top;
            buttonCancel.Enabled = false;
            buttonCancel.Visible = false;
            buttonHelp = WorkspaceButton("Справка                 F1", "buttonHelp", buttonHelp_Click, "nav");
            buttonTrcToAsc = WorkspaceButton("Конвертация", "buttonTrcToAsc", buttonFormatConvert_Click, "nav");
            progressBarProcess = new ProgressBar
            {
                Name = "progressBarProcess", Maximum = 1000, Height = 8,
                Dock = DockStyle.Top, TabStop = false, Visible = false,
                AccessibleName = "Прогресс обработки"
            };
            textBoxLog = new TextBox
            {
                Name = "textBoxLog", Multiline = true, ReadOnly = true,
                ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None, Font = WorkspaceFont(9F, FontStyle.Regular, "Consolas"),
                AccessibleName = "Журнал операций", WordWrap = false
            };
            contentSplit = new SplitContainer
            {
                Name = "contentSplit", Dock = DockStyle.Fill, Orientation = Orientation.Horizontal,
                Margin = Padding.Empty,
                SplitterWidth = 5, Size = new Size(1010, 686), SplitterDistance = 536,
                FixedPanel = FixedPanel.Panel2, TabStop = false
            };
            contentSplit.Panel1MinSize = 400;
            contentSplit.Panel2MinSize = 80;
            BuildWorkspace();
            ResumeLayout(true);
        }
        private SplitContainer contentSplit = null!;
        private Label labelCANlog = null!;
        private Label labelDevices = null!;
        private Label labelComposites = null!;
        private Label labelResult = null!;
        private Label labelFilterStatus = null!;
        private TextBox textBoxCanLog = null!;
        private TextBox textBoxDevices = null!;
        private TextBox textBoxComposites = null!;
        private TextBox textBoxOutput = null!;
        private Button buttonCANlog = null!;
        private Button buttonViewLog = null!;
        private Button buttonDevices = null!;
        private Button buttonComposites = null!;
        private Button buttonCompositesCreateOrAdd = null!;
        private Button buttonOutput = null!;
        private Button buttonOpenOutput = null!;
        private TextBox textBoxLog = null!;
        private Button buttonProcess = null!;
        private Button buttonHelp = null!;
        private Button buttonTrcToAsc = null!;
        private Button buttonDevicesParams = null!;
        private Button buttonDevicesCreateOrAdd = null!;
        private Button buttonSaveOptions = null!;
        private Button buttonCancel = null!;
        private ProgressBar progressBarProcess = null!;
        private Label labelProgress = null!;
    }
}
