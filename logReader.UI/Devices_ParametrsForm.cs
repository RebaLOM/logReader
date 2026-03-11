using System.Linq;
using logReader;

namespace logReader.UI
{
    public partial class Devices_ParametrsForm : Form
    {
        private readonly List<Device> _devices;
        private readonly Dictionary<string, bool> _deviceEnabled;
        private readonly Dictionary<string, bool[]> _paramEnabled;

        private const int GB_MARGIN = 8;

        // Все размеры вычисляются от шрифта — корректно работают при любом DPI/масштабе
        private int HeaderH => Font.Height + 18;
        private int ParamH => Font.Height + 12;
        private int BtnH => Font.Height + 6;
        private int GbTitleH => Font.Height + 8;

        // Ширина кнопок измеряется по самому длинному тексту + отступы
        // TextRenderer.MeasureText учитывает текущий DPI и шрифт
        private int DeviceBtnW => TextRenderer.MeasureText("Выключено", Font).Width + 20;
        private int ParamBtnW => TextRenderer.MeasureText("Выкл", Font).Width + 20;

        private Panel _innerPanel = null!;

        public Devices_ParametrsForm(List<Device> devices,
            Dictionary<string, bool> deviceEnabled,
            Dictionary<string, bool[]> paramEnabled)
        {
            InitializeComponent();
            _devices = devices;
            _deviceEnabled = deviceEnabled;
            _paramEnabled = paramEnabled;

            Icon = Application.OpenForms.OfType<MainForm>().FirstOrDefault()?.Icon;
            Shown += (_, _) => BuildDevicePanels();
        }

        private int GbWidth() =>
            scrollPanel.ClientSize.Width
            - SystemInformation.VerticalScrollBarWidth - 12;

        private int GbHeight(int paramCount) =>
            GbTitleH + HeaderH + 2 + paramCount * ParamH + GbTitleH;

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

                if (!_paramEnabled.TryGetValue(device.ID, out var paramArr))
                {
                    paramArr = Enumerable.Repeat(true, device.headers.Length).ToArray();
                    _paramEnabled[device.ID] = paramArr;
                }
                else if (paramArr.Length != device.headers.Length)
                {
                    var resized = new bool[device.headers.Length];
                    int copyLen = Math.Min(paramArr.Length, resized.Length);
                    Array.Copy(paramArr, resized, copyLen);
                    for (int i = copyLen; i < resized.Length; i++)
                        resized[i] = true;
                    paramArr = resized;
                    _paramEnabled[device.ID] = resized;
                }

                bool devOn = _deviceEnabled[device.ID];

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
                Height = HeaderH,
                Width = gb.ClientSize.Width - 12,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
                BackColor = Color.FromArgb(220, 228, 242)
            };

            headerPanel.Controls.Add(new Label
            {
                Text = "Устройство:  " + device.ID,
                Left = 8,
                Top = 0,
                Height = HeaderH,
                Width = headerPanel.Width - DeviceBtnW - 16,
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font(Font, FontStyle.Bold)
            });

            int devW = DeviceBtnW;
            var btnDevice = new Button
            {
                Tag = device.ID,
                Text = devOn ? "Включено" : "Выключено",
                Left = headerPanel.Width - devW - 4,
                Top = (HeaderH - BtnH) / 2,
                Width = devW,
                Height = BtnH,
                Anchor = AnchorStyles.Right,
                BackColor = devOn ? Color.FromArgb(168, 214, 168) : Color.FromArgb(214, 168, 168),
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false
            };
            btnDevice.FlatAppearance.BorderSize = 0;
            btnDevice.Click += DeviceBtn_Click;
            headerPanel.Controls.Add(btnDevice);
            gb.Controls.Add(headerPanel);

            gb.Controls.Add(new Panel
            {
                Left = 6,
                Top = 18 + HeaderH,
                Width = gb.ClientSize.Width - 12,
                Height = 1,
                BackColor = Color.Silver,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            });

            int paramTop = 18 + HeaderH + 2;

            for (int i = 0; i < device.headers.Length; i++)
            {
                bool paramOn = i < paramArr.Length ? paramArr[i] : true;

                var paramPanel = new Panel
                {
                    Tag = (device.ID, i),
                    Left = 6,
                    Top = paramTop + i * ParamH,
                    Height = ParamH,
                    Width = gb.ClientSize.Width - 12,
                    Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
                    BackColor = i % 2 == 0 ? Color.White : Color.FromArgb(246, 246, 252)
                };

                paramPanel.Controls.Add(new Label
                {
                    Text = device.headers[i],
                    Left = 8,
                    Top = 0,
                    Height = ParamH,
                    Width = paramPanel.Width - ParamBtnW - 16,
                    Anchor = AnchorStyles.Left | AnchorStyles.Right,
                    TextAlign = ContentAlignment.MiddleLeft,
                    AutoEllipsis = true
                });

                int prmW = ParamBtnW;
                int btnTop = (ParamH - BtnH) / 2;
                var btnParam = new Button
                {
                    Tag = (device.ID, i),
                    Text = paramOn ? "Вкл" : "Выкл",
                    Left = paramPanel.Width - prmW - 4,
                    Top = btnTop,
                    Width = prmW,
                    Height = BtnH,
                    Anchor = AnchorStyles.Right,
                    BackColor = paramOn ? Color.FromArgb(200, 232, 200) : Color.FromArgb(232, 200, 200),
                    FlatStyle = FlatStyle.Flat,
                    UseVisualStyleBackColor = false
                };
                btnParam.FlatAppearance.BorderSize = 0;
                btnParam.Click += ParamBtn_Click;

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
        private void DeviceBtn_Click(object? sender, EventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string deviceId) return;
            // Инвертируем текущее состояние
            bool isOn = !_deviceEnabled.GetValueOrDefault(deviceId, true);
            btn.Text = isOn ? "Включено" : "Выключено";
            btn.BackColor = isOn ? Color.FromArgb(168, 214, 168) : Color.FromArgb(214, 168, 168);
            _deviceEnabled[deviceId] = isOn;
        }

        private void ParamBtn_Click(object? sender, EventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not (string deviceId, int idx)) return;

            if (!_paramEnabled.TryGetValue(deviceId, out var arr))
            {
                var dev = _devices.First(d => d.ID == deviceId);
                arr = Enumerable.Repeat(true, dev.headers.Length).ToArray();
                _paramEnabled[deviceId] = arr;
            }
            else if (idx >= arr.Length)
            {
                var dev = _devices.First(d => d.ID == deviceId);
                if (idx >= dev.headers.Length) return;

                var resized = new bool[dev.headers.Length];
                int copyLen = Math.Min(arr.Length, resized.Length);
                Array.Copy(arr, resized, copyLen);
                for (int i = copyLen; i < resized.Length; i++)
                    resized[i] = true;
                arr = resized;
                _paramEnabled[deviceId] = arr;
            }

            bool isOn = !arr[idx];
            btn.Text = isOn ? "Вкл" : "Выкл";
            btn.BackColor = isOn ? Color.FromArgb(200, 232, 200) : Color.FromArgb(232, 200, 200);

            arr[idx] = isOn;
        }

        private void SetAll(bool value)
        {
            if (_innerPanel == null) return;

            foreach (var gb in _innerPanel.Controls.OfType<GroupBox>())
            {
                if (!gb.Visible) continue;

                foreach (var panel in gb.Controls.OfType<Panel>())
                {
                    foreach (var btn in panel.Controls.OfType<Button>())
                    {
                        // Определяем тип кнопки по тегу и обновляем только если состояние отличается
                        if (btn.Tag is string deviceId)
                        {
                            bool current = _deviceEnabled.GetValueOrDefault(deviceId, true);
                            if (current != value)
                            {
                                btn.Text = value ? "Включено" : "Выключено";
                                btn.BackColor = value ? Color.FromArgb(168, 214, 168) : Color.FromArgb(214, 168, 168);
                                _deviceEnabled[deviceId] = value;
                            }
                        }
                        else if (btn.Tag is (string dId, int idx))
                        {
                            if (!_paramEnabled.ContainsKey(dId))
                            {
                                var dev = _devices.First(d => d.ID == dId);
                                _paramEnabled[dId] = Enumerable.Repeat(true, dev.headers.Length).ToArray();
                            }
                            bool current = _paramEnabled[dId][idx];
                            if (current != value)
                            {
                                btn.Text = value ? "Вкл" : "Выкл";
                                btn.BackColor = value ? Color.FromArgb(200, 232, 200) : Color.FromArgb(232, 200, 200);
                                _paramEnabled[dId][idx] = value;
                            }
                        }
                    }
                }
            }
        }

        private void buttonEnableAll_Click(object sender, EventArgs e) => SetAll(true);
        private void buttonDisableAll_Click(object sender, EventArgs e) => SetAll(false);
    }
}
