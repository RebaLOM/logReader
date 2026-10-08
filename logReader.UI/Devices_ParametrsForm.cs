using System.Linq;
using logReader;

namespace logReader.UI
{
    public partial class Devices_ParametrsForm : Form
    {
        private readonly List<Device> _devices;
        private readonly Dictionary<string, bool> _deviceEnabled;
        private readonly Dictionary<string, bool[]> _paramEnabled;
        private readonly List<string> _missingDevices;
        private readonly List<string> _matchedDevices;
        private readonly Dictionary<string, ModernButton> _deviceButtons = new();
        private readonly Dictionary<(string, int), ModernButton> _parameterButtons = new();
        private Panel? _innerPanel;

        private int RowHeight => Math.Max(UiScale.Px(this, 40), Font.Height + UiScale.Px(this, 16));
        private int HeaderHeight => UiScale.Px(this, 64);
        private int CardPadding => UiScale.Px(this, 16);
        private int CardWidth => Math.Max(UiScale.Px(this, 280),
            scrollPanel.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - UiScale.Px(this, 32));

        public Devices_ParametrsForm(List<Device> devices,
            Dictionary<string, bool> deviceEnabled,
            Dictionary<string, bool[]> paramEnabled,
            List<string>? missingDevices = null,
            List<string>? matchedDevices = null)
        {
            InitializeComponent();
            AppTheme.Apply(this);
            _devices = devices;
            _deviceEnabled = deviceEnabled;
            _paramEnabled = paramEnabled;
            _missingDevices = missingDevices ?? new();
            _matchedDevices = matchedDevices ?? new();
            Icon = Application.OpenForms.OfType<MainForm>().FirstOrDefault()?.Icon;
            RefreshLogDeviceLists("");
            Shown += (_, _) => BuildDevicePanels();
        }

        private void EnsureSelections(Device device)
        {
            _deviceEnabled.TryAdd(device.ID, true);
            if (!_paramEnabled.TryGetValue(device.ID, out var previous))
                _paramEnabled[device.ID] = Enumerable.Repeat(true, device.headers.Length).ToArray();
            else if (previous.Length != device.headers.Length)
            {
                var resized = Enumerable.Repeat(true, device.headers.Length).ToArray();
                Array.Copy(previous, resized, Math.Min(previous.Length, resized.Length));
                _paramEnabled[device.ID] = resized;
            }
        }

        private void BuildDevicePanels()
        {
            Point scroll = scrollPanel.AutoScrollPosition;
            scrollPanel.SuspendLayout();
            if (_innerPanel != null)
            {
                scrollPanel.Controls.Remove(_innerPanel);
                _innerPanel.Dispose();
            }
            _deviceButtons.Clear();
            _parameterButtons.Clear();
            _innerPanel = new Panel
            {
                Left = UiScale.Px(this, 8), Top = UiScale.Px(this, 8),
                Width = CardWidth, BackColor = AppTheme.Background
            };
            foreach (var device in _devices)
            {
                EnsureSelections(device);
                _innerPanel.Controls.Add(CreateDeviceCard(device));
            }
            scrollPanel.Controls.Add(_innerPanel);
            ApplyFilter(textBoxSearch.Text.Trim());
            scrollPanel.AutoScrollPosition = new Point(-scroll.X, -scroll.Y);
            scrollPanel.ResumeLayout();
            UpdateSelectionCount();
        }

        private ModernCard CreateDeviceCard(Device device)
        {
            int pad = CardPadding;
            var card = new ModernCard
            {
                Tag = device.ID, Width = CardWidth,
                Height = pad * 2 + HeaderHeight + device.headers.Length * RowHeight,
                Padding = Padding.Empty, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            int toggleWidth = UiScale.Px(this, 132);
            var header = new Panel
            {
                Left = pad, Top = pad, Width = card.Width - pad * 2, Height = HeaderHeight,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = AppTheme.Surface
            };
            var title = new Label
            {
                Text = string.IsNullOrWhiteSpace(device.Name) || device.Name == "Unknown"
                    ? "Устройство " + device.ID : device.Name + " · " + device.ID,
                Left = 0, Top = 0, Width = header.Width - toggleWidth - pad,
                Height = UiScale.Px(this, 28), Font = Typography.CardTitle,
                ForeColor = AppTheme.TextPrimary, AutoEllipsis = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                TextAlign = ContentAlignment.MiddleLeft
            };
            header.Controls.Add(title);
            header.Controls.Add(new Label
            {
                Text = "CAN ID " + device.ID + "  ·  Параметров: " + device.headers.Length,
                Top = UiScale.Px(this, 30), Left = 0, Height = UiScale.Px(this, 22),
                Width = header.Width - toggleWidth - pad, Font = Typography.Secondary,
                ForeColor = AppTheme.TextSecondary, AutoEllipsis = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            });
            var deviceButton = new ModernButton
            {
                Tag = device.ID, Left = header.Width - toggleWidth,
                Top = UiScale.Px(this, 7), Width = toggleWidth, Height = UiScale.Px(this, 36),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                AccessibleName = "Устройство " + device.ID
            };
            SetToggleVisual(deviceButton, _deviceEnabled[device.ID], false);
            deviceButton.Click += DeviceBtn_Click;
            header.Controls.Add(deviceButton);
            _deviceButtons[device.ID] = deviceButton;
            card.Controls.Add(header);

            for (int i = 0; i < device.headers.Length; i++)
            {
                var row = new Panel
                {
                    Tag = (device.ID, i), Left = pad, Top = pad + HeaderHeight + i * RowHeight,
                    Width = card.Width - pad * 2, Height = RowHeight,
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    BackColor = i % 2 == 0 ? AppTheme.Surface : AppTheme.SurfaceSecondary
                };
                row.Controls.Add(new Label
                {
                    Text = device.headers[i], Left = UiScale.Px(this, 12), Top = 0,
                    Width = row.Width - toggleWidth - UiScale.Px(this, 24), Height = RowHeight,
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true,
                    Font = Typography.Body, ForeColor = AppTheme.TextPrimary
                });
                var button = new ModernButton
                {
                    Tag = (device.ID, i), Left = row.Width - toggleWidth,
                    Top = UiScale.Px(this, 4), Width = toggleWidth, Height = RowHeight - UiScale.Px(this, 8),
                    Anchor = AnchorStyles.Top | AnchorStyles.Right,
                    AccessibleName = "Параметр " + device.headers[i] + ", устройство " + device.ID
                };
                SetToggleVisual(button, _paramEnabled[device.ID][i], true);
                button.Click += ParamBtn_Click;
                _parameterButtons[(device.ID, i)] = button;
                row.Controls.Add(button);
                card.Controls.Add(row);
            }
            return card;
        }

        private static void SetToggleVisual(ModernButton button, bool on, bool parameter)
        {
            button.Variant = on ? ButtonVariant.Primary : ButtonVariant.Ghost;
            button.Icon = on ? IconKind.Check : IconKind.None;
            button.Text = parameter ? (on ? "Включён" : "Выключен") : (on ? "Включено" : "Выключено");
            button.AccessibleDescription = on ? "Включено в обработку. Нажмите, чтобы выключить."
                : "Исключено из обработки. Нажмите, чтобы включить.";
        }

        private void textBoxSearch_TextChanged(object? sender, EventArgs e)
            => ApplyFilter(textBoxSearch.Text.Trim());

        private void textBoxSearchUnknown_TextChanged(object? sender, EventArgs e)
            => RefreshLogDeviceLists(textBoxSearchUnknown.Text.Trim());

        private void RefreshLogDeviceLists(string query)
        {
            var missing = _missingDevices.Where(id => id.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
            var matched = _matchedDevices.Where(id => id.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
            listBoxMissing.BeginUpdate();
            listBoxMatched.BeginUpdate();
            listBoxMissing.Items.Clear();
            listBoxMatched.Items.Clear();
            listBoxMissing.Items.AddRange(missing);
            listBoxMatched.Items.AddRange(matched);
            listBoxMissing.EndUpdate();
            listBoxMatched.EndUpdate();
            labelMissingTitle.Text = $"Нет в файле посылок · {missing.Length}";
            labelMatchedTitle.Text = $"Совпадают · {matched.Length}";
            listBoxMissing.Visible = missing.Length > 0;
            listBoxMatched.Visible = matched.Length > 0;
            emptyMissing.Visible = missing.Length == 0;
            emptyMatched.Visible = matched.Length == 0;
            emptyMissing.Title = _missingDevices.Count == 0 ? "Нет отсутствующих устройств" : "Нет совпадений";
            emptyMissing.Description = _missingDevices.Count == 0
                ? "Все найденные ID описаны или лог ещё не выбран." : "Попробуйте другой ID или очистите поиск.";
            emptyMatched.Title = _matchedDevices.Count == 0 ? "Нет совпадающих устройств" : "Нет совпадений";
            emptyMatched.Description = _matchedDevices.Count == 0
                ? "Совпадений с файлом посылок нет или лог ещё не выбран." : "Попробуйте другой ID или очистите поиск.";
        }

        private void ApplyFilter(string query)
        {
            if (_innerPanel == null) return;
            _innerPanel.SuspendLayout();
            int y = 0;
            int visibleCount = 0;
            foreach (var card in _innerPanel.Controls.OfType<ModernCard>())
            {
                bool visible = (card.Tag as string ?? "").Contains(query, StringComparison.OrdinalIgnoreCase);
                card.Visible = visible;
                if (!visible) continue;
                card.Top = y;
                y += card.Height + UiScale.Px(this, 12);
                visibleCount++;
            }
            _innerPanel.Height = y;
            _innerPanel.ResumeLayout();
            emptyDevices.Visible = visibleCount == 0;
            emptyDevices.Title = _devices.Count == 0 ? "Нет устройств" : "Нет совпадений";
            emptyDevices.Description = _devices.Count == 0
                ? "Загрузите файл посылок, чтобы выбрать устройства и параметры."
                : "Попробуйте другой CAN ID или очистите поиск.";
            if (emptyDevices.Visible) emptyDevices.BringToFront();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_innerPanel == null || scrollPanel == null) return;
            _innerPanel.Width = CardWidth;
            foreach (var card in _innerPanel.Controls.OfType<ModernCard>()) card.Width = CardWidth;
        }

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);
            if (_innerPanel != null) BuildDevicePanels();
        }

        private void DeviceBtn_Click(object? sender, EventArgs e)
        {
            if (sender is not ModernButton button || button.Tag is not string id) return;
            bool on = !_deviceEnabled.GetValueOrDefault(id, true);
            _deviceEnabled[id] = on;
            SetToggleVisual(button, on, false);
            UpdateSelectionCount();
        }

        private void ParamBtn_Click(object? sender, EventArgs e)
        {
            if (sender is not ModernButton button || button.Tag is not (string id, int index)) return;
            var device = _devices.First(d => d.ID == id);
            EnsureSelections(device);
            if (index < 0 || index >= device.headers.Length) return;
            bool on = !_paramEnabled[id][index];
            _paramEnabled[id][index] = on;
            SetToggleVisual(button, on, true);
            UpdateSelectionCount();
        }

        private void SetAll(bool value)
        {
            // Bulk actions apply to every device, including those hidden by search.
            foreach (var device in _devices)
            {
                EnsureSelections(device);
                _deviceEnabled[device.ID] = value;
                Array.Fill(_paramEnabled[device.ID], value);
            }
            foreach (var button in _deviceButtons.Values) SetToggleVisual(button, value, false);
            foreach (var button in _parameterButtons.Values) SetToggleVisual(button, value, true);
            UpdateSelectionCount();
        }

        private void UpdateSelectionCount()
        {
            int enabledDevices = _devices.Count(d => _deviceEnabled.GetValueOrDefault(d.ID, true));
            int totalParameters = _devices.Sum(d => d.headers.Length);
            int enabledParameters = _devices.Where(d => _deviceEnabled.GetValueOrDefault(d.ID, true))
                .Sum(d => _paramEnabled.TryGetValue(d.ID, out var values) ? values.Count(v => v) : d.headers.Length);
            labelSelectionCount.Text = $"Устройства {enabledDevices}/{_devices.Count}  ·  Параметры {enabledParameters}/{totalParameters}";
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.F))
            {
                var search = tabControlMain.SelectedTab == tabUnknown ? textBoxSearchUnknown : textBoxSearch;
                search.Focus();
                search.SelectAll();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void buttonEnableAll_Click(object sender, EventArgs e) => SetAll(true);
        private void buttonDisableAll_Click(object sender, EventArgs e) => SetAll(false);
    }
}
