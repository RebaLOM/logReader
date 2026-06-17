using System.Globalization;

namespace logReader
{
    // Составной блок как Device: переиспользуем вывод и фильтры; байты — из CompositeRuntime.
    public sealed class CompositeDevice : Device
    {
        private readonly CompositeRuntime _runtime;

        public IReadOnlyList<CompositeSignal> Signals { get; }

        public CompositeDevice(string block, List<CompositeSignal> signals, CompositeRuntime runtime)
            : base(block, signals.Count)
        {
            _runtime = runtime;
            Signals = signals;
            for (int i = 0; i < signals.Count; i++)
                headers[i] = signals[i].Param;
        }

        // Параметр готов, когда все источники цепочки уже встречались в логе.
        public bool HasReadyParamForSource(string sourceId)
        {
            for (int i = 0; i < Signals.Count; i++)
            {
                var sig = Signals[i];
                if (!_runtime.AllSourcesSeen(sig)) continue;

                foreach (var p in sig.Pieces)
                {
                    if (string.Equals(p.SourceId, sourceId, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            return false;
        }

        public override void Decode()
        {
            for (int i = 0; i < Signals.Count; i++)
                ProcessedData[i] = Compute(Signals[i]);
        }

        private string Compute(CompositeSignal sig)
        {
            if (sig.Pieces.Count == 0 || !_runtime.AllSourcesSeen(sig))
                return "";

            ulong raw = 0;
            int totalBits = 0;
            foreach (var p in sig.Pieces)
            {
                int b = _runtime.GetByte(p.SourceId, p.Byte) & 0xFF;
                int len = Math.Clamp(p.BitLen, 1, 8);
                int start = Math.Clamp(p.BitStart, 0, 7);
                int mask = (1 << len) - 1;
                int piece = (b >> start) & mask;

                if (totalBits + len > 64) break;
                raw = (raw << len) | (uint)piece;
                totalBits += len;
            }

            long value;
            if (sig.Signed && totalBits > 0 && totalBits < 64)
            {
                ulong signBit = 1UL << (totalBits - 1);
                if ((raw & signBit) != 0)
                    raw |= ~((1UL << totalBits) - 1);
                value = unchecked((long)raw);
            }
            else
            {
                value = unchecked((long)raw);
            }

            double phys = (value * sig.Scale) + sig.Offset;
            return phys.ToString(CultureInfo.InvariantCulture);
        }
    }

    // Последние байты каждого источника; обновляется для всех посылок лога, вне фильтров устройств.
    public sealed class CompositeRuntime
    {
        private readonly Dictionary<string, int[]> _bytes = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _seen = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<CompositeDevice>> _triggerToBlocks =
            new(StringComparer.OrdinalIgnoreCase);

        public List<CompositeDevice> Blocks { get; } = new();

        public bool IsEmpty => Blocks.Count == 0;

        public IReadOnlyCollection<string> SourceIds => _bytes.Keys;

        public bool IsSourceId(string id) => _bytes.ContainsKey(id);

        public bool IsTriggerId(string id) => _triggerToBlocks.ContainsKey(id);

        public IReadOnlyList<CompositeDevice> BlocksTriggeredBy(string id)
            => _triggerToBlocks.TryGetValue(id, out var list)
                ? list
                : (IReadOnlyList<CompositeDevice>)Array.Empty<CompositeDevice>();

        public int GetByte(string sourceId, int byteIndex)
        {
            if (byteIndex < 0 || byteIndex > 7) return 0;
            return _bytes.TryGetValue(sourceId, out var arr) ? arr[byteIndex] : 0;
        }

        public bool AllSourcesSeen(CompositeSignal sig)
        {
            foreach (var p in sig.Pieces)
                if (!_seen.Contains(p.SourceId)) return false;
            return true;
        }

        public void Reset()
        {
            _seen.Clear();
            foreach (var arr in _bytes.Values)
                Array.Clear(arr, 0, arr.Length);
            foreach (var block in Blocks)
                for (int i = 0; i < block.ProcessedData.Length; i++)
                    block.ProcessedData[i] = "";
        }

        public void OnMessage(string id, ReadOnlySpan<int> bytes, int count)
        {
            if (!_bytes.TryGetValue(id, out var arr)) return;
            for (int i = 0; i < 8; i++)
                arr[i] = i < count ? (bytes[i] & 0xFF) : 0;
            _seen.Add(id);
        }

        public void OnMessage(string id, int[] bytes)
            => OnMessage(id, bytes.AsSpan(), Math.Min(bytes.Length, 8));

        public static CompositeRuntime Build(IEnumerable<CompositeSignal> signals)
        {
            var runtime = new CompositeRuntime();

            var byBlock = new Dictionary<string, List<CompositeSignal>>(StringComparer.OrdinalIgnoreCase);
            var blockOrder = new List<string>();

            foreach (var sig in signals)
            {
                if (sig.Pieces.Count == 0 || string.IsNullOrWhiteSpace(sig.Param))
                    continue;

                if (string.IsNullOrWhiteSpace(sig.TriggerId))
                    sig.TriggerId = sig.ResolveDefaultTriggerId();

                string block = string.IsNullOrWhiteSpace(sig.Block) ? CompositeDefaults.BlockName : sig.Block;

                if (!byBlock.TryGetValue(block, out var list))
                {
                    list = new List<CompositeSignal>();
                    byBlock[block] = list;
                    blockOrder.Add(block);
                }
                list.Add(sig);

                foreach (var p in sig.Pieces)
                {
                    if (!runtime._bytes.ContainsKey(p.SourceId))
                        runtime._bytes[p.SourceId] = new int[8];
                }
            }

            foreach (var block in blockOrder)
            {
                var device = new CompositeDevice(block, byBlock[block], runtime);
                runtime.Blocks.Add(device);

                foreach (var sig in device.Signals)
                {
                    string trigger = string.IsNullOrWhiteSpace(sig.TriggerId)
                        ? sig.ResolveDefaultTriggerId()
                        : sig.TriggerId;
                    if (string.IsNullOrWhiteSpace(trigger)) continue;

                    if (!runtime._triggerToBlocks.TryGetValue(trigger, out var blocks))
                    {
                        blocks = new List<CompositeDevice>();
                        runtime._triggerToBlocks[trigger] = blocks;
                    }
                    if (!blocks.Contains(device))
                        blocks.Add(device);
                }
            }

            return runtime;
        }
    }
}
