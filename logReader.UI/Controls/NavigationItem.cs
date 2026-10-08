using System.ComponentModel;
using logReader.UI.Theme;

namespace logReader.UI.Controls;

public class NavigationItem : Control
{
    private bool _selected;
    private bool _hover;

    public NavigationItem()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        DoubleBuffered = true;
        Height = 40;
        Cursor = Cursors.Hand;
        Font = Typography.Body();
        // PushButton + AccessibleName: FlaUI видит и AutomationId (Name), и локализованный текст.
        AccessibleRole = AccessibleRole.PushButton;
        TabStop = true;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // Без полной заливки при ресайзе остаются вертикальные артефакты.
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

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        // Visible label for AT; Designer Name stays as UIA AutomationId.
        AccessibleName = Text;
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new NavigationItemAccessibleObject(this);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // TabStop=true + PushButton: Space/Enter должны активировать как клик (UIA DoDefaultAction недостаточно).
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
        Color bg = _selected ? p.Selection : _hover ? p.Elevated : p.SurfaceSecondary;
        using (var brush = new SolidBrush(bg))
            e.Graphics.FillRectangle(brush, ClientRectangle);

        if (_selected)
        {
            using var accent = new SolidBrush(p.Primary);
            e.Graphics.FillRectangle(accent, new Rectangle(0, 8, 3, Height - 16));
        }

        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            new Rectangle(Spacing.Md, 0, Width - Spacing.Md, Height),
            _selected ? p.Text : p.TextSecondary,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
    }

    private sealed class NavigationItemAccessibleObject : ControlAccessibleObject
    {
        public NavigationItemAccessibleObject(NavigationItem owner) : base(owner) { }

        public override string? Name
        {
            get
            {
                if (Owner is not NavigationItem owner)
                    return base.Name;
                return string.IsNullOrEmpty(owner.AccessibleName) ? owner.Text : owner.AccessibleName;
            }
            set => base.Name = value;
        }

        public override AccessibleRole Role => AccessibleRole.PushButton;

        public override string? DefaultAction => "Нажать";

        public override void DoDefaultAction()
        {
            if (Owner is NavigationItem owner)
                owner.OnClick(EventArgs.Empty);
        }
    }
}
