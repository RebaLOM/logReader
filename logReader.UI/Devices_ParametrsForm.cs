using System.Linq;
using logReader;

namespace logReader.UI
{
    public partial class Devices_ParametrsForm : Form
    {
        private readonly List<Device> _devices;
        private readonly Dictionary<string, bool> _deviceEnabled;
        private readonly Dictionary<string, bool[]> _paramEnabled;

        public Devices_ParametrsForm(List<Device> devices,
            Dictionary<string, bool> deviceEnabled,
            Dictionary<string, bool[]> paramEnabled)
        {
            InitializeComponent();
            _devices = devices;
            _deviceEnabled = deviceEnabled;
            _paramEnabled = paramEnabled;
            BuildDevicePanels();
        }

        private void BuildDevicePanels()
        {
            scrollPanel.Controls.Clear();
            scrollPanel.SuspendLayout();

            int yOffset = 8;

            foreach (var device in _devices)
            {
                // Инициализируем состояние если нет
                if (!_deviceEnabled.ContainsKey(device.ID))
                    _deviceEnabled[device.ID] = true;
                if (!_paramEnabled.ContainsKey(device.ID))
                    _paramEnabled[device.ID] = Enumerable.Repeat(true, device.headers.Length).ToArray();

                bool devOn = _deviceEnabled[device.ID];
                bool[] paramArr = _paramEnabled[device.ID];

                // ── Контейнер устройства ──────────────────────────────────
                var groupBox = new GroupBox
                {
                    Text = "",
                    Left = 8,
                    Top = yOffset,
                    Width = scrollPanel.ClientSize.Width - 24,
                    Anchor = AnchorStyles.Left | AnchorStyles.Right,
                    Padding = new Padding(6, 4, 6, 6),
                    Tag = device.ID
                };

                int innerY = 22;

                // ── Заголовок: ID устройства + кнопка вкл/выкл ───────────
                var headerPanel = new Panel
                {
                    Left = 6,
                    Top = innerY,
                    Height = 32,
                    Width = groupBox.ClientSize.Width - 12,
                    Anchor = AnchorStyles.Left | AnchorStyles.Right,
                    BackColor = Color.FromArgb(230, 235, 245)
                };

                var lblDevice = new Label
                {
                    Text = $"Устройство: {device.ID}",
                    Left = 8,
                    Top = 7,
                    AutoSize = true,
                    Font = new Font(Font, FontStyle.Bold),
                    Tag = device.ID
                };

                var btnToggleDevice = new CheckBox
                {
                    Text = devOn ? "Включено" : "Выключено",
                    Checked = devOn,
                    Appearance = Appearance.Button,
                    Left = headerPanel.Width - 120,
                    Top = 4,
                    Width = 110,
                    Height = 24,
                    Anchor = AnchorStyles.Right,
                    Tag = device.ID,
                    BackColor = devOn ? Color.FromArgb(180, 220, 180) : Color.FromArgb(220, 180, 180),
                    FlatStyle = FlatStyle.Flat,
                    TextAlign = ContentAlignment.MiddleCenter
                };
                btnToggleDevice.FlatAppearance.BorderSize = 0;
                btnToggleDevice.CheckedChanged += DeviceToggle_CheckedChanged;

                headerPanel.Controls.Add(lblDevice);
                headerPanel.Controls.Add(btnToggleDevice);
                groupBox.Controls.Add(headerPanel);

                innerY += headerPanel.Height + 6;

                // ── Разделительная линия ─────────────────────────────────
                var separator = new Panel
                {
                    Left = 6,
                    Top = innerY,
                    Width = groupBox.ClientSize.Width - 12,
                    Height = 1,
                    BackColor = Color.Silver,
                    Anchor = AnchorStyles.Left | AnchorStyles.Right
                };
                groupBox.Controls.Add(separator);
                innerY += 6;

                // ── Параметры ────────────────────────────────────────────
                for (int i = 0; i < device.headers.Length; i++)
                {
                    bool paramOn = i < paramArr.Length ? paramArr[i] : true;

                    var paramPanel = new Panel
                    {
                        Left = 6,
                        Top = innerY,
                        Height = 28,
                        Width = groupBox.ClientSize.Width - 12,
                        Anchor = AnchorStyles.Left | AnchorStyles.Right,
                        BackColor = i % 2 == 0 ? Color.White : Color.FromArgb(245, 245, 250),
                        Tag = (device.ID, i)
                    };

                    var lblParam = new Label
                    {
                        Text = device.headers[i],
                        Left = 10,
                        Top = 6,
                        Width = paramPanel.Width - 140,
                        Anchor = AnchorStyles.Left | AnchorStyles.Right,
                        AutoEllipsis = true
                    };

                    var btnToggleParam = new CheckBox
                    {
                        Text = paramOn ? "Вкл" : "Выкл",
                        Checked = paramOn,
                        Appearance = Appearance.Button,
                        Left = paramPanel.Width - 75,
                        Top = 2,
                        Width = 65,
                        Height = 22,
                        Anchor = AnchorStyles.Right,
                        Tag = (device.ID, i),
                        BackColor = paramOn ? Color.FromArgb(200, 230, 200) : Color.FromArgb(230, 200, 200),
                        FlatStyle = FlatStyle.Flat,
                        TextAlign = ContentAlignment.MiddleCenter
                    };
                    btnToggleParam.FlatAppearance.BorderSize = 0;
                    btnToggleParam.CheckedChanged += ParamToggle_CheckedChanged;

                    paramPanel.Controls.Add(lblParam);
                    paramPanel.Controls.Add(btnToggleParam);
                    groupBox.Controls.Add(paramPanel);

                    innerY += 30;
                }

                innerY += 8;
                groupBox.Height = innerY;
                scrollPanel.Controls.Add(groupBox);
                yOffset += groupBox.Height + 10;
            }

            scrollPanel.ResumeLayout();
        }

        private void DeviceToggle_CheckedChanged(object? sender, EventArgs e)
        {
            if (sender is not CheckBox cb || cb.Tag is not string deviceId) return;

            bool isOn = cb.Checked;
            cb.Text = isOn ? "Включено" : "Выключено";
            cb.BackColor = isOn ? Color.FromArgb(180, 220, 180) : Color.FromArgb(220, 180, 180);

            _deviceEnabled[deviceId] = isOn;
        }

        private void ParamToggle_CheckedChanged(object? sender, EventArgs e)
        {
            if (sender is not CheckBox cb || cb.Tag is not (string deviceId, int paramIdx)) return;

            bool isOn = cb.Checked;
            cb.Text = isOn ? "Вкл" : "Выкл";
            cb.BackColor = isOn ? Color.FromArgb(200, 230, 200) : Color.FromArgb(230, 200, 200);

            if (!_paramEnabled.ContainsKey(deviceId))
            {
                var dev = _devices.First(d => d.ID == deviceId);
                _paramEnabled[deviceId] = Enumerable.Repeat(true, dev.headers.Length).ToArray();
            }

            if (paramIdx < _paramEnabled[deviceId].Length)
                _paramEnabled[deviceId][paramIdx] = isOn;
        }

        private void SetAllDevices(bool value)
        {
            foreach (Control ctrl in scrollPanel.Controls)
            {
                if (ctrl is not GroupBox gb || gb.Tag is not string deviceId) continue;

                // Кнопка устройства
                var headerPanel = gb.Controls.OfType<Panel>().FirstOrDefault();
                if (headerPanel != null)
                {
                    var devBtn = headerPanel.Controls.OfType<CheckBox>().FirstOrDefault();
                    if (devBtn != null)
                    {
                        devBtn.Checked = value;
                        // CheckedChanged обновит _deviceEnabled
                    }
                }

                // Кнопки параметров
                foreach (var paramPanel in gb.Controls.OfType<Panel>().Skip(1))
                {
                    if (paramPanel.Tag is not (string, int)) continue;
                    var paramBtn = paramPanel.Controls.OfType<CheckBox>().FirstOrDefault();
                    if (paramBtn != null) paramBtn.Checked = value;
                }
            }
        }

        private void buttonEnableAll_Click(object sender, EventArgs e) => SetAllDevices(true);
        private void buttonDisableAll_Click(object sender, EventArgs e) => SetAllDevices(false);
    }
}