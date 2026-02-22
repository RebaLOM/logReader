using System.Linq;
using logReader;

namespace logReader.UI
{
    public partial class Devices_ParametrsForm : Form
    {
        private readonly List<Device> _devices;
        private readonly Dictionary<string, bool> _deviceEnabled;
        private readonly Dictionary<string, bool[]> _paramEnabled;

        private const int HEADER_H = 34;
        private const int PARAM_H = 28;
        private const int GB_MARGIN = 8;

        private Panel _innerPanel = null!;

        public Devices_ParametrsForm(List<Device> devices,
            Dictionary<string, bool> deviceEnabled,
            Dictionary<string, bool[]> paramEnabled)
        {
            InitializeComponent();
            Icon = Application.OpenForms.OfType<MainForm>().FirstOrDefault()?.Icon;
            _devices = devices;
            _deviceEnabled = deviceEnabled;
            _paramEnabled = paramEnabled;

            Shown += (_, _) => BuildDevicePanels();
        }

        private int GbWidth() =>
            scrollPanel.ClientSize.Width
            - SystemInformation.VerticalScrollBarWidth - 12;

        private int GbHeight(int paramCount) =>
            22 + HEADER_H + 2 + paramCount * PARAM_H + 10;

        private void BuildDevicePanels()
        {
            scrollPanel.Controls.Clear();

            int gbW = GbWidth();
            int yOffset = 6;
            int totalH = 6;

            foreach (var d in _devices)
                totalH += GbHeight(d.headers.Length) + GB_MARGIN;

            _innerPanel = new Panel
            {
                Top = 0,
                Left = 0,
                Width = scrollPanel.ClientSize.Width,
                Height = totalH,
            };

            foreach (var device in _devices)
            {
                if (!_deviceEnabled.ContainsKey(device.ID))
                    _deviceEnabled[device.ID] = true;
                if (!_paramEnabled.ContainsKey(device.ID))
                    _paramEnabled[device.ID] = Enumerable.Repeat(true, device.headers.Length).ToArray();

                bool devOn = _deviceEnabled[device.ID];
                bool[] paramArr = _paramEnabled[device.ID];

                var gb = CreateGroupBox(device, devOn, paramArr, gbW, yOffset);
                _innerPanel.Controls.Add(gb);

                yOffset += GbHeight(device.headers.Length) + GB_MARGIN;
            }

            scrollPanel.Controls.Add(_innerPanel);
        }

        private GroupBox CreateGroupBox(Device device, bool devOn, bool[] paramArr, int gbW, int top)
        {
            var gb = new GroupBox
            {
                Tag = device.ID,
                Left = 6,
                Top = top,
                Width = gbW,
                Height = GbHeight(device.headers.Length),
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            };

            var headerPanel = new Panel
            {
                Left = 6,
                Top = 18,
                Height = HEADER_H,
                Width = gb.ClientSize.Width - 12,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
                BackColor = Color.FromArgb(220, 228, 242)
            };

            headerPanel.Controls.Add(new Label
            {
                Text = "Посылка:  " + device.ID,
                Left = 8,
                Top = 0,
                Height = HEADER_H,
                Width = headerPanel.Width - 130,
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font(Font, FontStyle.Bold)
            });

            var btnDevice = new CheckBox
            {
                Tag = device.ID,
                Text = devOn ? "Включено" : "Выключено",
                Checked = devOn,
                Appearance = Appearance.Button,
                Left = headerPanel.Width - 120,
                Top = 5,
                Width = 116,
                Height = 24,
                Anchor = AnchorStyles.Right,
                BackColor = devOn ? Color.FromArgb(168, 214, 168) : Color.FromArgb(214, 168, 168),
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleCenter
            };
            btnDevice.FlatAppearance.BorderSize = 0;
            btnDevice.CheckedChanged += DeviceToggle_CheckedChanged;
            headerPanel.Controls.Add(btnDevice);
            gb.Controls.Add(headerPanel);

            gb.Controls.Add(new Panel
            {
                Left = 6,
                Top = 18 + HEADER_H,
                Width = gb.ClientSize.Width - 12,
                Height = 1,
                BackColor = Color.Silver,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            });

            int paramTop = 18 + HEADER_H + 2;

            for (int i = 0; i < device.headers.Length; i++)
            {
                bool paramOn = i < paramArr.Length ? paramArr[i] : true;

                var paramPanel = new Panel
                {
                    Tag = (device.ID, i),
                    Left = 6,
                    Top = paramTop + i * PARAM_H,
                    Height = PARAM_H - 1,
                    Width = gb.ClientSize.Width - 12,
                    Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
                    BackColor = i % 2 == 0 ? Color.White : Color.FromArgb(246, 246, 252)
                };

                paramPanel.Controls.Add(new Label
                {
                    Text = device.headers[i],
                    Left = 8,
                    Top = 0,
                    Height = paramPanel.Height,
                    Width = paramPanel.Width - 82,
                    Anchor = AnchorStyles.Left | AnchorStyles.Right,
                    TextAlign = ContentAlignment.MiddleLeft,
                    AutoEllipsis = true
                });

                var btnParam = new CheckBox
                {
                    Tag = (device.ID, i),
                    Text = paramOn ? "Вкл" : "Выкл",
                    Checked = paramOn,
                    Appearance = Appearance.Button,
                    Left = paramPanel.Width - 70,
                    Top = 2,
                    Width = 66,
                    Height = 22,
                    Anchor = AnchorStyles.Right,
                    BackColor = paramOn ? Color.FromArgb(200, 232, 200) : Color.FromArgb(232, 200, 200),
                    FlatStyle = FlatStyle.Flat,
                    TextAlign = ContentAlignment.MiddleCenter
                };
                btnParam.FlatAppearance.BorderSize = 0;
                btnParam.CheckedChanged += ParamToggle_CheckedChanged;

                paramPanel.Controls.Add(btnParam);
                gb.Controls.Add(paramPanel);
            }

            return gb;
        }

        // ─── Поиск ────────────────────────────────────────────────────────
        private void textBoxSearch_TextChanged(object? sender, EventArgs e)
        {
            string query = textBoxSearch.Text.Trim().ToLowerInvariant();
            ApplyFilter(query);
        }

        private void ApplyFilter(string query)
        {
            if (_innerPanel == null) return;

            _innerPanel.SuspendLayout();

            int yOffset = 6;
            int totalH = 6;

            foreach (var gb in _innerPanel.Controls.OfType<GroupBox>())
            {
                string deviceId = gb.Tag as string ?? "";
                bool visible = string.IsNullOrEmpty(query)
                               || deviceId.ToLowerInvariant().Contains(query);

                gb.Visible = visible;

                if (visible)
                {
                    gb.Top = yOffset;
                    yOffset += gb.Height + GB_MARGIN;
                    totalH += gb.Height + GB_MARGIN;
                }
            }

            _innerPanel.Height = Math.Max(totalH, scrollPanel.ClientSize.Height);
            _innerPanel.ResumeLayout();
        }

        // ─── Resize ───────────────────────────────────────────────────────
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_innerPanel == null || scrollPanel == null) return;

            int newW = scrollPanel.ClientSize.Width;
            int gbW = GbWidth();

            _innerPanel.Width = newW;

            foreach (var gb in _innerPanel.Controls.OfType<GroupBox>())
                gb.Width = gbW;
        }

        // ─── Обработчики кнопок ───────────────────────────────────────────
        private void DeviceToggle_CheckedChanged(object? sender, EventArgs e)
        {
            if (sender is not CheckBox cb || cb.Tag is not string deviceId) return;
            bool isOn = cb.Checked;
            cb.Text = isOn ? "Включено" : "Выключено";
            cb.BackColor = isOn ? Color.FromArgb(168, 214, 168) : Color.FromArgb(214, 168, 168);
            _deviceEnabled[deviceId] = isOn;
        }

        private void ParamToggle_CheckedChanged(object? sender, EventArgs e)
        {
            if (sender is not CheckBox cb || cb.Tag is not (string deviceId, int idx)) return;
            bool isOn = cb.Checked;
            cb.Text = isOn ? "Вкл" : "Выкл";
            cb.BackColor = isOn ? Color.FromArgb(200, 232, 200) : Color.FromArgb(232, 200, 200);

            if (!_paramEnabled.ContainsKey(deviceId))
            {
                var dev = _devices.First(d => d.ID == deviceId);
                _paramEnabled[deviceId] = Enumerable.Repeat(true, dev.headers.Length).ToArray();
            }
            if (idx < _paramEnabled[deviceId].Length)
                _paramEnabled[deviceId][idx] = isOn;
        }

        private void SetAll(bool value)
        {
            if (_innerPanel == null) return;

            foreach (var gb in _innerPanel.Controls.OfType<GroupBox>())
            {
                // применяем только к видимым (отфильтрованным) устройствам
                if (!gb.Visible) continue;

                foreach (var panel in gb.Controls.OfType<Panel>())
                    foreach (var cb in panel.Controls.OfType<CheckBox>())
                        cb.Checked = value;
            }
        }

        private void buttonEnableAll_Click(object sender, EventArgs e) => SetAll(true);
        private void buttonDisableAll_Click(object sender, EventArgs e) => SetAll(false);
    }
}