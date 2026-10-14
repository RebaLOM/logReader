using System.Globalization;

namespace logReader.Processing;

// The Locarus profile packs six three-byte SPN/FMI values across three CAN frames.
// A fresh FE25 is the cycle boundary; fragments from preceding cycles are never reused.
internal sealed class LocarusDtcDecoder
{
    private readonly double _timeoutMs;
    private readonly int[]?[] _parts = new int[3][];
    private double _cycleStart = double.NaN;
    private double _lastTime = double.NaN;
    private double _snapshotTime = double.NaN;
    private bool _closed;
    private int? _count;
    private DtcSnapshot _current = DtcSnapshot.Unknown(0);

    public LocarusDtcDecoder(double timeoutMs = 2000)
    {
        if (!double.IsFinite(timeoutMs) || timeoutMs <= 0)
            throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        _timeoutMs = timeoutMs;
    }

    internal static bool IsDiagnosticId(string id) => PartIndex(id) >= 0;

    public DtcSnapshot? OnFrame(string id, ReadOnlySpan<int> payload, double timeMs)
    {
        ObserveTime(timeMs);
        int part = PartIndex(id);
        if (part < 0) return null;
        if (!double.IsFinite(timeMs)) return _current;

        if (part == 0)
        {
            Clear(timeMs);
            _cycleStart = timeMs;
        }
        else if (_closed || double.IsNaN(_cycleStart)) return null;
        bool valid = payload.Length >= 8;
        foreach (int value in payload) valid &= value is >= 0 and <= 255;
        if (!valid)
        {
            _parts[part] = null;
            if (part == 0) _count = null;
            _snapshotTime = timeMs;
            return _current = Decode(timeMs);
        }

        _parts[part] = payload[..8].ToArray();
        if (part == 0) _count = payload[3] & 0x7f;
        _snapshotTime = timeMs;
        _current = Decode(timeMs);
        _closed = _parts.All(p => p != null);
        return _current;
    }

    public DtcSnapshot SnapshotAt(double timeMs)
    {
        ObserveTime(timeMs);
        return _current with { TimeMs = timeMs };
    }

    // Matrix rows and legacy steps are logical snapshots. Empty rows may carry
    // the preceding confirmed state until its TTL, but new fragments must not
    // complete a pending cycle belonging to a different row.
    public void ResetCycle()
    {
        Array.Clear(_parts);
        _count = null;
        _cycleStart = double.NaN;
        _closed = false;
    }

    private void ObserveTime(double timeMs)
    {
        if (!double.IsFinite(timeMs) || (!double.IsNaN(_lastTime) && timeMs < _lastTime)
            || (!double.IsNaN(_snapshotTime) && timeMs - _snapshotTime > _timeoutMs))
            Clear(timeMs);
        else if (!_closed && !double.IsNaN(_cycleStart) && timeMs - _cycleStart > _timeoutMs)
            ResetCycle();
        _lastTime = timeMs;
    }

    private void Clear(double timeMs)
    {
        Array.Clear(_parts);
        _count = null;
        _cycleStart = double.NaN;
        _snapshotTime = double.NaN;
        _closed = false;
        _current = DtcSnapshot.Unknown(timeMs);
    }

    private DtcSnapshot Decode(double timeMs)
    {
        string raw = string.Join("; ", _parts.Select((p, i) => p == null ? "" :
            $"18FE{25 + i}03=" + string.Join(" ", p.Select(b => b.ToString("X2", CultureInfo.InvariantCulture)))).Where(s => s.Length > 0));
        if (_count == null) return DtcSnapshot.Unknown(timeMs, raw);

        int count = _count.Value;
        var slots = Enumerable.Repeat(DtcSlot.Unknown, 6).ToArray();
        for (int i = 0; i < 6; i++)
        {
            int a = PackedByte(i * 3), b = PackedByte(i * 3 + 1), c = PackedByte(i * 3 + 2);
            if (a < 0 || b < 0 || c < 0) continue;
            slots[i] = a == 0 && b == 0 && c == 0 ? DtcSlot.Empty
                : DtcSlot.Present(new DtcCode(a | (b << 8) | ((c & 0xe0) << 11), c & 31));
        }
        // The producer scans every slot. The count describes list quality; it
        // does not guarantee that active entries occupy a contiguous prefix.
        bool complete = count <= 6 && slots.All(s => s.IsKnown)
            && slots.Where(s => s.Code != null).Select(s => s.Code).Distinct().Count() == count;
        return new DtcSnapshot(timeMs, slots, count, complete, raw);
    }

    private int PackedByte(int index)
    {
        int part = index < 2 ? 0 : index < 10 ? 1 : 2;
        int offset = part == 0 ? index + 6 : part == 1 ? index - 2 : index - 10;
        return _parts[part]?[offset] ?? -1;
    }

    private static int PartIndex(string id)
    {
        if (!CanToken.TryNormalizeId(id, out string normalized)) return -1;
        return normalized switch { "18FE2503" => 0, "18FE2603" => 1, "18FE2703" => 2, _ => -1 };
    }
}
