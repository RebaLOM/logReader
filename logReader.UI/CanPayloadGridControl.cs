using System.ComponentModel;
using logReader;

namespace logReader.UI
{
    internal sealed record SignalOverlay(
        string Name,
        int StartBit,
        int Length,
        bool IsLittleEndian,
        Color Color,
        bool IsCurrent = false);

    internal enum CanPayloadGridMode
    {
        View,
        Edit
    }

    internal sealed class OverlaySelectedEventArgs : EventArgs
    {
        public string? OverlayName { get; init; }
    }

    internal sealed class CanPayloadGridControl : UserControl
    {
        private const int CellSize = 24;
        private const int LabelColWidth = 34;
        private const int HeaderRowHeight = 22;
        private const int LegendRowHeight = 18;

        private static readonly Color EmptyCell = Color.FromArgb(240, 242, 245);
        private static readonly Color ConflictColor = Color.FromArgb(220, 53, 69);

        private int _dlc = 8;
        private CanPayloadGridMode _mode = CanPayloadGridMode.View;
        private bool _showLegend = true;
        private bool _binByteMode;
        private int _binByteIndex;
        private bool _littleEndian = true;
        private int _selectionStartBit;
        private int _selectionLength = 1;
        private HashSet<int> _selectionBits = new();
        private bool _suppressSelectionEvent;

        private List<SignalOverlay> _overlays = new();
        private int? _dragAnchorBit;
        private int? _dragHoverBit;

        public CanPayloadGridControl()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Color.White;
            TabStop = false;
            UpdatePreferredSize();
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int Dlc
        {
            get => _dlc;
            set
            {
                int v = Math.Clamp(value, 1, 8);
                if (_dlc == v) return;
                _dlc = v;
                UpdatePreferredSize();
                Invalidate();
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public CanPayloadGridMode Mode
        {
            get => _mode;
            set
            {
                if (_mode == value) return;
                _mode = value;
                Invalidate();
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ShowLegend
        {
            get => _showLegend;
            set
            {
                if (_showLegend == value) return;
                _showLegend = value;
                UpdatePreferredSize();
                Invalidate();
            }
        }

        // BIN: подсветка только одной строки байта.
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool BinByteMode
        {
            get => _binByteMode;
            set
            {
                if (_binByteMode == value) return;
                _binByteMode = value;
                Invalidate();
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int BinByteIndex
        {
            get => _binByteIndex;
            set
            {
                int v = Math.Clamp(value, 0, _dlc - 1);
                if (_binByteIndex == v) return;
                _binByteIndex = v;
                Invalidate();
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsLittleEndian
        {
            get => _littleEndian;
            set
            {
                if (_littleEndian == value) return;
                _littleEndian = value;
                Invalidate();
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IReadOnlyList<SignalOverlay> Overlays
        {
            get => _overlays;
            set
            {
                _overlays = value?.ToList() ?? new List<SignalOverlay>();
                UpdatePreferredSize();
                Invalidate();
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int SelectionStartBit
        {
            get => _selectionStartBit;
            set => SetSelection(value, _selectionLength, fireEvent: false);
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int SelectionLength
        {
            get => _selectionLength;
            set => SetSelection(_selectionStartBit, value, fireEvent: false);
        }

        public event EventHandler? SelectionChanged;
        public event EventHandler<OverlaySelectedEventArgs>? OverlaySelected;

        public void SetSelection(int startBit, int length, bool fireEvent = true)
        {
            length = Math.Max(1, length);
            if (_selectionStartBit == startBit && _selectionLength == length
                && _selectionBits.Count > 0)
            {
                return;
            }

            _selectionStartBit = startBit;
            _selectionLength = length;
            RebuildSelectionBits();
            Invalidate();

            if (fireEvent && !_suppressSelectionEvent)
                SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        private void RebuildSelectionBits()
        {
            _selectionBits = new HashSet<int>(
                BitMath.EnumerateSignalBits(_selectionStartBit, _selectionLength, _littleEndian));
        }

        public void SetSelectionFromFields(int byteIndex, int bitInByte, int length, bool littleEndian, bool fireEvent = true)
        {
            _littleEndian = littleEndian;
            int start = BitMath.CellToGlobalBit(byteIndex, bitInByte);
            SetSelection(start, length, fireEvent);
        }

        public void ApplySelectionToFields(out int byteIndex, out int bitInByte, out int length)
        {
            byteIndex = _selectionStartBit / 8;
            bitInByte = _selectionStartBit % 8;
            length = _selectionLength;
        }

        public static Color ColorForSignalName(string name) =>
            CanPayloadGridPalette.ColorForName(name);

        private void UpdatePreferredSize()
        {
            int gridH = HeaderRowHeight + _dlc * CellSize + 8;
            int legendH = _showLegend && _overlays.Count > 0 ? 6 + _overlays.Count * LegendRowHeight : 0;
            int w = LabelColWidth + 8 * CellSize + 16;
            int h = gridH + legendH + 4;
            MinimumSize = new Size(w, h);
            Size = new Size(w, h);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.Clear(BackColor);

            int payloadBits = _dlc * 8;
            var owners = new SignalOverlay?[payloadBits];
            var overlapCount = new int[payloadBits];
            BuildOwnership(owners, overlapCount, payloadBits);

            bool viewHighlightActive = _mode == CanPayloadGridMode.View && _overlays.Any(o => o.IsCurrent);
            var currentOverlayBits = new HashSet<int>();
            if (viewHighlightActive)
            {
                foreach (var ov in _overlays.Where(o => o.IsCurrent))
                {
                    foreach (int bit in BitMath.EnumerateSignalBits(ov.StartBit, ov.Length, ov.IsLittleEndian))
                    {
                        if (bit >= 0 && bit < payloadBits)
                            currentOverlayBits.Add(bit);
                    }
                }
            }

            for (int row = 0; row < _dlc; row++)
            {
                if (_binByteMode && row != _binByteIndex)
                    continue;

                int y = HeaderRowHeight + row * CellSize;
                string rowLabel = $"B{row}";
                TextRenderer.DrawText(g, rowLabel, Font, new Rectangle(0, y, LabelColWidth - 4, CellSize),
                    ForeColor, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);

                for (int col = 0; col < 8; col++)
                {
                    int bitInByte = 7 - col;
                    int global = BitMath.CellToGlobalBit(row, bitInByte);
                    if (global >= payloadBits) continue;

                    var rect = CellRect(row, col);
                    bool dimmed = !_binByteMode && owners[global] != null && (
                        (_mode == CanPayloadGridMode.Edit && !owners[global]!.IsCurrent)
                        || (viewHighlightActive && !owners[global]!.IsCurrent));

                    Color fill = EmptyCell;
                    var owner = owners[global];
                    if (owner != null)
                        fill = dimmed ? Blend(owner.Color, EmptyCell, 0.55f) : owner.Color;

                    if (overlapCount[global] > 1)
                        fill = Blend(ConflictColor, fill, 0.45f);

                    using var brush = new SolidBrush(fill);
                    g.FillRectangle(brush, rect);
                    using var pen = new Pen(Color.FromArgb(180, 180, 190));
                    g.DrawRectangle(pen, rect);

                    if (_mode == CanPayloadGridMode.Edit && _selectionBits.Contains(global))
                    {
                        using var selPen = new Pen(Color.FromArgb(30, 60, 120), 2);
                        g.DrawRectangle(selPen, Rectangle.Inflate(rect, -1, -1));
                    }

                    if (viewHighlightActive && currentOverlayBits.Contains(global))
                    {
                        using var selPen = new Pen(Color.FromArgb(30, 60, 120), 2);
                        g.DrawRectangle(selPen, Rectangle.Inflate(rect, -1, -1));
                    }

                    if (_dragAnchorBit is int anchor && _dragHoverBit is int hover)
                    {
                        if (TryPreviewBits(anchor, hover, payloadBits, out var preview) && preview.Contains(global))
                        {
                            using var prevBrush = new SolidBrush(Color.FromArgb(90, 100, 149, 237));
                            g.FillRectangle(prevBrush, rect);
                        }
                    }
                }
            }

            for (int col = 0; col < 8; col++)
            {
                int bitInByte = 7 - col;
                int x = LabelColWidth + col * CellSize;
                TextRenderer.DrawText(g, bitInByte.ToString(), Font,
                    new Rectangle(x, 2, CellSize, HeaderRowHeight - 2),
                    Color.DimGray, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            if (_showLegend && _overlays.Count > 0)
                PaintLegend(g);
        }

        private void PaintLegend(Graphics g)
        {
            int y = HeaderRowHeight + _dlc * CellSize + 8;
            foreach (var ov in _overlays.DistinctBy(o => o.Name))
            {
                var swatch = new Rectangle(4, y + 3, 12, 12);
                using var brush = new SolidBrush(ov.Color);
                g.FillRectangle(brush, swatch);
                g.DrawRectangle(Pens.Gray, swatch);
                TextRenderer.DrawText(g, ov.Name, Font, new Rectangle(22, y, Width - 24, LegendRowHeight),
                    ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                y += LegendRowHeight;
            }
        }

        private Rectangle CellRect(int row, int col)
        {
            int x = LabelColWidth + col * CellSize;
            int y = HeaderRowHeight + row * CellSize;
            return new Rectangle(x + 1, y + 1, CellSize - 2, CellSize - 2);
        }

        private bool TryHitTest(Point client, out int globalBit)
        {
            globalBit = -1;
            int payloadBits = _dlc * 8;
            for (int row = 0; row < _dlc; row++)
            {
                if (_binByteMode && row != _binByteIndex) continue;
                for (int col = 0; col < 8; col++)
                {
                    if (!CellRect(row, col).Contains(client)) continue;
                    int bitInByte = 7 - col;
                    globalBit = BitMath.CellToGlobalBit(row, bitInByte);
                    return globalBit < payloadBits;
                }
            }
            return false;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;

            if (_mode == CanPayloadGridMode.View)
            {
                HandleViewModeClick(e.Location);
                return;
            }

            if (_mode != CanPayloadGridMode.Edit) return;
            if (!TryHitTest(e.Location, out int bit)) return;
            _dragAnchorBit = bit;
            _dragHoverBit = bit;
            Capture = true;
            Invalidate();
        }

        private void HandleViewModeClick(Point location)
        {
            if (!TryHitTest(location, out int bit))
            {
                OverlaySelected?.Invoke(this, new OverlaySelectedEventArgs { OverlayName = null });
                return;
            }

            int payloadBits = _dlc * 8;
            var owners = new SignalOverlay?[payloadBits];
            var overlapCount = new int[payloadBits];
            BuildOwnership(owners, overlapCount, payloadBits);

            string? name = bit >= 0 && bit < payloadBits ? owners[bit]?.Name : null;
            OverlaySelected?.Invoke(this, new OverlaySelectedEventArgs { OverlayName = name });
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragAnchorBit is not int anchor) return;
            if (!TryHitTest(e.Location, out int bit)) return;
            _dragHoverBit = bit;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_dragAnchorBit is not int anchor) return;

            Capture = false;
            int payloadBits = _dlc * 8;
            if (TryHitTest(e.Location, out int target))
            {
                bool built = _binByteMode
                    ? TryBuildBinSelection(anchor, target, out int binStart, out int binLen)
                    : BitMath.TryBuildSelectionFromGlobalBits(anchor, target, _littleEndian, payloadBits, out binStart, out binLen);

                if (built)
                {
                    _suppressSelectionEvent = true;
                    SetSelection(binStart, binLen, fireEvent: false);
                    _suppressSelectionEvent = false;
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                }
            }

            _dragAnchorBit = null;
            _dragHoverBit = null;
            Invalidate();
        }

        private bool TryBuildBinSelection(int anchor, int target, out int startBit, out int length)
        {
            startBit = 0;
            length = 0;
            int row = _binByteIndex;
            if (anchor / 8 != row || target / 8 != row) return false;
            int lo = Math.Min(anchor, target);
            int hi = Math.Max(anchor, target);
            startBit = lo;
            length = hi - lo + 1;
            return length > 0;
        }

        private bool TryPreviewBits(int anchor, int hover, int payloadBits, out HashSet<int> bits)
        {
            bits = new HashSet<int>();
            if (_binByteMode)
            {
                if (!TryBuildBinSelection(anchor, hover, out int start, out int len)) return false;
                foreach (int b in BitMath.EnumerateSignalBits(start, len, littleEndian: true))
                    bits.Add(b);
                return true;
            }

            if (!BitMath.TryBuildSelectionFromGlobalBits(anchor, hover, _littleEndian, payloadBits, out int startBit, out int length))
                return false;
            foreach (int b in BitMath.EnumerateSignalBits(startBit, length, _littleEndian))
                bits.Add(b);
            return true;
        }

        private void BuildOwnership(SignalOverlay?[] owners, int[] overlapCount, int payloadBits)
        {
            foreach (var ov in _overlays)
            {
                foreach (int bit in BitMath.EnumerateSignalBits(ov.StartBit, ov.Length, ov.IsLittleEndian))
                {
                    if (bit < 0 || bit >= payloadBits) continue;
                    overlapCount[bit]++;
                    owners[bit] ??= ov;
                }
            }
        }

        private static Color Blend(Color a, Color b, float t)
        {
            float u = 1f - t;
            return Color.FromArgb(
                255,
                (int)(a.R * t + b.R * u),
                (int)(a.G * t + b.G * u),
                (int)(a.B * t + b.B * u));
        }
    }

    internal static class CanPayloadGridPalette
    {
        private static readonly Color[] Colors =
        {
            Color.FromArgb(91, 141, 239),
            Color.FromArgb(246, 153, 63),
            Color.FromArgb(87, 187, 138),
            Color.FromArgb(214, 96, 150),
            Color.FromArgb(168, 118, 220),
            Color.FromArgb(60, 170, 193),
            Color.FromArgb(220, 176, 74),
            Color.FromArgb(140, 150, 165),
            Color.FromArgb(111, 183, 128),
            Color.FromArgb(196, 120, 90),
            Color.FromArgb(120, 145, 210),
            Color.FromArgb(175, 130, 190),
            Color.FromArgb(100, 175, 160),
            Color.FromArgb(210, 140, 120),
            Color.FromArgb(130, 160, 100),
            Color.FromArgb(170, 110, 110),
        };

        public static Color ColorForName(string name)
        {
            if (string.IsNullOrEmpty(name)) return Colors[0];
            int hash = StableHash(name);
            return Colors[Math.Abs(hash) % Colors.Length];
        }

        public static IReadOnlyDictionary<string, Color> AssignColors(IEnumerable<string> names)
        {
            var map = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
            var used = new HashSet<int>();
            foreach (string name in names)
            {
                if (string.IsNullOrEmpty(name) || map.ContainsKey(name))
                    continue;

                int preferred = Math.Abs(StableHash(name)) % Colors.Length;
                int idx = FindFreeIndex(preferred, used);
                used.Add(idx);
                map[name] = Colors[idx];
            }
            return map;
        }

        private static int FindFreeIndex(int preferred, HashSet<int> used)
        {
            if (!used.Contains(preferred))
                return preferred;
            for (int offset = 1; offset < Colors.Length; offset++)
            {
                int idx = (preferred + offset) % Colors.Length;
                if (!used.Contains(idx))
                    return idx;
            }
            return preferred;
        }

        private static int StableHash(string s)
        {
            unchecked
            {
                int h = 17;
                foreach (char c in s)
                    h = h * 31 + char.ToUpperInvariant(c);
                return h;
            }
        }
    }
}
