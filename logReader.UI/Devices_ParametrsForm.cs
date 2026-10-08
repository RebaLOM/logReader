using System.Linq;
using logReader.UI.Theme;

namespace logReader.UI
{
    // Фильтры устройств и параметров: дерево с флажками (один оконный контрол вместо трёх на параметр).
    // Правки делаются на копии и применяются только по «OK».
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
            panelSearch.Tag = ThemeTags.Elevated;
            panelSearchUnknown.Tag = ThemeTags.Elevated;
            panelButtons.Tag = ThemeTags.Elevated;
            labelMissingTitle.Font = Typography.Section();
            labelMatchedTitle.Font = Typography.Section();
            ThemeForm.Wire(this);
            _devices = devices;
            _targetDeviceEnabled = deviceEnabled;
            _targetParamEnabled = paramEnabled;
            _missingDevices = missingDevices ?? new List<string>();
            _matchedDevices = matchedDevices ?? new List<string>();

            foreach (var device in _devices)
            {
                _deviceEnabled[device.ID] = deviceEnabled.GetValueOrDefault(device.ID, true);
                var source = paramEnabled.GetValueOrDefault(device.ID);
                var arr = new bool[device.Headers.Length];
                for (int i = 0; i < arr.Length; i++)
                    arr[i] = source == null || i >= source.Length || source[i];
                _paramEnabled[device.ID] = arr;
            }

            Icon = Application.OpenForms.OfType<MainForm>().FirstOrDefault()?.Icon;

            _tree.Dock = DockStyle.Fill;
            _tree.CheckBoxes = true;
            _tree.HideSelection = false;
            _tree.AfterCheck += Tree_AfterCheck;
            scrollPanel.AutoScroll = false;
            scrollPanel.Controls.Add(_tree);
            textBoxSearch.PlaceholderText = "Поиск по ID устройства или имени параметра...";

            AddDialogButtons();
            RefreshLogDeviceLists("");
            BuildTree("");
        }

        private void AddDialogButtons()
        {
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Size = new Size(90, 28), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            var cancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, Size = new Size(90, 28), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            cancel.Location = new Point(panelButtons.ClientSize.Width - cancel.Width - 12, 10);
            ok.Location = new Point(cancel.Left - ok.Width - 8, 10);
            ok.Click += (_, _) => ApplyToTarget();
            panelButtons.Controls.Add(ok);
            panelButtons.Controls.Add(cancel);
            AcceptButton = ok;
            CancelButton = cancel;
        }

        private void ApplyToTarget()
        {
            foreach (var kv in _deviceEnabled)
                _targetDeviceEnabled[kv.Key] = kv.Value;
            foreach (var kv in _paramEnabled)
                _targetParamEnabled[kv.Key] = (bool[])kv.Value.Clone();
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
                    var paramIndexes = Enumerable.Range(0, device.Headers.Length)
                        .Where(i => idMatches || device.Headers[i].Contains(query, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    if (!idMatches && paramIndexes.Count == 0) continue;

                    var deviceNode = new TreeNode(DeviceText(device)) { Tag = device, Checked = _deviceEnabled[device.ID] };
                    foreach (int i in paramIndexes)
                        deviceNode.Nodes.Add(new TreeNode(device.Headers[i]) { Tag = (device, i), Checked = _paramEnabled[device.ID][i] });
                    _tree.Nodes.Add(deviceNode);
                    if (query.Length > 0) deviceNode.Expand();
                }
            }
            finally
            {
                _tree.EndUpdate();
                _updatingChecks = false;
            }
        }

        private string DeviceText(Device device)
        {
            int enabled = _paramEnabled[device.ID].Count(v => v);
            return $"{device.ID}   ({enabled}/{device.Headers.Length})";
        }

        private void Tree_AfterCheck(object? sender, TreeViewEventArgs e)
        {
            if (_updatingChecks || e.Node == null) return;

            if (e.Node.Tag is Device device)
            {
                _deviceEnabled[device.ID] = e.Node.Checked;
            }
            else if (e.Node.Tag is ValueTuple<Device, int> param)
            {
                _paramEnabled[param.Item1.ID][param.Item2] = e.Node.Checked;
                if (e.Node.Parent != null)
                    e.Node.Parent.Text = DeviceText(param.Item1);
            }
        }

        private void textBoxSearch_TextChanged(object? sender, EventArgs e)
            => BuildTree(textBoxSearch.Text.Trim());

        private void textBoxSearchUnknown_TextChanged(object? sender, EventArgs e)
            => RefreshLogDeviceLists(textBoxSearchUnknown.Text.Trim());

        private void RefreshLogDeviceLists(string query)
        {
            FillList(listBoxMissing, _missingDevices, query, "Нет отсутствующих устройств");
            FillList(listBoxMatched, _matchedDevices, query, "Нет совпадающих устройств");
        }

        private static void FillList(ListBox list, List<string> ids, string query, string emptyText)
        {
            list.BeginUpdate();
            list.Items.Clear();
            if (ids.Count == 0)
            {
                list.Items.Add(emptyText);
                list.Items.Add("или лог не выбран.");
            }
            else
            {
                foreach (string id in ids)
                    if (query.Length == 0 || id.Contains(query, StringComparison.OrdinalIgnoreCase))
                        list.Items.Add(id);
                if (list.Items.Count == 0)
                    list.Items.Add("Нет совпадений по поиску.");
            }
            list.EndUpdate();
        }

        private void SetAll(bool value)
        {
            // «Включить/выключить всё» — по всей модели, не только по видимым после поиска.
            foreach (var dev in _devices)
            {
                _deviceEnabled[dev.ID] = value;
                Array.Fill(_paramEnabled[dev.ID], value);
            }
            BuildTree(textBoxSearch.Text.Trim());
        }

        private void buttonEnableAll_Click(object sender, EventArgs e) => SetAll(true);
        private void buttonDisableAll_Click(object sender, EventArgs e) => SetAll(false);

        // Двойной щелчок по флажку TreeView меняет его вид без второго AfterCheck — состояние
        // расходится с моделью. Второй щелчок обрабатывается как обычный одиночный.
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
