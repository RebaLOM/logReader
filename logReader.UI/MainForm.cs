using System.Linq;
using logReader.UI.Controls;
using logReader.UI.Theme;

namespace logReader.UI
{
    public partial class MainForm : Form
    {
        private static readonly IReadOnlyList<FormatConversionPair> _conversionPairs = new List<FormatConversionPair>
        {
            new("trc_to_asc", "TRC -> ASC", ".trc", ".asc"),
            new("csv_to_asc", "CSV -> ASC", ".csv", ".asc"),
        };

        private Dictionary<string, bool> _deviceEnabled = new();
        private Dictionary<string, bool[]> _paramEnabled = new();

        // Кэш описаний: перечитывается при смене пути или времени изменения файла (правка в Excel).
        private readonly CachedFile<List<Device>> _devicesCache = new(path => DeviceFiles.LoadDevices(path));
        private readonly CachedFile<CompositeRuntime> _compositesCache = new(path => DeviceFiles.LoadComposites(path));
        private List<Device>? _cachedDevices => _devicesCache.Value;

        private sealed class SaveOptions
        {
            public OutputFormat OutputFormat { get; set; } = OutputFormat.Csv;
            public BatchOutputMode BatchMode { get; set; } = BatchOutputMode.PerInputFile;
            public LogFormatKind FolderFormatFilter { get; set; } = LogFormatKind.All;
            public DstConnectOptions DstConnect { get; set; } = new();
            public bool IncludeDeviceIdHeaderRow { get; set; }
        }

        private readonly SaveOptions _saveOptions = new();

        /// <summary>Высота нижней панели журнала (px); сохраняется при перетаскивании разделителя.</summary>
        private int _logPanelHeight = 100;
        private bool _layingOutContentSplit;

        private CancellationTokenSource? _operation;
        private bool _closeWhenIdle;

        public MainForm()
        {
            InitializeComponent();
            TagSectionSurfaces();
            WireContentSplitLayout();
            UpdateDevicesCreateAddButtonState();
            UpdateCompositesCreateAddButtonState();
            UpdateFilterLabel();
            buttonOpenOutput.Visible = false;
            FormClosing += MainForm_FormClosing;
            Load += (_, _) =>
            {
                ApplyThemeToUi();
                ThemeNative.ApplyTitleBar(this, AppTheme.Current);
            };
            AppTheme.Changed += OnAppThemeChanged;
            FormClosed += (_, _) => AppTheme.Changed -= OnAppThemeChanged;
        }

        private void TagSectionSurfaces()
        {
            headerPanel.Tag = ThemeTags.Header;
            labelBrand.Tag = ThemeTags.Brand;
            labelFilterStatus.Tag = ThemeTags.Muted;
            labelProgress.Tag = ThemeTags.Muted;
            textBoxLog.Tag = ThemeTags.Console;
            buttonProcess.Tag = ThemeTags.Primary;
        }

        private void OnAppThemeChanged(object? sender, EventArgs e)
        {
            if (IsDisposed || Disposing || !IsHandleCreated)
                return;
            BeginInvoke(ApplyThemeToUi);
        }

        private void ApplyThemeToUi()
        {
            if (IsDisposed || Disposing)
                return;
            AppTheme.Apply(this);
            ThemePalette p = AppTheme.Palette;
            brandAccent.BackColor = p.Primary;
            buttonThemeToggle.Text = AppTheme.Current == ThemeMode.Dark ? "Светлая тема" : "Тёмная тема";
            UpdateFilterLabel();
            ThemeNative.ApplyTitleBar(this, AppTheme.Current);
        }

        private void buttonThemeToggle_Click(object? sender, EventArgs e)
        {
            ThemeMode next = AppTheme.Current == ThemeMode.Dark ? ThemeMode.Light : ThemeMode.Dark;
            AppTheme.SetAndPersist(next);
        }

        private bool IsBusy => _operation != null;

        private static string EnsureOutputPathMatchesFormat(string path, OutputFormat outputFormat)
        {
            string trimmed = path.Trim();
            string desiredExt = LogProcessingService.GetOutputExtension(outputFormat);
            string currentExt = Path.GetExtension(trimmed);
            if (string.IsNullOrEmpty(currentExt))
                return trimmed + desiredExt;
            if (currentExt.Equals(".xlsx", StringComparison.OrdinalIgnoreCase)
                || currentExt.Equals(".csv", StringComparison.OrdinalIgnoreCase))
                return Path.ChangeExtension(trimmed, desiredExt);
            return trimmed;
        }

        private static bool LooksLikeFilePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            string trimmed = path.Trim();
            if (trimmed.EndsWith(Path.DirectorySeparatorChar) || trimmed.EndsWith(Path.AltDirectorySeparatorChar))
                return false;

            return !string.IsNullOrEmpty(Path.GetExtension(trimmed));
        }

        private void SyncOutputFormatWithPath(string path)
        {
            string ext = Path.GetExtension(path);
            if (ext.Equals(".csv", StringComparison.OrdinalIgnoreCase))
            {
                if (_saveOptions.OutputFormat == OutputFormat.Xlsx)
                    _saveOptions.OutputFormat = OutputFormat.Csv;
            }
            else if (ext.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                _saveOptions.OutputFormat = OutputFormat.Xlsx;
        }

        private void buttonSaveOptions_Click(object sender, EventArgs e)
        {
            string? folderPath = Directory.Exists(textBoxCanLog.Text.Trim()) ? textBoxCanLog.Text.Trim() : null;
            using var dlg = new SaveOptionsForm(
                _saveOptions.OutputFormat,
                _saveOptions.BatchMode,
                _saveOptions.DstConnect,
                folderPath,
                _saveOptions.FolderFormatFilter,
                _saveOptions.IncludeDeviceIdHeaderRow);
            if (dlg.ShowDialog(this) != DialogResult.OK)
                return;

            _saveOptions.OutputFormat = dlg.SelectedOutputFormat;
            _saveOptions.BatchMode = dlg.SelectedBatchMode;
            _saveOptions.FolderFormatFilter = dlg.SelectedFolderFormats;
            _saveOptions.DstConnect = dlg.SelectedDstConnectOptions;
            _saveOptions.IncludeDeviceIdHeaderRow = dlg.IncludeDeviceIdHeaderRow;

            if (string.IsNullOrWhiteSpace(textBoxOutput.Text))
                return;
            if (Directory.Exists(textBoxCanLog.Text.Trim()))
                return;
            if (!LooksLikeFilePath(textBoxOutput.Text))
                return;

            textBoxOutput.Text = EnsureOutputPathMatchesFormat(textBoxOutput.Text, _saveOptions.OutputFormat);
        }

        private static bool IsDescriptionFile(string path)
        {
            string ext = Path.GetExtension(path);
            return ext.Equals(".xlsx", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".dbc", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".dbf", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsDevicesFileSelectedAndExists()
        {
            string path = textBoxDevices.Text;
            return !string.IsNullOrWhiteSpace(path) && File.Exists(path) && IsDescriptionFile(path);
        }

        private void UpdateDevicesCreateAddButtonState()
        {
            buttonDevicesCreateOrAdd.Text = IsDevicesFileSelectedAndExists()
                ? "Редактор"
                : "Создать...";
        }

        private void EnsureFiltersMatchDevices()
        {
            if (_cachedDevices == null) return;

            foreach (var d in _cachedDevices)
            {
                if (!_deviceEnabled.ContainsKey(d.ID))
                    _deviceEnabled[d.ID] = true;

                if (!_paramEnabled.TryGetValue(d.ID, out var arr))
                {
                    _paramEnabled[d.ID] = Enumerable.Repeat(true, d.Headers.Length).ToArray();
                    continue;
                }

                if (arr.Length == d.Headers.Length) continue;

                var resized = new bool[d.Headers.Length];
                int copyLen = Math.Min(arr.Length, resized.Length);
                Array.Copy(arr, resized, copyLen);
                for (int i = copyLen; i < resized.Length; i++)
                    resized[i] = true;
                _paramEnabled[d.ID] = resized;
            }
        }

        private void UpdateFilterLabel()
        {
            var devices = _cachedDevices;
            ThemePalette p = AppTheme.Palette;
            if (devices == null || devices.Count == 0)
            {
                labelFilterStatus.Text = "Файл посылок не загружен";
                labelFilterStatus.ForeColor = p.Muted;
                return;
            }

            // Параметры выключенного устройства не участвуют в подсчёте активных фильтров.
            var filter = OutputFilter.From(_deviceEnabled, _paramEnabled);
            int enabledDevices = devices.Count(d => filter.IsDeviceEnabled(d.ID));
            int totalParams = devices.Sum(d => d.Headers.Length);
            int enabledParams = devices.Sum(d => filter.GetActiveParams(d).Length);

            labelFilterStatus.Text = $"Устройства: {enabledDevices}/{devices.Count}  Параметры: {enabledParams}/{totalParams}";
            labelFilterStatus.ForeColor = p.Text;
        }

        private void ResetFilters()
        {
            _deviceEnabled = new();
            _paramEnabled = new();
        }

        private enum LogSourceKind { None, File, Folder }

        private LogSourceKind ShowPickLogSourceDialog()
        {
            using var dlg = new AppDialog
            {
                Text = "Источник логов",
                ShowInTaskbar = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(16),
            };

            var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 3, RowCount = 2, Dock = DockStyle.Fill };
            var lbl = new Label
            {
                Text = "Выберите один файл лога или папку с логами:",
                AutoSize = true,
                Margin = new Padding(3, 3, 3, 12),
                Font = Typography.Body(),
            };
            layout.Controls.Add(lbl, 0, 0);
            layout.SetColumnSpan(lbl, 3);

            var btnFile = new ModernButton { Text = "&Файл...", Kind = ButtonKind.Primary, AutoSize = true, MinimumSize = new Size(100, 30) };
            var btnFolder = new ModernButton { Text = "&Папка...", Kind = ButtonKind.Secondary, AutoSize = true, MinimumSize = new Size(100, 30) };
            var btnCancel = new ModernButton { Text = "Отмена", Kind = ButtonKind.Ghost, AutoSize = true, MinimumSize = new Size(100, 30), DialogResult = DialogResult.Cancel };
            layout.Controls.Add(btnFile, 0, 1);
            layout.Controls.Add(btnFolder, 1, 1);
            layout.Controls.Add(btnCancel, 2, 1);

            var kind = LogSourceKind.None;
            btnFile.Click += (_, _) => { kind = LogSourceKind.File; dlg.Close(); };
            btnFolder.Click += (_, _) => { kind = LogSourceKind.Folder; dlg.Close(); };

            dlg.Controls.Add(layout);
            dlg.AcceptButton = btnFile;
            dlg.CancelButton = btnCancel;

            dlg.ShowDialog(this);
            return kind;
        }

        private void buttonCANlog_Click(object sender, EventArgs e)
        {
            LogSourceKind kind = ShowPickLogSourceDialog();
            if (kind == LogSourceKind.None) return;

            if (kind == LogSourceKind.File)
            {
                using OpenFileDialog ofd = new OpenFileDialog();
                ofd.Filter = "Лог файлы (*.csv;*.trc;*.asc;*.txt)|*.csv;*.trc;*.asc;*.txt|CSV (*.csv)|*.csv|pCAN (*.trc)|*.trc|ASC (*.asc)|*.asc|CANfox / PCAN-View (*.txt)|*.txt";
                if (ofd.ShowDialog(this) != DialogResult.OK) return;
                textBoxCanLog.Text = ofd.FileName;

                string dir = Path.GetDirectoryName(ofd.FileName) ?? "";
                string ext = LogProcessingService.GetOutputExtension(_saveOptions.OutputFormat);
                string name = Path.GetFileNameWithoutExtension(ofd.FileName) + "_result" + ext;
                textBoxOutput.Text = Path.Combine(dir, name);
                return;
            }

            using var fbd = new FolderBrowserDialog();
            fbd.Description = "Выберите папку с логами (.csv, .trc, .asc, .txt CANfox)";
            if (fbd.ShowDialog(this) != DialogResult.OK) return;

            textBoxCanLog.Text = fbd.SelectedPath;
            textBoxOutput.Text = Path.Combine(fbd.SelectedPath, "result");
        }

        private void buttonViewLog_Click(object sender, EventArgs e)
        {
            string path = textBoxCanLog.Text.Trim();
            if (string.IsNullOrWhiteSpace(path))
            {
                Log("Ошибка: сначала укажите файл лога (.csv, .trc, .asc или .txt CANfox).");
                return;
            }

            if (!Directory.Exists(path) && !File.Exists(path))
            {
                Log("Ошибка: файл лога не найден.");
                return;
            }

            using var viewForm = new CanLogViewForm(path);
            viewForm.ShowDialog(this);
        }

        private void buttonDevices_Click(object sender, EventArgs e)
        {
            using OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Файлы посылок (*.xlsx;*.dbc;*.dbf)|*.xlsx;*.dbc;*.dbf|Excel files (*.xlsx)|*.xlsx|DBC files (*.dbc)|*.dbc|DBF files (*.dbf)|*.dbf";
            if (ofd.ShowDialog(this) == DialogResult.OK)
                textBoxDevices.Text = ofd.FileName;
        }

        private void textBoxDevices_TextChanged(object sender, EventArgs e)
        {
            UpdateDevicesCreateAddButtonState();
            string path = textBoxDevices.Text;
            if (!IsDevicesFileSelectedAndExists())
            {
                _devicesCache.Clear();
                ResetFilters();
                UpdateFilterLabel();
                return;
            }

            if (_devicesCache.IsCurrent(path)) return;

            // Новый файл устройств — сбрасываем фильтры, иначе останутся ID прошлого файла.
            ResetFilters();
            TryLoadDevices(path);
            UpdateFilterLabel();
        }

        private List<Device>? TryLoadDevices(string path, bool logDetails = false)
        {
            try
            {
                bool reloaded = !_devicesCache.IsCurrent(path);
                var devices = _devicesCache.Get(path, logDetails ? Log : null);
                if (reloaded && logDetails)
                    Log($"Файл посылок загружен: устройств {devices.Count}.");
                return devices;
            }
            // Ошибки разбора приходят из ClosedXML/OpenXML разных типов — для пользователя это одна ошибка загрузки.
            catch (Exception ex)
            {
                _devicesCache.Clear();
                Log($"Ошибка загрузки файла посылок: {ex.Message}");
                return null;
            }
        }

        private void buttonDevicesCreateOrAdd_Click(object sender, EventArgs e)
        {
            if (IsDevicesFileSelectedAndExists())
                OpenDevicesEditor();
            else
                CreateNewDevicesFile();

            UpdateDevicesCreateAddButtonState();
        }

        private void CreateNewDevicesFile()
        {
            using var kindDlg = new FileKindPromptForm();
            if (kindDlg.ShowDialog(this) != DialogResult.OK) return;
            if (kindDlg.SelectedKind == FileKindPromptForm.FileKind.None) return;

            using SaveFileDialog sfd = new SaveFileDialog();
            if (kindDlg.SelectedKind == FileKindPromptForm.FileKind.Xlsx)
            {
                sfd.Filter = "Excel files (*.xlsx)|*.xlsx";
                sfd.DefaultExt = "xlsx";
            }
            else if (kindDlg.SelectedKind == FileKindPromptForm.FileKind.Dbf)
            {
                sfd.Filter = "DBF files (*.dbf)|*.dbf";
                sfd.DefaultExt = "dbf";
            }
            else
            {
                sfd.Filter = "DBC files (*.dbc)|*.dbc";
                sfd.DefaultExt = "dbc";
            }
            sfd.AddExtension = true;
            if (sfd.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                if (kindDlg.SelectedKind == FileKindPromptForm.FileKind.Xlsx)
                    DeviceExcelFile.CreateDevicesExcelTemplate(sfd.FileName);
                else if (kindDlg.SelectedKind == FileKindPromptForm.FileKind.Dbf)
                    DbfFile.CreateEmpty(sfd.FileName);
                else
                    DbcFile.CreateEmpty(sfd.FileName);

                textBoxDevices.Text = sfd.FileName;
                Log("Файл посылок создан. Откроется редактор.");

                OpenDevicesEditor();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log("Ошибка создания файла посылок: " + ex.Message);
            }
        }

        // null — файл можно открыть на запись; иначе причина, понятная пользователю.
        private static string? DescribeWriteBlock(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return "нет прав на запись (файл только для чтения или защищён)";
            }
            catch (IOException)
            {
                return "файл открыт в другой программе — закройте его и попробуйте снова";
            }
        }

        private void OpenDevicesEditor()
        {
            string path = textBoxDevices.Text;
            if (!IsDevicesFileSelectedAndExists())
            {
                Log("Ошибка: файл посылок не найден.");
                return;
            }

            string? block = DescribeWriteBlock(path);
            if (block != null)
            {
                Log("Ошибка: файл посылок: " + block + ".");
                return;
            }

            using var editor = new DevicesEditorForm(path);
            if (editor.LoadFailed)
            {
                Log("Ошибка: файл посылок не прочитан — редактор не открыт.");
                return;
            }
            editor.ShowDialog(this);

            if (editor.Modified)
            {
                _devicesCache.Clear();
                if (TryLoadDevices(path, logDetails: true) != null)
                {
                    EnsureFiltersMatchDevices();
                    Log("Файл посылок обновлён.");
                }
                UpdateFilterLabel();
            }
        }

        private bool IsCompositesFileSelectedAndExists()
        {
            string path = textBoxComposites.Text;
            return !string.IsNullOrWhiteSpace(path)
                && File.Exists(path)
                && Path.GetExtension(path).Equals(".xlsx", StringComparison.OrdinalIgnoreCase);
        }

        private void UpdateCompositesCreateAddButtonState()
        {
            buttonCompositesCreateOrAdd.Text = IsCompositesFileSelectedAndExists()
                ? "Редактор"
                : "Создать .xlsx";
        }

        private CompositeRuntime? EnsureCompositesLoaded()
        {
            if (!IsCompositesFileSelectedAndExists())
            {
                _compositesCache.Clear();
                return null;
            }

            return _compositesCache.Get(textBoxComposites.Text, Log);
        }

        private void textBoxComposites_TextChanged(object sender, EventArgs e)
        {
            UpdateCompositesCreateAddButtonState();
            _compositesCache.Clear();
        }

        private void buttonComposites_Click(object sender, EventArgs e)
        {
            using OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Файл составных параметров (*.xlsx)|*.xlsx";
            if (ofd.ShowDialog(this) == DialogResult.OK)
                textBoxComposites.Text = ofd.FileName;
        }

        private void buttonCompositesCreateOrAdd_Click(object sender, EventArgs e)
        {
            if (IsCompositesFileSelectedAndExists())
            {
                OpenCompositesEditor();
            }
            else
            {
                using SaveFileDialog sfd = new SaveFileDialog();
                sfd.Filter = "Excel files (*.xlsx)|*.xlsx";
                sfd.DefaultExt = "xlsx";
                sfd.AddExtension = true;
                if (sfd.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    CompositeExcelFile.CreateTemplate(sfd.FileName);
                    textBoxComposites.Text = sfd.FileName;
                    Log("Файл составных параметров создан. Откроется редактор.");
                    OpenCompositesEditor();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Log("Ошибка создания файла составных параметров: " + ex.Message);
                }
            }

            UpdateCompositesCreateAddButtonState();
        }

        private void OpenCompositesEditor()
        {
            string path = textBoxComposites.Text;
            if (!IsCompositesFileSelectedAndExists())
            {
                Log("Ошибка: файл составных параметров не найден.");
                return;
            }

            string? block = DescribeWriteBlock(path);
            if (block != null)
            {
                Log("Ошибка: файл составных параметров: " + block + ".");
                return;
            }

            using var editor = new CompositeEditorForm(path);
            if (editor.LoadFailed)
            {
                Log("Ошибка: файл составных параметров не прочитан — редактор не открыт.");
                return;
            }
            editor.ShowDialog(this);

            if (editor.Modified)
            {
                _compositesCache.Clear();
                Log("Файл составных параметров обновлён.");
            }
        }

        private void buttonOutput_Click(object sender, EventArgs e)
        {
            using SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "Excel files (*.xlsx)|*.xlsx|CSV files (*.csv)|*.csv";
            sfd.DefaultExt = LogProcessingService.GetOutputExtension(_saveOptions.OutputFormat).TrimStart('.');
            sfd.AddExtension = true;
            sfd.FilterIndex = _saveOptions.OutputFormat == OutputFormat.Xlsx ? 1 : 2;
            if (sfd.ShowDialog(this) == DialogResult.OK)
            {
                textBoxOutput.Text = sfd.FileName;
                SyncOutputFormatWithPath(sfd.FileName);
                buttonOpenOutput.Visible = false;
            }
        }

        private static bool TryResolveOutputDirectoryForBatch(string? outputPath, Action<string> log, out string outputDir)
        {
            outputDir = "";
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                log("Ошибка: не указан путь сохранения.");
                return false;
            }

            string t = outputPath.Trim();
            if (File.Exists(t))
            {
                log("Ошибка: для обработки папки укажите каталог для результатов, а не файл.");
                return false;
            }

            if (Directory.Exists(t))
            {
                outputDir = Path.GetFullPath(t);
                return true;
            }

            if (t.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)
                || t.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                log("Ошибка: для обработки папки укажите каталог для результатов (не путь к одному выходному файлу).");
                return false;
            }

            try
            {
                Directory.CreateDirectory(t);
                outputDir = Path.GetFullPath(t);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                log("Ошибка: не удалось создать папку результатов: " + ex.Message);
                return false;
            }
        }

        private void textBoxOutput_TextChanged(object sender, EventArgs e)
        {
            SyncOutputFormatWithPath(textBoxOutput.Text);
            buttonOpenOutput.Visible = false;
        }

        private void buttonOpenOutput_Click(object sender, EventArgs e)
        {
            string p = textBoxOutput.Text;
            if (string.IsNullOrWhiteSpace(p))
            {
                Log("Путь не задан.");
                return;
            }

            bool exists = File.Exists(p) || Directory.Exists(p);
            if (!exists)
            {
                Log("Файл или папка не найдены.");
                return;
            }

            try
            {
                // Путь вывода выбрал пользователь; UseShellExecute открывает его ассоциацией ОС.
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = p,
                    UseShellExecute = true
                });
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                Log("Не удалось открыть: " + ex.Message);
            }
        }

        private HelpForm? _helpForm;

        private void buttonHelp_Click(object sender, EventArgs e)
        {
            // Немодальная справка: один экземпляр, ссылку обнуляем при закрытии.
            if (_helpForm is { IsDisposed: false })
            {
                _helpForm.Activate();
                return;
            }

            _helpForm = new HelpForm();
            _helpForm.FormClosed += (_, _) => _helpForm = null;
            _helpForm.Show(this);
        }

        private void buttonFormatConvert_Click(object sender, EventArgs e)
        {
            string initialPath = File.Exists(textBoxCanLog.Text) ? textBoxCanLog.Text : "";
            using var dialog = new FormatConversionDialog(_conversionPairs, initialPath, Log);
            dialog.ShowDialog(this);
        }

        private async void buttonDevicesParams_Click(object sender, EventArgs e)
        {
            if (!IsDevicesFileSelectedAndExists())
            {
                Log("Ошибка: сначала укажите файл посылок (.xlsx, .dbc или .dbf).");
                return;
            }

            var devices = TryLoadDevices(textBoxDevices.Text, logDetails: true);
            if (devices == null) return;
            if (devices.Count == 0)
            {
                Log("Ошибка: устройства не загружены из файла.");
                return;
            }

            EnsureFiltersMatchDevices();

            CompositeRuntime? composites;
            try { composites = EnsureCompositesLoaded(); }
            catch (Exception ex)
            {
                Log("Ошибка загрузки составных параметров: " + ex.Message);
                return;
            }

            // Список для фильтра: устройства + составные блоки.
            var filterDevices = new List<Device>(devices);
            if (composites != null)
                filterDevices.AddRange(composites.Blocks);

            string canLogPath = textBoxCanLog.Text.Trim();
            LogDeviceScanResult? scan = await RunBusyAsync("Сверка с логом...", (context, token) =>
                UnknownDevicesScanner.ScanLogDevices(canLogPath, devices, context.Log, token));
            if (scan == null) return;

            var missingInDevices = scan.MissingInDevices;
            // Источники составных параметров не считаем отсутствующими устройствами.
            if (composites != null)
            {
                var srcIds = new HashSet<string>(composites.SourceIds, StringComparer.OrdinalIgnoreCase);
                missingInDevices = missingInDevices.Where(id => !srcIds.Contains(id)).ToList();
            }

            using (var form = new Devices_ParametrsForm(
                filterDevices, _deviceEnabled, _paramEnabled,
                missingInDevices, scan.MatchedInDevices))
                form.ShowDialog(this);

            UpdateFilterLabel();
        }

        // Журнал пишется асинхронно: фоновый поток не ждёт UI и не падает, если окно уже закрывается.
        private void Log(string message)
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired)
            {
                if (IsHandleCreated)
                    BeginInvoke(new Action<string>(Log), message);
                return;
            }
            textBoxLog.AppendText(message + Environment.NewLine);
        }

        // Длительная операция в фоне: ввод заблокирован, есть прогресс и «Отмена».
        private async Task<T?> RunBusyAsync<T>(string stage, Func<ProcessingContext, CancellationToken, T?> work)
            where T : class
        {
            using var cts = new CancellationTokenSource();
            _operation = cts;
            SetBusy(true, stage);
            var progress = new Progress<ProcessingProgress>(p =>
            {
                progressBarProcess.Value = (int)Math.Round(p.Fraction * progressBarProcess.Maximum);
                if (!string.IsNullOrEmpty(p.Stage))
                    labelProgress.Text = p.Stage;
            });
            var context = new ProcessingContext(Log, progress, cts.Token);

            try
            {
                return await Task.Run(() => work(context, cts.Token), cts.Token);
            }
            catch (OperationCanceledException)
            {
                Log("Операция отменена.");
                return null;
            }
            catch (Exception ex)
            {
                Log("Критическая ошибка: " + ex.Message);
                return null;
            }
            finally
            {
                _operation = null;
                SetBusy(false, "");
                if (_closeWhenIdle)
                    BeginInvoke(Close);
            }
        }

        private void SetBusy(bool busy, string stage)
        {
            foreach (Control c in workRoot.Controls)
            {
                if (c == buttonCancel || c == buttonHelp || c == progressBarProcess
                    || c == labelProgress || c == labelFilterStatus || c == headerPanel)
                    continue;
                c.Enabled = !busy;
            }

            buttonThemeToggle.Enabled = true;
            buttonCancel.Enabled = busy;
            buttonCancel.Visible = busy;
            buttonHelp.Enabled = true;
            progressBarProcess.Visible = busy;
            labelProgress.Visible = busy;
            progressBarProcess.Value = 0;
            labelProgress.Text = stage;
            buttonProcess.Text = busy ? "Обработка..." : "Обработать";
            UseWaitCursor = busy;
            buttonCancel.UseWaitCursor = false;
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            _operation?.Cancel();
            buttonCancel.Enabled = false;
            labelProgress.Text = "Отмена...";
        }

        private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (!IsBusy) return;

            var answer = MessageBox.Show(this,
                "Обработка ещё выполняется. Прервать её и закрыть программу?",
                "LOGER", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            e.Cancel = true;
            if (answer != DialogResult.Yes) return;

            // Закрываемся после отмены: фоновая операция успеет удалить временные файлы.
            _closeWhenIdle = true;
            _operation?.Cancel();
        }

        private async void buttonProcess_Click(object sender, EventArgs e)
        {
            textBoxLog.Clear();

            string canInput = textBoxCanLog.Text.Trim();
            bool isFolderInput = Directory.Exists(canInput);
            OutputFormat outputFormat = _saveOptions.OutputFormat;

            if (string.IsNullOrWhiteSpace(canInput))
            {
                Log("Ошибка: не указан файл лога или папка с логами.");
                return;
            }
            if (!isFolderInput && !File.Exists(canInput))
            {
                Log("Ошибка: файл лога не найден.");
                return;
            }
            if (!IsDevicesFileSelectedAndExists())
            {
                Log("Ошибка: файл посылок (.xlsx, .dbc или .dbf) не найден.");
                return;
            }

            string devFull = Path.GetFullPath(textBoxDevices.Text);
            var allDevices = TryLoadDevices(textBoxDevices.Text, logDetails: true);
            if (allDevices == null) return;

            OutputSettings settings;
            try
            {
                settings = BuildOutputSettings(outputFormat);
            }
            catch (Exception ex)
            {
                Log("Ошибка загрузки составных параметров: " + ex.Message);
                return;
            }

            if (isFolderInput)
            {
                await ProcessFolderAsync(canInput, devFull, allDevices, settings);
                return;
            }

            // Один файл лога — без пакетного режима.
            if (string.IsNullOrWhiteSpace(textBoxOutput.Text))
            {
                Log("Ошибка: не указан путь сохранения.");
                return;
            }

            string outputPath = EnsureOutputPathMatchesFormat(textBoxOutput.Text, outputFormat);
            textBoxOutput.Text = outputPath;

            string? parentDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
            {
                Log($"Ошибка: директория для сохранения не существует: {parentDir}");
                return;
            }

            string outFull = Path.GetFullPath(outputPath);
            if (outFull.Equals(Path.GetFullPath(canInput), StringComparison.OrdinalIgnoreCase))
            {
                Log("Ошибка: файл вывода совпадает с файлом лога. Укажите другой путь.");
                return;
            }
            if (outFull.Equals(devFull, StringComparison.OrdinalIgnoreCase))
            {
                Log("Ошибка: файл вывода совпадает с файлом посылок. Укажите другой путь.");
                return;
            }

            string? block = DescribeWriteBlock(outputPath);
            if (block != null)
            {
                Log("Ошибка: выходной файл: " + block + ".");
                return;
            }

            var service = new LogProcessingService();
            var result = await RunBusyAsync(Path.GetFileName(canInput), (context, _) =>
                service.ProcessSingleFile(canInput, outputPath, allDevices, settings, context));
            if (result == null)
            {
                buttonOpenOutput.Visible = false;
                return;
            }

            if (result.Success)
            {
                Log($"Файл успешно создан (строк: {result.RowsWritten:N0}).");
                buttonOpenOutput.Visible = true;
            }
            else
            {
                Log("Обработка завершилась с ошибкой: выходной файл не создан.");
                buttonOpenOutput.Visible = false;
            }
        }

        private async Task ProcessFolderAsync(string inputFolder, string devFull, List<Device> allDevices, OutputSettings settings)
        {
            if (!TryResolveOutputDirectoryForBatch(textBoxOutput.Text, Log, out string outputDir))
                return;

            if (string.Equals(outputDir, devFull, StringComparison.OrdinalIgnoreCase))
            {
                Log("Ошибка: папка результатов совпадает с путём к файлу посылок. Укажите другую папку.");
                return;
            }

            LogFormatKind formats = _saveOptions.FolderFormatFilter;
            if (formats == LogFormatKind.None)
            {
                Log("Ошибка: в «Параметрах сохранения» не выбран ни один формат логов для папки.");
                return;
            }

            var batchMode = _saveOptions.BatchMode;
            var service = new LogProcessingService();

            var outcome = await RunBusyAsync<BatchRun>("Поиск логов...", (context, token) =>
            {
                // Определение формата читает начало каждого файла — в фоне, чтобы окно не зависало на большой папке.
                var all = LogFolderScanner.EnumerateSupportedLogFiles(inputFolder)
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (all.Count == 0)
                {
                    context.Log("Ошибка: в папке не найдено файлов .csv, .trc, .asc или .txt.");
                    return null;
                }

                var files = new List<string>();
                foreach (var path in all)
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        var kind = LogFormatDetector.Detect(path);
                        if (kind != LogFormatKind.None && (formats & kind) != 0)
                            files.Add(path);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        context.Log($"Пропуск: {Path.GetFileName(path)} — {ex.Message}");
                    }
                }

                if (files.Count == 0)
                {
                    context.Log($"Ошибка: в папке найдено {all.Count} файл(ов), но нет файлов выбранных форматов.");
                    return null;
                }

                context.Log($"Папка с логами: найдено {all.Count} файл(ов), выбрано {files.Count}.");
                context.Log($"Каталог результатов: {outputDir}");
                return new BatchRun(service.ProcessFolderBatch(files, outputDir, devFull, batchMode, allDevices, settings, context));
            });

            if (outcome is not { Outcome: var result })
            {
                buttonOpenOutput.Visible = false;
                return;
            }

            Log($"Готово: создано файлов: {result.Created} из {result.Expected}"
                + (result.Failed > 0 ? $", с ошибками: {result.Failed}." : "."));
            buttonOpenOutput.Visible = result.Created > 0;
        }

        private sealed record BatchRun(LogProcessingService.BatchOutcome Outcome);

        // Снимок фильтров и настроек: фоновая обработка не видит последующих правок в окнах.
        private OutputSettings BuildOutputSettings(OutputFormat outputFormat) => new()
        {
            Format = outputFormat,
            Filter = OutputFilter.From(_deviceEnabled, _paramEnabled),
            Composites = EnsureCompositesLoaded(),
            IncludeDeviceIdHeaderRow = _saveOptions.IncludeDeviceIdHeaderRow,
            DstConnect = _saveOptions.DstConnect,
        };

        private void WireContentSplitLayout()
        {
            _logPanelHeight = Math.Max(
                contentSplit.Panel2MinSize,
                contentSplit.Height - contentSplit.SplitterDistance - contentSplit.SplitterWidth);

            contentSplit.SplitterMoved += (_, _) =>
            {
                if (!_layingOutContentSplit)
                    _logPanelHeight = contentSplit.Panel2.Height;
            };
            contentSplit.Resize += (_, _) => ApplyLogPanelBottomAnchor();
            Resize += (_, _) => ApplyLogPanelBottomAnchor();
            Shown += (_, _) => ApplyLogPanelBottomAnchor();
        }

        /// <summary>Держит журнал у нижнего края окна с сохранённой высотой.</summary>
        private void ApplyLogPanelBottomAnchor()
        {
            if (_layingOutContentSplit || contentSplit.Height <= 0)
                return;

            int splitter = contentSplit.SplitterWidth;
            int minLog = contentSplit.Panel2MinSize;
            int minTop = contentSplit.Panel1MinSize;
            int available = contentSplit.Height - splitter;

            if (available <= minLog + minTop)
                return;

            int logHeight = Math.Clamp(_logPanelHeight, minLog, available - minTop);
            int topHeight = available - logHeight;

            if (topHeight < minTop)
            {
                topHeight = minTop;
                logHeight = Math.Max(minLog, available - topHeight);
            }

            if (contentSplit.SplitterDistance == topHeight)
                return;

            _layingOutContentSplit = true;
            try
            {
                contentSplit.SplitterDistance = topHeight;
            }
            finally
            {
                _layingOutContentSplit = false;
            }

            _logPanelHeight = logHeight;
        }
    }

    // Значение, загруженное из файла; считается актуальным, пока не изменились путь, размер и время записи.
    internal sealed class CachedFile<T> where T : class
    {
        private readonly Func<string, T> _load;
        private string _path = "";
        private DateTime _stamp;
        private long _length;

        public CachedFile(Func<string, T> load) => _load = load;

        public T? Value { get; private set; }

        public bool IsCurrent(string path)
        {
            if (Value == null || !string.Equals(_path, path, StringComparison.OrdinalIgnoreCase)) return false;
            var info = new FileInfo(path);
            return info.Exists && info.LastWriteTimeUtc == _stamp && info.Length == _length;
        }

        public T Get(string path, Action<string>? log = null)
        {
            if (IsCurrent(path)) return Value!;

            var info = new FileInfo(path);
            if (Value != null && string.Equals(_path, path, StringComparison.OrdinalIgnoreCase))
                log?.Invoke($"Файл изменён на диске и перечитан: {Path.GetFileName(path)}");

            Value = _load(path);
            _path = path;
            _stamp = info.LastWriteTimeUtc;
            _length = info.Length;
            return Value;
        }

        public void Clear()
        {
            Value = null;
            _path = "";
        }
    }
}
