namespace logReader.UI
{
    public partial class Form1 : Form
    {
        public Form1()
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
                await Task.Run(() =>
                {
                    var processor = new CanLogProcessor();
                    processor.Process(
                        textBoxCanLog.Text,
                        textBoxDevices.Text,
                        textBoxOutput.Text,
                        Log
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
