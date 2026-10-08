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
        private int CellSize => UiScale.Px(this, 28);
        private int LabelColWidth => UiScale.Px(this, 40);
        private int HeaderRowHeight => UiScale.Px(this, 28);
        private int LegendRowHeight => UiScale.Px(this, 24);
        private Color EmptyCell => AppTheme.SurfaceSecondary;
        private Color ConflictColor => AppTheme.Error;

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
        private int? _hoverBit;
        private readonly ToolTip _toolTip = new() { InitialDelay = 350, ReshowDelay = 100, AutoPopDelay = 8000 };

        public CanPayloadGridControl()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = AppTheme.Surface;
            ForeColor = AppTheme.TextPrimary;
            Font = Typography.Mono;
            TabStop = true;
            AccessibleRole = AccessibleRole.Graphic;
            AccessibleName = "Карта битов CAN";
            AccessibleDescription = "В редакторе выберите биты мышью. Стрелки перемещают начало, Shift и стрелки изменяют длину.";
            RebuildSelectionBits();
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
                int v = Math.Clamp(value, 0, 7);
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
                RebuildSelectionBits();
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
            IsLittleEndian = littleEndian;
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
            int gridH = HeaderRowHeight + _dlc * CellSize + UiScale.Px(this, 8);
            int legendH = _showLegend && _overlays.Count > 0 ? UiScale.Px(this, 8) + _overlays.DistinctBy(o => o.Name).Count() * LegendRowHeight : 0;
            int w = LabelColWidth + 8 * CellSize + UiScale.Px(this, 16);
            int h = gridH + legendH + UiScale.Px(this, 4);
            MinimumSize = new Size(w, h);
            Size = new Size(w, h);
        }

        public override Size GetPreferredSize(Size proposedSize)
            => MinimumSize;

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            UpdatePreferredSize();
        }

        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            UpdatePreferredSize();
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Invalidate();
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
            var occupiedByOther = new HashSet<int>();
            if (_mode == CanPayloadGridMode.Edit)
                foreach (var overlay in _overlays.Where(o => !o.IsCurrent))
                    foreach (int bit in BitMath.EnumerateSignalBits(overlay.StartBit, overlay.Length, overlay.IsLittleEndian))
                        if (bit >= 0 && bit < payloadBits) occupiedByOther.Add(bit);
            HashSet<int>? dragPreview = null;
            if (_dragAnchorBit is int dragAnchor && _dragHoverBit is int dragHover
                && TryPreviewBits(dragAnchor, dragHover, payloadBits, out var previewBits))
                dragPreview = previewBits;

            for (int row = 0; row < _dlc; row++)
            {
                int y = HeaderRowHeight + row * CellSize;
                string rowLabel = $"B{row}";
                TextRenderer.DrawText(g, rowLabel, Font, new Rectangle(0, y, LabelColWidth - 4, CellSize),
                    _binByteMode && row != _binByteIndex ? AppTheme.TextMuted : ForeColor, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);

                for (int col = 0; col < 8; col++)
                {
                    int bitInByte = 7 - col;
                    int global = BitMath.CellToGlobalBit(row, bitInByte);
                    if (global >= payloadBits) continue;

                    var rect = CellRect(row, col);
                    bool dimmed = !_binByteMode && owners[global] != null && (
                        (_mode == CanPayloadGridMode.Edit && !owners[global]!.IsCurrent)
                        || (viewHighlightActive && !owners[global]!.IsCurrent));

                    bool inactiveByte = _binByteMode && row != _binByteIndex;
                    bool selected = _mode == CanPayloadGridMode.Edit && _selectionBits.Contains(global) && !inactiveByte;
                    Color fill = EmptyCell;
                    var owner = owners[global];
                    if (owner != null)
                        fill = Blend(owner.Color, EmptyCell, dimmed ? 0.18f : 0.42f);

                    if (overlapCount[global] > 1 || (selected && occupiedByOther.Contains(global)))
                        fill = Blend(ConflictColor, fill, 0.28f);
                    if (selected && owner == null)
                        fill = AppTheme.PrimarySoft;
                    if (inactiveByte)
                        fill = Blend(fill, AppTheme.Surface, 0.18f);
                    if (_hoverBit == global && !inactiveByte)
                        fill = Blend(AppTheme.PrimarySoft, fill, 0.5f);

                    using var brush = new SolidBrush(fill);
                    g.FillRectangle(brush, rect);
                    using var pen = new Pen(AppTheme.Border);
                    g.DrawRectangle(pen, rect);

                    if (selected)
                    {
                        using var selPen = new Pen(AppTheme.Primary, UiScale.Px(this, 2));
                        g.DrawRectangle(selPen, Rectangle.Inflate(rect, -1, -1));
                    }

                    if (viewHighlightActive && currentOverlayBits.Contains(global))
                    {
                        using var selPen = new Pen(AppTheme.Primary, UiScale.Px(this, 2));
                        g.DrawRectangle(selPen, Rectangle.Inflate(rect, -1, -1));
                    }

                    if (dragPreview?.Contains(global) == true)
                    {
                        using var prevBrush = new SolidBrush(Color.FromArgb(90, AppTheme.Primary));
                        g.FillRectangle(prevBrush, rect);
                    }
                    TextRenderer.DrawText(g, global.ToString(), Font, rect,
                        inactiveByte ? AppTheme.TextMuted : AppTheme.TextSecondary,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                }
            }

            for (int col = 0; col < 8; col++)
            {
                int bitInByte = 7 - col;
                int x = LabelColWidth + col * CellSize;
                TextRenderer.DrawText(g, bitInByte.ToString(), Font,
                    new Rectangle(x, 2, CellSize, HeaderRowHeight - 2),
                    AppTheme.TextMuted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            TextRenderer.DrawText(g, "Байт", Typography.Caption, new Rectangle(0, 0, LabelColWidth, HeaderRowHeight),
                AppTheme.TextMuted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            if (_showLegend && _overlays.Count > 0)
                PaintLegend(g);
            if (Focused)
            {
                using var focus = new Pen(AppTheme.Primary, UiScale.Px(this, 1));
                g.DrawRectangle(focus, 0, 0, Width - 1, Height - 1);
            }
        }

        private void PaintLegend(Graphics g)
        {
            int y = HeaderRowHeight + _dlc * CellSize + UiScale.Px(this, 8);
            foreach (var ov in _overlays.DistinctBy(o => o.Name))
            {
                var swatch = new Rectangle(UiScale.Px(this, 4), y + UiScale.Px(this, 6), UiScale.Px(this, 12), UiScale.Px(this, 12));
                using var brush = new SolidBrush(ov.Color);
                g.FillRectangle(brush, swatch);
                using var border = new Pen(AppTheme.Border);
                g.DrawRectangle(border, swatch);
                TextRenderer.DrawText(g, ov.Name, Typography.Secondary, new Rectangle(UiScale.Px(this, 24), y, Width - UiScale.Px(this, 28), LegendRowHeight),
                    ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
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
            Focus();

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
            int? hovered = TryHitTest(e.Location, out int hit) ? hit : null;
            if (_hoverBit != hovered)
            {
                _hoverBit = hovered;
                _toolTip.SetToolTip(this, hovered.HasValue ? DescribeBit(hovered.Value) : null);
                Cursor = hovered.HasValue ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
            if (_dragAnchorBit is not int anchor) return;
            if (!TryHitTest(e.Location, out int bit)) return;
            _dragHoverBit = bit;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoverBit = null;
            _toolTip.SetToolTip(this, null);
            Invalidate();
        }

        private string DescribeBit(int bit)
        {
            var names = _overlays
                .Where(o => BitMath.EnumerateSignalBits(o.StartBit, o.Length, o.IsLittleEndian).Contains(bit))
                .Select(o => o.Name)
                .Distinct()
                .ToList();
            string ownership = names.Count == 0 ? "Свободный бит" : string.Join(", ", names);
            string conflict = names.Count > 1 ? "\nВ этом бите пересекаются сигналы." : "";
            return $"Байт {bit / 8} · бит {bit % 8} · общий бит {bit}\n{ownership}{conflict}";
        }

        protected override bool IsInputKey(Keys keyData)
            => (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End
                || (_mode == CanPayloadGridMode.View && (keyData & Keys.KeyCode) == Keys.Enter)
                || base.IsInputKey(keyData);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            int bit = _mode == CanPayloadGridMode.Edit ? _selectionStartBit : _hoverBit ?? 0;
            int row = Math.Clamp(bit / 8, 0, _dlc - 1);
            int bitInByte = Math.Clamp(bit % 8, 0, 7);
            int length = _selectionLength;
            if (_mode == CanPayloadGridMode.View && e.KeyCode is Keys.Enter or Keys.Space)
            {
                HandleViewModeClick(CellRect(row, 7 - bitInByte).Location + new Size(2, 2));
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            if (e.Shift && _mode == CanPayloadGridMode.Edit)
            {
                if (e.KeyCode is Keys.Right or Keys.Down) length++;
                else if (e.KeyCode is Keys.Left or Keys.Up) length--;
                else return;
            }
            else
            {
                switch (e.KeyCode)
                {
                    case Keys.Left: bitInByte++; break;
                    case Keys.Right: bitInByte--; break;
                    case Keys.Up: row--; break;
                    case Keys.Down: row++; break;
                    case Keys.Home: bitInByte = 7; break;
                    case Keys.End: bitInByte = 0; break;
                    default: return;
                }
            }

            e.Handled = true;
            e.SuppressKeyPress = true;
            if (row < 0 || row >= _dlc || bitInByte < 0 || bitInByte > 7 || length < 1) return;
            if (_binByteMode && (row != _binByteIndex || bitInByte + length > 8)) return;
            int start = BitMath.CellToGlobalBit(row, bitInByte);
            if (_mode == CanPayloadGridMode.Edit)
            {
                if (BitMath.SignalFitsInDlc(start, length, _littleEndian, _dlc * 8))
                    SetSelection(start, length);
            }
            else
            {
                _hoverBit = start;
                Invalidate();
            }
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (Capture) return;
            // OnMouseUp consumes the anchor after releasing capture, so defer cleanup there.
            if ((Control.MouseButtons & MouseButtons.Left) != MouseButtons.None)
            {
                _dragAnchorBit = null;
                _dragHoverBit = null;
                Invalidate();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _toolTip.Dispose();
            base.Dispose(disposing);
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
        private static IReadOnlyList<Color> Colors => AppTheme.PayloadColors;

        public static Color ColorForName(string name)
        {
            if (string.IsNullOrEmpty(name)) return Colors[0];
            int hash = StableHash(name);
            return Colors[PaletteIndex(hash)];
        }

        public static IReadOnlyDictionary<string, Color> AssignColors(IEnumerable<string> names)
        {
            var map = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
            var used = new HashSet<int>();
            foreach (string name in names)
            {
                if (string.IsNullOrEmpty(name) || map.ContainsKey(name))
                    continue;

                int preferred = PaletteIndex(StableHash(name));
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
            for (int offset = 1; offset < Colors.Count; offset++)
            {
                int idx = (preferred + offset) % Colors.Count;
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

        private static int PaletteIndex(int hash)
            => (int)(Math.Abs((long)hash) % Colors.Count);
    }
}
