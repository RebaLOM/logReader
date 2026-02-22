using System.Linq;
using logReader;

namespace logReader.UI
{
    public partial class Devices_ParametrsForm : Form
    {
        private readonly List<Device> _devices;
        private readonly Dictionary<string, bool> _deviceEnabled;
        private readonly Dictionary<string, bool[]> _paramEnabled;
        private bool _inUpdate;

        public Devices_ParametrsForm(List<Device> devices,
            Dictionary<string, bool> deviceEnabled,
            Dictionary<string, bool[]> paramEnabled)
        {
            InitializeComponent();
            _devices = devices;
            _deviceEnabled = deviceEnabled;
            _paramEnabled = paramEnabled;
            LoadDeviceList();
        }

        private void LoadDeviceList()
        {
            dataGridView.Rows.Clear();
            foreach (var device in _devices)
            {
                bool devOn = _deviceEnabled.GetValueOrDefault(device.ID, true);
                if (!_paramEnabled.ContainsKey(device.ID))
                    _paramEnabled[device.ID] = Enumerable.Range(0, device.headers.Length).Select(_ => true).ToArray();
                var paramArr = _paramEnabled[device.ID];

                for (int i = 0; i < device.headers.Length; i++)
                {
                    bool paramOn = i < paramArr.Length ? paramArr[i] : true;
                    dataGridView.Rows.Add(devOn, device.ID, paramOn, device.headers[i]);
                }
            }
        }

        private void dataGridView_CurrentCellDirtyStateChanged(object? sender, EventArgs e)
        {
            if (dataGridView.IsCurrentCellDirty && dataGridView.CurrentCell?.ColumnIndex is 0 or 2)
            {
                dataGridView.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        private void dataGridView_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (_inUpdate || e.RowIndex < 0) return;
            var row = dataGridView.Rows[e.RowIndex];
            if (row.Cells[1].Value is not string deviceId) return;

            if (e.ColumnIndex == 0) // Device checkbox
            {
                bool val = row.Cells[0].Value is true;
                _deviceEnabled[deviceId] = val;
                _inUpdate = true;
                try
                {
                    foreach (DataGridViewRow r in dataGridView.Rows)
                    {
                        if (r.Cells[1].Value as string == deviceId)
                            r.Cells[0].Value = val;
                    }
                }
                finally { _inUpdate = false; }
            }
            else if (e.ColumnIndex == 2) // Param checkbox
            {
                if (!_paramEnabled.ContainsKey(deviceId))
                {
                    var dev = _devices.First(d => d.ID == deviceId);
                    _paramEnabled[deviceId] = Enumerable.Range(0, dev.headers.Length).Select(_ => true).ToArray();
                }
                int paramIdx = GetParamIndexForRow(deviceId, e.RowIndex);
                if (paramIdx >= 0 && paramIdx < _paramEnabled[deviceId].Length)
                    _paramEnabled[deviceId][paramIdx] = row.Cells[2].Value is true;
            }
        }

        private int GetParamIndexForRow(string deviceId, int rowIndex)
        {
            int idx = 0;
            foreach (var device in _devices)
            {
                if (device.ID == deviceId)
                {
                    for (int i = 0; i < device.headers.Length; i++)
                    {
                        if (idx + i == rowIndex) return i;
                    }
                    return -1;
                }
                idx += device.headers.Length;
            }
            return -1;
        }

        private void SaveState()
        {
            string? lastDeviceId = null;
            int paramIdx = -1;
            foreach (DataGridViewRow row in dataGridView.Rows)
            {
                if (row.Cells[1].Value is not string deviceId) continue;

                _deviceEnabled[deviceId] = row.Cells[0].Value is true;

                if (deviceId != lastDeviceId)
                {
                    lastDeviceId = deviceId;
                    paramIdx = 0;
                }
                else
                {
                    paramIdx++;
                }

                var dev = _devices.FirstOrDefault(d => d.ID == deviceId);
                if (dev != null)
                {
                    if (!_paramEnabled.ContainsKey(deviceId))
                        _paramEnabled[deviceId] = new bool[dev.headers.Length];
                    if (paramIdx < _paramEnabled[deviceId].Length)
                        _paramEnabled[deviceId][paramIdx] = row.Cells[2].Value is true;
                }
            }
        }

        private void buttonEnableAll_Click(object sender, EventArgs e)
        {
            foreach (DataGridViewRow row in dataGridView.Rows)
            {
                row.Cells[0].Value = true;
                row.Cells[2].Value = true;
            }
            foreach (var device in _devices)
            {
                _deviceEnabled[device.ID] = true;
                _paramEnabled[device.ID] = Enumerable.Range(0, device.headers.Length).Select(_ => true).ToArray();
            }
        }

        private void buttonDisableAll_Click(object sender, EventArgs e)
        {
            foreach (DataGridViewRow row in dataGridView.Rows)
            {
                row.Cells[0].Value = false;
                row.Cells[2].Value = false;
            }
            foreach (var device in _devices)
            {
                _deviceEnabled[device.ID] = false;
                _paramEnabled[device.ID] = new bool[device.headers.Length];
            }
        }

        private void Devices_ParametrsForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            SaveState();
        }

        private void dataGridView_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {

        }
    }
}
