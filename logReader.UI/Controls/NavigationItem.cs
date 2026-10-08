using System.ComponentModel;
using System.Drawing.Drawing2D;
using logReader.UI.Icons;
using logReader.UI.Theme;

namespace logReader.UI.Controls;

// Пункт боковой навигации: текстовый или icon-rail с жёлтой «таблеткой» выбора.
public class NavigationItem : Control
{
    private bool _selected;
    private bool _hover;
    private IconKind _icon = IconKind.Workspace;
    private bool _railMode = true;

    public NavigationItem()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        DoubleBuffered = true;
        Height = 48;
        Cursor = Cursors.Hand;
        Font = Typography.Body();
        AccessibleRole = AccessibleRole.PushButton;
        TabStop = true;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        using var brush = new SolidBrush(AppTheme.Palette.SurfaceSecondary);
        e.Graphics.FillRectangle(brush, ClientRectangle);
    }

    [DefaultValue(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public bool Selected
    {
        get => _selected;
        set
        {
            if (_selected == value) return;
            _selected = value;
            Invalidate();
        }
    }

    [DefaultValue(IconKind.Workspace)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public IconKind Icon
    {
        get => _icon;
        set
        {
            if (_icon == value) return;
            _icon = value;
            Invalidate();
        }
    }

    [DefaultValue(true)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public bool RailMode
    {
        get => _railMode;
        set
        {
            if (_railMode == value) return;
            _railMode = value;
            Height = value ? 48 : 40;
            Invalidate();
        }
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        AccessibleName = Text;
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new NavigationItemAccessibleObject(this);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            OnClick(EventArgs.Empty);
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        base.OnKeyDown(e);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hover = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = false;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        ThemePalette p = AppTheme.Palette;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        if (_railMode)
            PaintRail(e.Graphics, p);
        else
            PaintTextRow(e.Graphics, p);
    }

    private void PaintRail(Graphics g, ThemePalette p)
    {
        int pad = 10;
        var pill = new Rectangle(pad, 6, Width - pad * 2, Height - 12);

        if (_selected || _hover)
        {
            Color fill = _selected ? p.Primary : p.Elevated;
            using var path = RoundedRect(pill, Radius.Md);
            using var brush = new SolidBrush(fill);
            g.FillPath(brush, path);
        }

        Color iconColor = _selected ? p.OnPrimary : _hover ? p.Text : p.TextSecondary;
        int iconSize = 20;
        var iconBounds = new Rectangle(
            (Width - iconSize) / 2,
            (Height - iconSize) / 2,
            iconSize,
            iconSize);
        AppIcons.Draw(g, _icon, iconBounds, iconColor);
    }

    private void PaintTextRow(Graphics g, ThemePalette p)
    {
        Color bg = _selected ? p.Selection : _hover ? p.Elevated : p.SurfaceSecondary;
        using (var brush = new SolidBrush(bg))
            g.FillRectangle(brush, ClientRectangle);

        if (_selected)
        {
            using var accent = new SolidBrush(p.Primary);
            g.FillRectangle(accent, new Rectangle(0, 8, 3, Height - 16));
        }

        TextRenderer.DrawText(
            g,
            Text,
            Font,
            new Rectangle(Spacing.Md, 0, Width - Spacing.Md, Height),
            _selected ? p.Text : p.TextSecondary,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
    }

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        int d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private sealed class NavigationItemAccessibleObject : ControlAccessibleObject
    {
        private readonly NavigationItem _owner;

        public NavigationItemAccessibleObject(NavigationItem owner) : base(owner) => _owner = owner;

        public override string Name => _owner.AccessibleName ?? _owner.Text;

        public override AccessibleStates State
        {
            get
            {
                AccessibleStates s = base.State;
                if (_owner.Selected)
                    s |= AccessibleStates.Checked | AccessibleStates.Selected;
                return s;
            }
        }

        public override void DoDefaultAction() => _owner.OnClick(EventArgs.Empty);
    }
}
