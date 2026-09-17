using logReader;

namespace logReader.UI
{
    internal static class CanPayloadGridFactory
    {
        public static List<SignalOverlay> FromDbcSignals(
            IEnumerable<DbcSignal> signals,
            string? currentName = null,
            IReadOnlyDictionary<string, Color>? colorMap = null)
        {
            var list = signals as IList<DbcSignal> ?? signals.ToList();
            colorMap ??= CanPayloadGridPalette.AssignColors(list.Select(s => s.Name));

            var overlays = new List<SignalOverlay>();
            foreach (var s in list)
            {
                bool isCurrent = currentName != null
                    && s.Name.Equals(currentName, StringComparison.OrdinalIgnoreCase);
                overlays.Add(new SignalOverlay(
                    s.Name,
                    s.StartBit,
                    s.Length,
                    s.IsLittleEndian,
                    ResolveColor(s.Name, colorMap),
                    isCurrent));
            }
            return overlays;
        }

        public static List<SignalOverlay> FromDeviceRows(
            IEnumerable<DeviceFieldRow> rows,
            string? currentHeader = null,
            IReadOnlyDictionary<string, Color>? colorMap = null)
        {
            var list = rows as IList<DeviceFieldRow> ?? rows.ToList();
            colorMap ??= CanPayloadGridPalette.AssignColors(list.Select(r => r.Header ?? ""));

            var overlays = new List<SignalOverlay>();
            foreach (var r in list)
            {
                string name = r.Header ?? "";
                bool isCurrent = currentHeader != null
                    && name.Equals(currentHeader, StringComparison.OrdinalIgnoreCase);

                if (string.Equals(r.Type, "BIN", StringComparison.OrdinalIgnoreCase))
                {
                    int bitStart = r.BitStart ?? 0;
                    int global = BitMath.CellToGlobalBit(r.StartBit, bitStart);
                    overlays.Add(new SignalOverlay(
                        name,
                        global,
                        r.Length,
                        IsLittleEndian: true,
                        ResolveColor(name, colorMap),
                        isCurrent));
                }
                else
                {
                    overlays.Add(new SignalOverlay(
                        name,
                        r.StartBit,
                        r.Length,
                        r.IsLittleEndian,
                        ResolveColor(name, colorMap),
                        isCurrent));
                }
            }
            return overlays;
        }

        private static Color ResolveColor(string name, IReadOnlyDictionary<string, Color> colorMap)
        {
            if (colorMap.TryGetValue(name, out Color mapped))
                return mapped;
            return CanPayloadGridPalette.ColorForName(name);
        }
    }
}
