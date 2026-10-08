using System.Drawing.Drawing2D;

namespace logReader.UI.Controls;

internal static class PaintGeometry
{
    public static Color SurfaceFor(Control control)
    {
        for (Control? ancestor = control.Parent; ancestor != null; ancestor = ancestor.Parent)
            if (ancestor.BackColor.A == 255) return ancestor.BackColor;
        return AppTheme.Background;
    }

    public static GraphicsPath Rounded(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        float diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        if (diameter <= 0) { path.AddRectangle(bounds); return path; }
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
