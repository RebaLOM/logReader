using System.Linq;

namespace logReader.UI
{
    public partial class MainForm : Form
    {
        private Dictionary<string, bool> _deviceEnabled = new();
        private Dictionary<string, bool[]> _paramEnabled = new();

        public MainForm()
        {
            InitializeComponent();
        }

        private void buttonCANlog_Click(object sender, EventArgs e)
        {
            using OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "CSV files (*.csv)|*.csv";
            if (ofd.ShowDialog() == DialogResult.OK)
                textBoxCanLog.Text = ofd.FileName;
        }

        private void buttonDevices_Click(object sender, EventArgs e)
        {
            using OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Excel files (*.xlsx)|*.xlsx";

            if (ofd.ShowDialog() == DialogResult.OK)
                textBoxDevices.Text = ofd.FileName;
        }

        private void buttonOutput_Click(object sender, EventArgs e)
        {
            using SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "Excel files (*.xlsx)|*.xlsx";
            if (sfd.ShowDialog() == DialogResult.OK)
                textBoxOutput.Text = sfd.FileName;

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
                var devices = logReader.Program.LoadDevicesFromExcel(textBoxDevices.Text, Log);
                if (devices.Count == 0)
                {
                    Log("Ошибка: устройства не загружены из файла.");
                    return;
                }
                var form = new Devices_ParametrsForm(devices, _deviceEnabled, _paramEnabled);
                form.ShowDialog(this);
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
                Log("Ошибка: CAN лог не найден.");
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

            buttonProcess.Enabled = false;

            try
            {
                var allDevices = logReader.Program.LoadDevicesFromExcel(textBoxDevices.Text, Log);
                var hasFilter = _deviceEnabled.Count > 0 || _paramEnabled.Count > 0;

                await Task.Run(() =>
                {
                    var processor = new CanLogProcessor();
                    processor.Process(
                        textBoxCanLog.Text,
                        allDevices,
                        textBoxOutput.Text,
                        Log,
                        hasFilter ? _deviceEnabled : null,
                        hasFilter ? _paramEnabled : null
                    );
                });

                Log("Файл успешно создан.");
            }
            catch (Exception ex)
            {
                Log("Критическая ошибка: " + ex.Message);
            }

            buttonProcess.Enabled = true;

        }
    }
}
