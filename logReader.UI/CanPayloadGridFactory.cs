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
            var list = new List<SignalOverlay>();
            foreach (var s in signals)
            {
                bool isCurrent = currentName != null
                    && s.Name.Equals(currentName, StringComparison.OrdinalIgnoreCase);
                list.Add(new SignalOverlay(
                    s.Name,
                    s.StartBit,
                    s.Length,
                    s.IsLittleEndian,
                    ResolveColor(s.Name, colorMap),
                    isCurrent));
            }
            return list;
        }

        public static List<SignalOverlay> FromDeviceRows(
            IEnumerable<DeviceFieldRow> rows,
            string? currentHeader = null,
            IReadOnlyDictionary<string, Color>? colorMap = null)
        {
            var list = new List<SignalOverlay>();
            foreach (var r in rows)
            {
                string name = r.Header ?? "";
                bool isCurrent = currentHeader != null
                    && name.Equals(currentHeader, StringComparison.OrdinalIgnoreCase);

                if (string.Equals(r.Type, "BIN", StringComparison.OrdinalIgnoreCase))
                {
                    int bitStart = r.BitStart ?? 0;
                    int global = BitMath.CellToGlobalBit(r.StartBit, bitStart);
                    list.Add(new SignalOverlay(
                        name,
                        global,
                        r.Length,
                        IsLittleEndian: true,
                        ResolveColor(name, colorMap),
                        isCurrent));
                }
                else
                {
                    list.Add(new SignalOverlay(
                        name,
                        r.StartBit,
                        r.Length,
                        r.IsLittleEndian,
                        ResolveColor(name, colorMap),
                        isCurrent));
                }
            }
            return list;
        }

        private static Color ResolveColor(string name, IReadOnlyDictionary<string, Color>? colorMap)
        {
            if (colorMap != null
                && colorMap.TryGetValue(name, out Color mapped))
                return mapped;
            return CanPayloadGridPalette.ColorForName(name);
        }
    }
}
