namespace logReader.Processing
{
    // Встраивание составных параметров в процессоры с отдельными рядами по устройствам.
    internal static class CompositeOutput
    {
        internal static List<Device> WithComposites(IEnumerable<Device> devices, CompositeRuntime? composites)
        {
            var result = new List<Device>(devices);
            if (composites != null && !composites.IsEmpty)
                result.AddRange(composites.Blocks);
            return result;
        }

        // Снимок блока пишем при приходе источника, когда все посылки цепочки уже встречались.
        internal static void EmitTriggered(
            CompositeRuntime? composites,
            string id,
            double time,
            TimeSeriesCollector collector,
            OutputFilter filter)
        {
            if (composites == null || composites.IsEmpty) return;
            if (!composites.IsSourceId(id)) return;

            foreach (var block in composites.Blocks)
            {
                if (!filter.IsDeviceEnabled(block.ID)) continue;
                if (!block.HasReadyParamForSource(id)) continue;

                block.Decode();
                collector.Record(block, time);
            }
        }
    }
}
