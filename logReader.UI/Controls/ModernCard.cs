using System.Drawing.Drawing2D;

namespace logReader.UI.Controls;

public class ModernCard : Panel
{
    public ModernCard()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = AppTheme.Surface;
        ForeColor = AppTheme.TextPrimary;
        Padding = new Padding(16);
        Margin = new Padding(0, 0, 0, 16);
    }

    protected override void OnPaintBackground(PaintEventArgs e) =>
        e.Graphics.Clear(PaintGeometry.SurfaceFor(this));

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        float stroke = Math.Max(1f, DeviceDpi / 96f);
        float inset = stroke / 2f + .5f;
        using var path = PaintGeometry.Rounded(new RectangleF(inset, inset, Math.Max(1, Width - 2 * inset), Math.Max(1, Height - 2 * inset)), UiScale.Px(this, 12));
        using var brush = new SolidBrush(BackColor);
        using var pen = new Pen(AppTheme.Border, stroke);
        e.Graphics.FillPath(brush, path);
        e.Graphics.DrawPath(pen, path);
    }
}
