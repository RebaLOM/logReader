namespace logReader
{
    // Неизменяемый снимок фильтров «устройство / параметр» на момент запуска обработки:
    // фоновая обработка не должна видеть правки, сделанные в окне фильтров параллельно.
    public sealed class OutputFilter
    {
        private readonly Dictionary<string, bool> _devices;
        private readonly Dictionary<string, bool[]> _params;

        public static OutputFilter All { get; } = new(new(), new());

        private OutputFilter(Dictionary<string, bool> devices, Dictionary<string, bool[]> parameters)
        {
            _devices = devices;
            _params = parameters;
        }

        public static OutputFilter From(
            IReadOnlyDictionary<string, bool>? deviceEnabled,
            IReadOnlyDictionary<string, bool[]>? paramEnabled)
        {
            var devices = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            if (deviceEnabled != null)
                foreach (var kv in deviceEnabled)
                    devices[kv.Key] = kv.Value;

            var parameters = new Dictionary<string, bool[]>(StringComparer.OrdinalIgnoreCase);
            if (paramEnabled != null)
                foreach (var kv in paramEnabled)
                    parameters[kv.Key] = (bool[])kv.Value.Clone();

            return new OutputFilter(devices, parameters);
        }

        public bool HasRestrictions => _devices.Values.Any(v => !v) || _params.Values.Any(a => a.Any(v => !v));

        public bool IsDeviceEnabled(string deviceId)
            => !_devices.TryGetValue(deviceId, out bool on) || on;

        // Параметры, добавленные после настройки фильтра (индекс за пределами массива), включены.
        public bool IsParamEnabled(string deviceId, int index)
            => !_params.TryGetValue(deviceId, out var arr) || index >= arr.Length || arr[index];

        public int[] GetActiveParams(Device device)
        {
            if (!IsDeviceEnabled(device.ID)) return Array.Empty<int>();
            var result = new List<int>(device.Headers.Length);
            for (int i = 0; i < device.Headers.Length; i++)
                if (IsParamEnabled(device.ID, i))
                    result.Add(i);
            return result.ToArray();
        }

        // Единый план колонок для заголовков и данных всех форматов вывода:
        // устройство без активных параметров не выводится вовсе.
        public List<OutputColumnGroup> BuildColumns(IEnumerable<Device> devices)
        {
            var groups = new List<OutputColumnGroup>();
            foreach (var device in devices)
            {
                int[] active = GetActiveParams(device);
                if (active.Length > 0)
                    groups.Add(new OutputColumnGroup(device, active));
            }
            return groups;
        }
    }

    public sealed record OutputColumnGroup(Device Device, int[] ParamIndexes)
    {
        public IEnumerable<string> Headers => ParamIndexes.Select(i => Device.Headers[i]);
    }
}
