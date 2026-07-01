using logReader;

namespace logReader.UI
{
    internal static class CanPayloadGridFactory
    {
        public static List<SignalOverlay> FromDbcSignals(
            IEnumerable<DbcSignal> signals,
            string? currentName = null)
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
                    CanPayloadGridPalette.ColorForName(s.Name),
                    isCurrent));
            }
            return list;
        }

        public static List<SignalOverlay> FromDeviceRows(
            IEnumerable<DeviceFieldRow> rows,
            string? currentHeader = null)
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
                        CanPayloadGridPalette.ColorForName(name),
                        isCurrent));
                }
                else
                {
                    list.Add(new SignalOverlay(
                        name,
                        r.StartBit,
                        r.Length,
                        r.IsLittleEndian,
                        CanPayloadGridPalette.ColorForName(name),
                        isCurrent));
                }
            }
            return list;
        }
    }
}
