using System.Linq;
using logReader;

namespace logReader.UI
{
    // A single tree keeps large descriptions responsive. Edits are committed only by OK.
    public partial class Devices_ParametrsForm : Form
    {
        private readonly List<Device> _devices;
        private readonly Dictionary<string, bool> _targetDeviceEnabled;
        private readonly Dictionary<string, bool[]> _targetParamEnabled;
        private readonly Dictionary<string, bool> _deviceEnabled = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, bool[]> _paramEnabled = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _missingDevices;
        private readonly List<string> _matchedDevices;
        private readonly SingleClickCheckTreeView _tree = new();
        private bool _updatingChecks;

        public Devices_ParametrsForm(List<Device> devices,
            Dictionary<string, bool> deviceEnabled,
            Dictionary<string, bool[]> paramEnabled,
            List<string>? missingDevices = null,
            List<string>? matchedDevices = null)
        {
            InitializeComponent();
            _devices = devices;
            _targetDeviceEnabled = deviceEnabled;
            _targetParamEnabled = paramEnabled;
            _missingDevices = missingDevices ?? new();
            _matchedDevices = matchedDevices ?? new();
            foreach (var device in _devices)
            {
                _deviceEnabled[device.ID] = deviceEnabled.GetValueOrDefault(device.ID, true);
                var previous = paramEnabled.GetValueOrDefault(device.ID);
                var selected = Enumerable.Repeat(true, device.Headers.Length).ToArray();
                if (previous != null)
                    Array.Copy(previous, selected, Math.Min(previous.Length, selected.Length));
                _paramEnabled[device.ID] = selected;
            }

            _tree.Dock = DockStyle.Fill;
            _tree.CheckBoxes = true;
            _tree.HideSelection = false;
            _tree.ShowLines = false;
            _tree.ShowRootLines = false;
            _tree.ShowPlusMinus = true;
            _tree.BorderStyle = BorderStyle.None;
            _tree.Font = Typography.Body;
            _tree.ForeColor = AppTheme.TextPrimary;
            _tree.BackColor = AppTheme.Surface;
            _tree.ItemHeight = UiScale.Px(this, 32);
            _tree.Indent = UiScale.Px(this, 24);
            _tree.AccessibleName = "Выбор устройств и параметров";
            _tree.AfterCheck += Tree_AfterCheck;
            scrollPanel.AutoScroll = false;
            scrollPanel.Controls.Add(_tree);
            textBoxSearch.PlaceholderText = "Введите CAN ID или имя параметра";
            AppTheme.Apply(this);
            Icon = Application.OpenForms.OfType<MainForm>().FirstOrDefault()?.Icon;
            RefreshLogDeviceLists("");
            BuildTree("");
            Shown += (_, _) => ScaleTree();
        }

        private void ApplyToTarget()
        {
            foreach (var selection in _deviceEnabled)
                _targetDeviceEnabled[selection.Key] = selection.Value;
            foreach (var selection in _paramEnabled)
                _targetParamEnabled[selection.Key] = (bool[])selection.Value.Clone();
        }

        private void BuildTree(string query)
        {
            _updatingChecks = true;
            _tree.BeginUpdate();
            try
            {
                _tree.Nodes.Clear();
                foreach (var device in _devices)
                {
                    bool idMatches = query.Length == 0 || device.ID.Contains(query, StringComparison.OrdinalIgnoreCase);
                    var parameterIndexes = Enumerable.Range(0, device.Headers.Length)
                        .Where(i => idMatches || device.Headers[i].Contains(query, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    if (!idMatches && parameterIndexes.Count == 0) continue;

                    var node = new TreeNode(DeviceText(device)) { Tag = device, Checked = _deviceEnabled[device.ID] };
                    foreach (int index in parameterIndexes)
                        node.Nodes.Add(new TreeNode(device.Headers[index])
                        {
                            Tag = (device, index), Checked = _paramEnabled[device.ID][index]
                        });
                    _tree.Nodes.Add(node);
                    if (query.Length > 0) node.Expand();
                }
            }
            finally
            {
                _tree.EndUpdate();
                _updatingChecks = false;
            }
            bool empty = _tree.Nodes.Count == 0;
            _tree.Visible = !empty;
            emptyDevices.Visible = empty;
            emptyDevices.Title = _devices.Count == 0 ? "Нет устройств" : "Нет совпадений";
            emptyDevices.Description = _devices.Count == 0
                ? "Загрузите файл посылок, чтобы выбрать устройства и параметры."
                : "Попробуйте другой CAN ID, имя параметра или очистите поиск.";
            UpdateSelectionCount();
        }

        private string DeviceText(Device device)
            => $"CAN ID {device.ID}  ·  {_paramEnabled[device.ID].Count(selected => selected)}/{device.Headers.Length} параметров";

        private void Tree_AfterCheck(object? sender, TreeViewEventArgs e)
        {
            if (_updatingChecks || e.Node == null) return;
            if (e.Node.Tag is Device device)
                _deviceEnabled[device.ID] = e.Node.Checked;
            else if (e.Node.Tag is ValueTuple<Device, int> parameter)
            {
                _paramEnabled[parameter.Item1.ID][parameter.Item2] = e.Node.Checked;
                if (e.Node.Parent != null)
                    e.Node.Parent.Text = DeviceText(parameter.Item1);
            }
            UpdateSelectionCount();
        }

        private void textBoxSearch_TextChanged(object? sender, EventArgs e)
            => BuildTree(textBoxSearch.Text.Trim());

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

        private void SetAll(bool value)
        {
            // Bulk actions include devices and parameters hidden by the current search.
            foreach (var device in _devices)
            {
                _deviceEnabled[device.ID] = value;
                Array.Fill(_paramEnabled[device.ID], value);
            }
            BuildTree(textBoxSearch.Text.Trim());
        }

        private void UpdateSelectionCount()
        {
            int enabledDevices = _devices.Count(d => _deviceEnabled[d.ID]);
            int totalParameters = _devices.Sum(d => d.Headers.Length);
            int enabledParameters = _devices.Where(d => _deviceEnabled[d.ID])
                .Sum(d => _paramEnabled[d.ID].Count(selected => selected));
            labelSelectionCount.Text = $"Устройства {enabledDevices}/{_devices.Count}  ·  Параметры {enabledParameters}/{totalParameters}";
        }

        private void ScaleTree()
        {
            _tree.ItemHeight = UiScale.Px(this, 32);
            _tree.Indent = UiScale.Px(this, 24);
        }

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);
            ScaleTree();
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

        // Suppress the native double-click checkbox visual change without a second AfterCheck.
        private sealed class SingleClickCheckTreeView : TreeView
        {
            private const int WM_LBUTTONDBLCLK = 0x0203;

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_LBUTTONDBLCLK)
                {
                    m.Result = IntPtr.Zero;
                    return;
                }
                base.WndProc(ref m);
            }
        }
    }
}
