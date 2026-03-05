using System.Linq;

namespace logReader.UI
{
    public partial class MainForm : Form
    {
        private Dictionary<string, bool> _deviceEnabled = new();
        private Dictionary<string, bool[]> _paramEnabled = new();
        private List<logReader.Device>? _cachedDevices = null;
        private string _cachedDevicesPath = "";

        public MainForm()
        {
            InitializeComponent();
            UpdateFilterLabel();
            buttonOpenOutput.Visible = false;
        }

        // ─── Обновить подпись с количеством активных фильтров ─────────────
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
                    return arr.Count(v => v);
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

        private void buttonCANlog_Click(object sender, EventArgs e)
        {
            using OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Лог файлы (*.csv;*.trc)|*.csv;*.trc|CSV (*.csv)|*.csv|pCAN (*.trc)|*.trc";
            if (ofd.ShowDialog() != DialogResult.OK) return;
            textBoxCanLog.Text = ofd.FileName;

            // Автоподстановка пути вывода если поле пустое
            if (string.IsNullOrWhiteSpace(textBoxOutput.Text))
            {
                string dir = Path.GetDirectoryName(ofd.FileName) ?? "";
                string name = Path.GetFileNameWithoutExtension(ofd.FileName) + "_result.xlsx";
                textBoxOutput.Text = Path.Combine(dir, name);
            }
        }

        private void buttonViewLog_Click(object sender, EventArgs e)
        {
            if (!File.Exists(textBoxCanLog.Text))
            {
                Log("Ошибка: сначала укажите файл лога (.csv или .trc).");
                return;
            }
            var viewForm = new CanLogViewForm(textBoxCanLog.Text);
            viewForm.ShowDialog(this);
        }

        private void buttonDevices_Click(object sender, EventArgs e)
        {
            using OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Excel files (*.xlsx)|*.xlsx";
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
                _cachedDevices = logReader.Program.LoadDevicesFromExcel(textBoxDevices.Text, _ => { });
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

        private void buttonOutput_Click(object sender, EventArgs e)
        {
            using SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "Excel files (*.xlsx)|*.xlsx";
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                textBoxOutput.Text = sfd.FileName;
                buttonOpenOutput.Visible = false;
            }
        }

        private static bool IsPCanLog(string path) =>
            Path.GetExtension(path).Equals(".trc", StringComparison.OrdinalIgnoreCase);

        private void textBoxOutput_TextChanged(object sender, EventArgs e)
        {
            buttonOpenOutput.Visible = false;
        }

        private void buttonOpenOutput_Click(object sender, EventArgs e)
        {
            if (!File.Exists(textBoxOutput.Text))
            {
                Log("Файл не найден.");
                return;
            }
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = textBoxOutput.Text,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Log("Не удалось открыть файл: " + ex.Message);
            }
        }

        private void buttonHelp_Click(object sender, EventArgs e)
        {
            var helpForm = new HelpForm();
            helpForm.Show(this);
        }

        private void buttonDevicesParams_Click(object sender, EventArgs e)
        {
            if (!File.Exists(textBoxDevices.Text))
            {
                Log("Ошибка: сначала укажите файл посылок (.xlsx).");
                return;
            }
            try
            {
                if (_cachedDevices == null || _cachedDevicesPath != textBoxDevices.Text)
                {
                    _cachedDevices = logReader.Program.LoadDevicesFromExcel(textBoxDevices.Text, Log);
                    _cachedDevicesPath = textBoxDevices.Text;
                }

                if (_cachedDevices.Count == 0)
                {
                    Log("Ошибка: устройства не загружены из файла.");
                    return;
                }

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

            if (!File.Exists(textBoxCanLog.Text))
            {
                Log("Ошибка: файл лога не найден.");
                return;
            }
            if (!File.Exists(textBoxDevices.Text))
            {
                Log("Ошибка: Excel файл устройств не найден.");
                return;
            }
            if (string.IsNullOrWhiteSpace(textBoxOutput.Text))
            {
                Log("Ошибка: не указан путь сохранения.");
                return;
            }

            // Проверяем что директория для сохранения существует
            string? outputDir = Path.GetDirectoryName(textBoxOutput.Text);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Log($"Ошибка: директория для сохранения не существует: {outputDir}");
                return;
            }

            // Защита: выходной файл не должен совпадать с входными
            string outFull = Path.GetFullPath(textBoxOutput.Text);
            string logFull = Path.GetFullPath(textBoxCanLog.Text);
            string devFull = Path.GetFullPath(textBoxDevices.Text);

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

            // Проверяем что выходной файл не заблокирован другой программой (например Excel)
            if (File.Exists(textBoxOutput.Text))
            {
                try { using var fs = new FileStream(textBoxOutput.Text, FileMode.Open, FileAccess.ReadWrite, FileShare.None); }
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
                    _cachedDevices = logReader.Program.LoadDevicesFromExcel(textBoxDevices.Text, Log);
                    _cachedDevicesPath = textBoxDevices.Text;
                }

                var allDevices = _cachedDevices;
                // Передаём фильтры только если что-то реально выключено
                bool anyDeviceOff = _deviceEnabled.Any(kv => !kv.Value);
                bool anyParamOff = _paramEnabled.Any(kv => kv.Value.Any(v => !v));
                var hasFilter = anyDeviceOff || anyParamOff;

                string logPath = textBoxCanLog.Text;
                bool isPCan = IsPCanLog(logPath);
                if (isPCan) Log("Формат: pCAN Viewer");
                else Log("Формат: CAN лог");

                await Task.Run(() =>
                {
                    if (isPCan)
                    {
                        var processor = new PCanLogProcessor();
                        processor.Process(
                            logPath,
                            allDevices,
                            textBoxOutput.Text,
                            Log,
                            hasFilter ? _deviceEnabled : null,
                            hasFilter ? _paramEnabled : null
                        );
                    }
                    else
                    {
                        var processor = new CanLogProcessor();
                        processor.Process(
                            logPath,
                            allDevices,
                            textBoxOutput.Text,
                            Log,
                            hasFilter ? _deviceEnabled : null,
                            hasFilter ? _paramEnabled : null
                        );
                    }
                });

                if (File.Exists(textBoxOutput.Text))
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