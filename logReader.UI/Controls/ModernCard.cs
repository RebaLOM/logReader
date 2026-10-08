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
        using var path = PaintGeometry.Rounded(new RectangleF(.5f, .5f, Math.Max(1, Width - 1), Math.Max(1, Height - 1)), UiScale.Px(this, 12));
        using var brush = new SolidBrush(BackColor);
        using var pen = new Pen(AppTheme.Border, Math.Max(1f, DeviceDpi / 96f));
        e.Graphics.FillPath(brush, path);
        e.Graphics.DrawPath(pen, path);
    }
}
