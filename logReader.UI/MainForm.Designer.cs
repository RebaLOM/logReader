#nullable enable

namespace logReader.UI
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer? components;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                components?.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            var resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = Typography.Body;
            BackColor = AppTheme.Background;
            ClientSize = new Size(1184, 824);
            MinimumSize = new Size(900, 640);
            StartPosition = FormStartPosition.CenterScreen;
            Icon = (Icon?)resources.GetObject("$this.Icon");
            Name = nameof(MainForm);
            Text = "LOGER · Рабочая область CAN";

            labelCANlog = new Label { Text = "Источник логов" };
            labelDevices = new Label { Text = "Правила декодирования" };
            labelComposites = new Label { Text = "Составные параметры" };
            labelResult = new Label { Text = "Результат" };
            labelFilterStatus = new Label { AutoSize = true, ForeColor = AppTheme.TextSecondary, Text = "Файл посылок не загружен" };
            textBoxCanLog = new ModernTextBox { Name = nameof(textBoxCanLog), PlaceholderText = "Путь к файлу или папке с логами" };
            textBoxDevices = new ModernTextBox { Name = nameof(textBoxDevices), PlaceholderText = "Файл посылок .xlsx, .dbc или .dbf" };
            textBoxComposites = new ModernTextBox { Name = nameof(textBoxComposites), PlaceholderText = "Необязательно · файл .xlsx" };
            textBoxOutput = new ModernTextBox { Name = nameof(textBoxOutput), PlaceholderText = "Путь к файлу или папке результатов" };
            textBoxLog = new TextBox
            {
                Name = nameof(textBoxLog), Multiline = true, ReadOnly = true,
                Dock = DockStyle.Fill, BorderStyle = BorderStyle.None,
                ScrollBars = ScrollBars.Vertical, Font = Typography.Mono,
                BackColor = AppTheme.Surface, ForeColor = AppTheme.TextSecondary
            };

            buttonCANlog = WorkspaceButton("Выбрать", IconKind.Folder);
            buttonCANlog.Click += buttonCANlog_Click;
            buttonViewLog = WorkspaceButton("Посылки в логе", IconKind.Table, ButtonVariant.Ghost);
            buttonViewLog.Click += buttonViewLog_Click;
            buttonDevices = WorkspaceButton("Выбрать", IconKind.File);
            buttonDevices.Click += buttonDevices_Click;
            buttonComposites = WorkspaceButton("Выбрать", IconKind.File);
            buttonComposites.Click += buttonComposites_Click;
            buttonOutput = WorkspaceButton("Выбрать", IconKind.Folder);
            buttonOutput.Click += buttonOutput_Click;
            buttonOpenOutput = WorkspaceButton("Открыть результат", IconKind.ExternalLink);
            buttonOpenOutput.Visible = false;
            buttonOpenOutput.Click += buttonOpenOutput_Click;
            buttonProcess = WorkspaceButton("Обработать логи", IconKind.Play, ButtonVariant.Primary);
            buttonProcess.Click += buttonProcess_Click;
            buttonHelp = WorkspaceButton("Справка", IconKind.Help, ButtonVariant.Ghost);
            buttonHelp.Click += buttonHelp_Click;
            buttonTrcToAsc = WorkspaceButton("Смена формата", IconKind.Convert);
            buttonTrcToAsc.Click += buttonFormatConvert_Click;
            buttonDevicesParams = WorkspaceButton("Выбрать параметры", IconKind.Filter, ButtonVariant.Ghost);
            buttonDevicesParams.Click += buttonDevicesParams_Click;
            buttonDevicesCreateOrAdd = WorkspaceButton("Создать файл", IconKind.Plus);
            buttonDevicesCreateOrAdd.Click += buttonDevicesCreateOrAdd_Click;
            buttonCompositesCreateOrAdd = WorkspaceButton("Создать .xlsx", IconKind.Plus);
            buttonCompositesCreateOrAdd.Click += buttonCompositesCreateOrAdd_Click;
            buttonSaveOptions = WorkspaceButton("Параметры сохранения", IconKind.Sliders, ButtonVariant.Ghost);
            buttonSaveOptions.Click += buttonSaveOptions_Click;

            buttonCancel = WorkspaceButton("Отмена", IconKind.Close);
            buttonCancel.Name = nameof(buttonCancel);
            buttonCancel.Enabled = false;
            buttonCancel.Visible = false;
            buttonCancel.Click += buttonCancel_Click;
            progressBarProcess = new ProgressBar
            {
                Name = nameof(progressBarProcess), Dock = DockStyle.Top, Height = 4,
                Maximum = 1000, Style = ProgressBarStyle.Continuous, Visible = false,
                Margin = new Padding(0, 8, 24, 0), TabStop = false
            };
            labelProgress = new Label
            {
                Name = nameof(labelProgress), AutoSize = true, AutoEllipsis = true,
                Font = Typography.Caption, ForeColor = AppTheme.TextSecondary,
                Visible = false, Margin = new Padding(0, 8, 8, 0),
                AccessibleName = "Ход обработки"
            };

            textBoxDevices.TextChanged += textBoxDevices_TextChanged;
            textBoxComposites.TextChanged += textBoxComposites_TextChanged;
            textBoxOutput.TextChanged += textBoxOutput_TextChanged;
            BuildWorkspace();
            ResumeLayout(true);
        }

        private SplitContainer contentSplit = null!;
        private Label labelCANlog = null!, labelDevices = null!, labelComposites = null!, labelResult = null!, labelFilterStatus = null!;
        private TextBox textBoxCanLog = null!, textBoxDevices = null!, textBoxComposites = null!, textBoxOutput = null!, textBoxLog = null!;
        private Button buttonCANlog = null!, buttonViewLog = null!, buttonDevices = null!, buttonComposites = null!;
        private Button buttonCompositesCreateOrAdd = null!, buttonOutput = null!, buttonOpenOutput = null!;
        private Button buttonProcess = null!, buttonHelp = null!, buttonTrcToAsc = null!, buttonDevicesParams = null!;
        private Button buttonDevicesCreateOrAdd = null!, buttonSaveOptions = null!, buttonCancel = null!;
        private ProgressBar progressBarProcess = null!;
        private Label labelProgress = null!;
    }
}
