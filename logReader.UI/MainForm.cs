using System.Linq;

namespace logReader.UI
{
    public partial class MainForm : Form
    {
        private static readonly IReadOnlyList<FormatConversionPair> _conversionPairs = new List<FormatConversionPair>
        {
            new("trc_to_asc", "TRC -> ASC", ".trc", ".asc"),
        };

        private Dictionary<string, bool> _deviceEnabled = new();
        private Dictionary<string, bool[]> _paramEnabled = new();
        private List<logReader.Device>? _cachedDevices = null;
        private string _cachedDevicesPath = "";

        public MainForm()
        {
            InitializeComponent();
            comboBoxOutputFormat.SelectedIndex = 0;
            UpdateDevicesCreateAddButtonState();
            UpdateFilterLabel();
            buttonOpenOutput.Visible = false;
        }

        private OutputFormat GetSelectedOutputFormat()
        {
            return string.Equals(comboBoxOutputFormat.SelectedItem?.ToString(), "CSV", StringComparison.OrdinalIgnoreCase)
                ? OutputFormat.Csv
                : OutputFormat.Xlsx;
        }

        private static string GetOutputExtension(OutputFormat outputFormat)
            => outputFormat == OutputFormat.Csv ? ".csv" : ".xlsx";

        private static string EnsureOutputPathMatchesFormat(string path, OutputFormat outputFormat)
        {
            string trimmed = path.Trim();
            string desiredExt = GetOutputExtension(outputFormat);
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
                comboBoxOutputFormat.SelectedItem = "CSV";
            else if (ext.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                comboBoxOutputFormat.SelectedItem = "XLSX";
        }

        // ─── Обновить подпись с количеством активных фильтров ─────────────
        private bool IsDevicesFileSelectedAndExists()
        {
            string path = textBoxDevices.Text;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return false;

            string ext = Path.GetExtension(path);
            return ext.Equals(".xlsx", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".dbc", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsDevicesExcelFileSelectedAndExists()
        {
            string path = textBoxDevices.Text;
            return !string.IsNullOrWhiteSpace(path)
                && File.Exists(path)
                && Path.GetExtension(path).Equals(".xlsx", StringComparison.OrdinalIgnoreCase);
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
                    _paramEnabled[d.ID] = Enumerable.Repeat(true, d.headers.Length).ToArray();
                    continue;
                }

                if (arr.Length == d.headers.Length) continue;

                var resized = new bool[d.headers.Length];
                int copyLen = Math.Min(arr.Length, resized.Length);
                Array.Copy(arr, resized, copyLen);
                for (int i = copyLen; i < resized.Length; i++)
                    resized[i] = true;
                _paramEnabled[d.ID] = resized;
            }
        }

        private void UpdateFilterLabel()
        {
            int totalDevices = _cachedDevices?.Count ?? 0;
            int enabledDevices = totalDevices == 0
                ? 0
                : _cachedDevices!.Count(d => _deviceEnabled.GetValueOrDefault(d.ID, true));

            // Параметры выключенного устройства не считаются включёнными
            int totalParams = _cachedDevices?.Sum(d => d.headers.Length) ?? 0;
            int enabledParams = totalParams == 0
                ? 0
                : _cachedDevices!.Sum(d =>
                {
                    bool devOn = _deviceEnabled.GetValueOrDefault(d.ID, true);
                    if (!devOn) return 0; // устройство выключено — все его параметры тоже
                    if (!_paramEnabled.TryGetValue(d.ID, out var arr))
                        return d.headers.Length;
                    int len = Math.Min(arr.Length, d.headers.Length);
                    int enabled = arr.Take(len).Count(v => v);
                    enabled += d.headers.Length - len;
                    return enabled;
                });

            if (totalDevices == 0)
            {
                labelFilterStatus.Text = "Файл посылок не загружен";
                labelFilterStatus.ForeColor = Color.DarkGray;
            }
            else
            {
                labelFilterStatus.Text = $"Устройства: {enabledDevices}/{totalDevices}  Параметры: {enabledParams}/{totalParams}";
                labelFilterStatus.ForeColor = Color.Black;
            }
        }

        private enum LogSourceKind { None, File, Folder }

        private LogSourceKind ShowPickLogSourceDialog()
        {
            using var dlg = new Form();
            dlg.Text = "Источник логов";
            dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
            dlg.StartPosition = FormStartPosition.CenterParent;
            dlg.MinimizeBox = false;
            dlg.MaximizeBox = false;
            dlg.ShowInTaskbar = false;
            dlg.ClientSize = new Size(360, 100);
            dlg.MinimumSize = dlg.Size;
            dlg.MaximumSize = dlg.Size;

            var lbl = new Label
            {
                Text = "Выберите один файл лога или папку с логами:",
                AutoSize = true,
                Location = new Point(12, 12),
            };
            var btnFile = new Button { Text = "Файл...", Location = new Point(12, 44), Size = new Size(100, 26) };
            var btnFolder = new Button { Text = "Папка...", Location = new Point(120, 44), Size = new Size(100, 26) };
            var btnCancel = new Button { Text = "Отмена", Location = new Point(228, 44), Size = new Size(100, 26) };

            var kind = LogSourceKind.None;
            btnFile.Click += (_, _) => { kind = LogSourceKind.File; dlg.Close(); };
            btnFolder.Click += (_, _) => { kind = LogSourceKind.Folder; dlg.Close(); };
            btnCancel.Click += (_, _) => { dlg.Close(); };

            dlg.Controls.AddRange(new Control[] { lbl, btnFile, btnFolder, btnCancel });
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
                ofd.Filter = "Лог файлы (*.csv;*.trc;*.asc)|*.csv;*.trc;*.asc|CSV (*.csv)|*.csv|pCAN (*.trc)|*.trc|ASC (*.asc)|*.asc";
                if (ofd.ShowDialog() != DialogResult.OK) return;
                textBoxCanLog.Text = ofd.FileName;

                string dir = Path.GetDirectoryName(ofd.FileName) ?? "";
                string ext = GetOutputExtension(GetSelectedOutputFormat());
                string name = Path.GetFileNameWithoutExtension(ofd.FileName) + "_result" + ext;
                textBoxOutput.Text = Path.Combine(dir, name);
                return;
            }

            using var fbd = new FolderBrowserDialog();
            fbd.Description = "Выберите папку с логами (.csv, .trc, .asc)";
            if (fbd.ShowDialog() != DialogResult.OK) return;

            textBoxCanLog.Text = fbd.SelectedPath;
            textBoxOutput.Text = Path.Combine(fbd.SelectedPath, "result");
        }

        private void buttonViewLog_Click(object sender, EventArgs e)
        {
            string path = textBoxCanLog.Text;
            if (string.IsNullOrWhiteSpace(path))
            {
                Log("Ошибка: сначала укажите файл лога (.csv, .trc или .asc).");
                return;
            }

            if (Directory.Exists(path))
            {
                string? first = EnumerateLogFilesInFolder(path)
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();
                if (first == null)
                {
                    Log("Ошибка: в папке нет файлов .csv, .trc или .asc.");
                    return;
                }
                path = first;
            }
            else if (!File.Exists(path))
            {
                Log("Ошибка: файл лога не найден.");
                return;
            }

            var viewForm = new CanLogViewForm(path);
            viewForm.ShowDialog(this);
        }

        private void buttonDevices_Click(object sender, EventArgs e)
        {
            using OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Файлы посылок (*.xlsx;*.dbc)|*.xlsx;*.dbc|Excel files (*.xlsx)|*.xlsx|DBC files (*.dbc)|*.dbc";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                if (textBoxDevices.Text != ofd.FileName)
                {
                    _cachedDevices = null;
                    _cachedDevicesPath = "";
                    _deviceEnabled = new();
                    _paramEnabled = new();
                    UpdateFilterLabel();
                }
                textBoxDevices.Text = ofd.FileName;
            }
        }

        private void textBoxDevices_TextChanged(object sender, EventArgs e)
        {
            UpdateDevicesCreateAddButtonState();
            // Автоматически загружаем устройства и обновляем статус при смене файла
            if (!File.Exists(textBoxDevices.Text))
            {
                _cachedDevices = null;
                _cachedDevicesPath = "";
                UpdateFilterLabel();
                return;
            }

            if (_cachedDevicesPath == textBoxDevices.Text) return; // уже загружен

            try
            {
                _cachedDevices = logReader.Program.LoadDevicesFromFile(textBoxDevices.Text, _ => { });
                _cachedDevicesPath = textBoxDevices.Text;
                // Сбрасываем фильтры только если это новый файл
                _deviceEnabled = new();
                _paramEnabled = new();
            }
            catch (Exception ex)
            {
                _cachedDevices = null;
                _cachedDevicesPath = "";
                Log($"Ошибка загрузки файла посылок: {ex.Message}");
            }

            UpdateFilterLabel();
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
            else
            {
                sfd.Filter = "DBC files (*.dbc)|*.dbc";
                sfd.DefaultExt = "dbc";
            }
            sfd.AddExtension = true;
            if (sfd.ShowDialog() != DialogResult.OK) return;

            try
            {
                if (kindDlg.SelectedKind == FileKindPromptForm.FileKind.Xlsx)
                    logReader.DeviceExcelFile.CreateDevicesExcelTemplate(sfd.FileName);
                else
                    logReader.DbcFile.CreateEmpty(sfd.FileName);

                _cachedDevices = null;
                _cachedDevicesPath = "";
                _deviceEnabled = new();
                _paramEnabled = new();
                UpdateFilterLabel();

                textBoxDevices.Text = sfd.FileName;
                Log("Файл посылок создан. Откроется редактор.");

                OpenDevicesEditor();
            }
            catch (Exception ex)
            {
                Log("Ошибка создания файла посылок: " + ex.Message);
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

            try { using var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None); }
            catch
            {
                Log("Ошибка: файл посылок уже открыт в другой программе. Закройте его и попробуйте снова.");
                return;
            }

            using var editor = new DevicesEditorForm(path);
            editor.ShowDialog(this);

            if (editor.Modified)
            {
                try
                {
                    _cachedDevices = logReader.Program.LoadDevicesFromFile(path, Log);
                    _cachedDevicesPath = path;
                    EnsureFiltersMatchDevices();
                    UpdateFilterLabel();
                    Log("Файл посылок обновлён.");
                }
                catch (Exception ex)
                {
                    Log("Ошибка перезагрузки файла посылок: " + ex.Message);
                }
            }
        }

        private void buttonOutput_Click(object sender, EventArgs e)
        {
            using SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "Excel files (*.xlsx)|*.xlsx|CSV files (*.csv)|*.csv|Excel/CSV (*.xlsx;*.csv)|*.xlsx;*.csv";
            sfd.DefaultExt = GetOutputExtension(GetSelectedOutputFormat()).TrimStart('.');
            sfd.AddExtension = true;
            sfd.FilterIndex = GetSelectedOutputFormat() == OutputFormat.Csv ? 2 : 1;
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                textBoxOutput.Text = sfd.FileName;
                SyncOutputFormatWithPath(sfd.FileName);
                buttonOpenOutput.Visible = false;
            }
        }

        private static bool IsPCanLog(string path) =>
            Path.GetExtension(path).Equals(".trc", StringComparison.OrdinalIgnoreCase);

        private static bool IsAscLog(string path) =>
            Path.GetExtension(path).Equals(".asc", StringComparison.OrdinalIgnoreCase);

        private static IEnumerable<string> EnumerateLogFilesInFolder(string folder)
        {
            string[] patterns = { "*.csv", "*.trc", "*.asc" };
            foreach (string pattern in patterns)
            {
                foreach (string path in Directory.EnumerateFiles(folder, pattern, SearchOption.TopDirectoryOnly))
                    yield return path;
            }
        }

        private static string BuildBatchOutputPath(string logFilePath, string outputFolder, OutputFormat outputFormat)
        {
            string stem = Path.GetFileNameWithoutExtension(logFilePath);
            string ext = Path.GetExtension(logFilePath).TrimStart('.');
            if (string.IsNullOrEmpty(ext)) ext = "log";
            return Path.Combine(outputFolder, $"{stem}_{ext}_result{GetOutputExtension(outputFormat)}");
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
                if ((File.GetAttributes(t) & FileAttributes.Directory) != FileAttributes.Directory)
                {
                    log("Ошибка: для обработки папки укажите каталог для результатов, а не файл.");
                    return false;
                }
                outputDir = Path.GetFullPath(t);
                return true;
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
            catch (Exception ex)
            {
                log("Ошибка: не удалось создать папку результатов: " + ex.Message);
                return false;
            }
        }

        private static void ProcessSingleLogFile(
            string logPath,
            string outputPath,
            OutputFormat outputFormat,
            List<logReader.Device> allDevices,
            bool hasFilter,
            Dictionary<string, bool> deviceEnabled,
            Dictionary<string, bool[]> paramEnabled,
            Action<string> log)
        {
            bool isPCan = IsPCanLog(logPath);
            bool isAsc = IsAscLog(logPath);
            if (isPCan) log("Формат: pCAN Viewer");
            else if (isAsc) log("Формат: ASC");
            else log("Формат: CAN лог");

            if (isPCan)
            {
                var processor = new PCanLogProcessor();
                processor.Process(
                    logPath,
                    allDevices,
                    outputPath,
                    outputFormat,
                    log,
                    hasFilter ? deviceEnabled : null,
                    hasFilter ? paramEnabled : null);
            }
            else if (isAsc)
            {
                var processor = new AscLogProcessor();
                processor.Process(
                    logPath,
                    allDevices,
                    outputPath,
                    outputFormat,
                    log,
                    hasFilter ? deviceEnabled : null,
                    hasFilter ? paramEnabled : null);
            }
            else
            {
                var processor = new CanLogProcessor();
                processor.Process(
                    logPath,
                    allDevices,
                    outputPath,
                    outputFormat,
                    log,
                    hasFilter ? deviceEnabled : null,
                    hasFilter ? paramEnabled : null);
            }
        }

        private void textBoxOutput_TextChanged(object sender, EventArgs e)
        {
            SyncOutputFormatWithPath(textBoxOutput.Text);
            buttonOpenOutput.Visible = false;
        }

        private void comboBoxOutputFormat_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(textBoxOutput.Text))
                return;

            if (Directory.Exists(textBoxCanLog.Text.Trim()))
                return;

            if (!LooksLikeFilePath(textBoxOutput.Text))
                return;

            textBoxOutput.Text = EnsureOutputPathMatchesFormat(textBoxOutput.Text, GetSelectedOutputFormat());
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
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = p,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Log("Не удалось открыть: " + ex.Message);
            }
        }

        private void buttonHelp_Click(object sender, EventArgs e)
        {
            var helpForm = new HelpForm();
            helpForm.Show(this);
        }

        private async void buttonFormatConvert_Click(object sender, EventArgs e)
        {
            textBoxLog.Clear();

            string initialPath = File.Exists(textBoxCanLog.Text) ? textBoxCanLog.Text : "";
            using var dialog = new FormatConversionDialog(_conversionPairs, initialPath);
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            string inputPath = dialog.SelectedInputPath;
            string outPath = dialog.SelectedOutputPath;
            FormatConversionPair pair = dialog.SelectedPair;

            if (string.IsNullOrWhiteSpace(inputPath))
            {
                Log("Ошибка: файл лога не найден.");
                return;
            }
            if (Directory.Exists(inputPath))
            {
                Log("Ошибка: для конвертации нужно выбрать файл, а не папку.");
                return;
            }
            if (!File.Exists(inputPath))
            {
                Log("Ошибка: файл лога не найден.");
                return;
            }

            string inputExt = Path.GetExtension(inputPath);
            if (!inputExt.Equals(pair.SourceExtension, StringComparison.OrdinalIgnoreCase))
            {
                Log($"Ошибка: выбранный файл не соответствует формату источника ({pair.SourceExtension}).");
                return;
            }

            if (string.IsNullOrWhiteSpace(outPath))
            {
                Log("Ошибка: укажите путь выходного файла.");
                return;
            }

            if (!Path.GetExtension(outPath).Equals(pair.TargetExtension, StringComparison.OrdinalIgnoreCase))
                outPath = Path.ChangeExtension(outPath, pair.TargetExtension);

            string? outDir = Path.GetDirectoryName(outPath);
            if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
            {
                Log($"Ошибка: директория для сохранения не существует: {outDir}");
                return;
            }

            string outFull = Path.GetFullPath(outPath);
            string inFull = Path.GetFullPath(inputPath);
            if (outFull.Equals(inFull, StringComparison.OrdinalIgnoreCase))
            {
                Log("Ошибка: файл вывода совпадает с файлом лога. Укажите другой путь.");
                return;
            }

            if (File.Exists(outPath))
            {
                try { using var fs = new FileStream(outPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None); }
                catch
                {
                    Log("Ошибка: выходной файл уже открыт в другой программе. Закройте его и попробуйте снова.");
                    return;
                }
            }

            buttonTrcToAsc.Enabled = false;
            buttonTrcToAsc.Text = "Конвертация...";
            Cursor = Cursors.WaitCursor;

            try
            {
                await Task.Run(() =>
                {
                    if (pair.Id == "trc_to_asc")
                    {
                        var converter = new TrcToAscConverter();
                        converter.Convert(inputPath, outPath, Log);
                        return;
                    }

                    Log($"Ошибка: конвертация {pair.DisplayName} пока не поддерживается.");
                });
            }
            catch (Exception ex)
            {
                Log("Критическая ошибка: " + ex.Message);
            }

            buttonTrcToAsc.Enabled = true;
            buttonTrcToAsc.Text = "Смена формата";
            Cursor = Cursors.Default;
        }

        private void buttonDevicesParams_Click(object sender, EventArgs e)
        {
            if (!File.Exists(textBoxDevices.Text))
            {
                Log("Ошибка: сначала укажите файл посылок (.xlsx или .dbc).");
                return;
            }
            try
            {
                if (_cachedDevices == null || _cachedDevicesPath != textBoxDevices.Text)
                {
                    _cachedDevices = logReader.Program.LoadDevicesFromFile(textBoxDevices.Text, Log);
                    _cachedDevicesPath = textBoxDevices.Text;
                }

                if (_cachedDevices.Count == 0)
                {
                    Log("Ошибка: устройства не загружены из файла.");
                    return;
                }

                EnsureFiltersMatchDevices();
                var form = new Devices_ParametrsForm(_cachedDevices, _deviceEnabled, _paramEnabled);
                form.ShowDialog(this);

                // Обновляем статус фильтров после закрытия диалога
                UpdateFilterLabel();
            }
            catch (Exception ex)
            {
                Log("Ошибка: " + ex.Message);
            }
        }

        private void Log(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(Log), message);
                return;
            }
            textBoxLog.AppendText(message + Environment.NewLine);
        }

        private async void buttonProcess_Click(object sender, EventArgs e)
        {
            textBoxLog.Clear();

            string canInput = textBoxCanLog.Text.Trim();
            bool isFolderInput = Directory.Exists(canInput);
            OutputFormat outputFormat = GetSelectedOutputFormat();

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
                Log("Ошибка: файл посылок (.xlsx или .dbc) не найден.");
                return;
            }

            string devFull = Path.GetFullPath(textBoxDevices.Text);

            if (isFolderInput)
            {
                if (!TryResolveOutputDirectoryForBatch(textBoxOutput.Text, Log, out string outputDir))
                    return;

                if (string.Equals(outputDir, devFull, StringComparison.OrdinalIgnoreCase))
                {
                    Log("Ошибка: папка результатов совпадает с путём к файлу посылок. Укажите другую папку.");
                    return;
                }

                var files = EnumerateLogFilesInFolder(canInput)
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (files.Count == 0)
                {
                    Log("Ошибка: в папке не найдено файлов .csv, .trc или .asc.");
                    return;
                }

                buttonProcess.Enabled = false;
                buttonProcess.Text = "Обработка...";
                Cursor = Cursors.WaitCursor;

                try
                {
                    if (_cachedDevices == null || _cachedDevicesPath != textBoxDevices.Text)
                    {
                        _cachedDevices = logReader.Program.LoadDevicesFromFile(textBoxDevices.Text, Log);
                        _cachedDevicesPath = textBoxDevices.Text;
                    }

                    var allDevices = _cachedDevices;
                    bool anyDeviceOff = _deviceEnabled.Any(kv => !kv.Value);
                    bool anyParamOff = _paramEnabled.Any(kv => kv.Value.Any(v => !v));
                    var hasFilter = anyDeviceOff || anyParamOff;

                    Log($"Папка с логами: найдено {files.Count} файл(ов).");
                    Log($"Каталог результатов: {outputDir}");

                    int ok = 0;
                    await Task.Run(() =>
                    {
                        foreach (string logPath in files)
                        {
                            string outPath = BuildBatchOutputPath(logPath, outputDir, outputFormat);
                            if (string.Equals(Path.GetFullPath(outPath), devFull, StringComparison.OrdinalIgnoreCase))
                            {
                                Log($"Пропуск: совпадает с файлом посылок — {Path.GetFileName(outPath)}");
                                continue;
                            }

                            if (File.Exists(outPath))
                            {
                                try
                                {
                                    using var fs = new FileStream(outPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                                }
                                catch
                                {
                                    Log($"Пропуск (файл занят): {Path.GetFileName(outPath)}");
                                    continue;
                                }
                            }

                            Log($"--- {Path.GetFileName(logPath)} ---");
                            ProcessSingleLogFile(
                                logPath,
                                outPath,
                                outputFormat,
                                allDevices,
                                hasFilter,
                                _deviceEnabled,
                                _paramEnabled,
                                Log);

                            if (File.Exists(outPath))
                                ok++;
                        }
                    });

                    Log($"Готово: создано файлов: {ok} из {files.Count}.");
                    buttonOpenOutput.Visible = ok > 0;
                }
                catch (Exception ex)
                {
                    Log("Критическая ошибка: " + ex.Message);
                }

                buttonProcess.Enabled = true;
                buttonProcess.Text = "Обработать";
                Cursor = Cursors.Default;
                return;
            }

            // Один файл лога
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
            string logFull = Path.GetFullPath(canInput);

            if (outFull.Equals(logFull, StringComparison.OrdinalIgnoreCase))
            {
                Log("Ошибка: файл вывода совпадает с файлом лога. Укажите другой путь.");
                return;
            }
            if (outFull.Equals(devFull, StringComparison.OrdinalIgnoreCase))
            {
                Log("Ошибка: файл вывода совпадает с файлом посылок. Укажите другой путь.");
                return;
            }

            if (File.Exists(outputPath))
            {
                try { using var fs = new FileStream(outputPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None); }
                catch
                {
                    Log("Ошибка: выходной файл уже открыт в другой программе. Закройте его и попробуйте снова.");
                    return;
                }
            }

            buttonProcess.Enabled = false;
            buttonProcess.Text = "Обработка...";
            Cursor = Cursors.WaitCursor;

            try
            {
                if (_cachedDevices == null || _cachedDevicesPath != textBoxDevices.Text)
                {
                    _cachedDevices = logReader.Program.LoadDevicesFromFile(textBoxDevices.Text, Log);
                    _cachedDevicesPath = textBoxDevices.Text;
                }

                var allDevices = _cachedDevices;
                bool anyDeviceOff = _deviceEnabled.Any(kv => !kv.Value);
                bool anyParamOff = _paramEnabled.Any(kv => kv.Value.Any(v => !v));
                var hasFilter = anyDeviceOff || anyParamOff;

                await Task.Run(() =>
                {
                    ProcessSingleLogFile(
                        canInput,
                        outputPath,
                        outputFormat,
                        allDevices,
                        hasFilter,
                        _deviceEnabled,
                        _paramEnabled,
                        Log);
                });

                if (File.Exists(outputPath))
                {
                    Log("Файл успешно создан.");
                    buttonOpenOutput.Visible = true;
                }
                else
                {
                    Log("Обработка завершилась с ошибкой: выходной файл не был создан.");
                    buttonOpenOutput.Visible = false;
                }
            }
            catch (Exception ex)
            {
                Log("Критическая ошибка: " + ex.Message);
            }

            buttonProcess.Enabled = true;
            buttonProcess.Text = "Обработать";
            Cursor = Cursors.Default;
        }
    }
}
