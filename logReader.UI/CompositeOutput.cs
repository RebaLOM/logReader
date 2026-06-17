using logReader;

namespace logReader.UI
{
    // Встраивание составных параметров в процессоры с отдельными рядами deviceData.
    internal static class CompositeOutput
    {
        internal static List<Device> WithComposites(List<Device> devices, CompositeRuntime? composites)
        {
            if (composites == null || composites.IsEmpty)
                return devices;

            var result = new List<Device>(devices);
            result.AddRange(composites.Blocks);
            return result;
        }

        // Снимок блока пишем при приходе источника, когда все посылки цепочки уже видели.
        internal static void EmitTriggered(
            CompositeRuntime? composites,
            string id,
            double timeVal,
            Dictionary<string, List<(double TimeVal, string[] Values)>> deviceData)
        {
            if (composites == null || composites.IsEmpty) return;
            if (!composites.IsSourceId(id)) return;

            foreach (var block in composites.Blocks)
            {
                if (!block.HasReadyParamForSource(id)) continue;

                block.Decode();

                if (!deviceData.TryGetValue(block.ID, out var list))
                {
                    list = new List<(double, string[])>();
                    deviceData[block.ID] = list;
                }
                list.Add((timeVal, (string[])block.ProcessedData.Clone()));
            }
        }
    }
}
