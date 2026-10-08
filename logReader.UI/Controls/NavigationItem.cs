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
}
