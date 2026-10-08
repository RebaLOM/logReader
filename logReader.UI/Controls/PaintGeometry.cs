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
        if (!float.IsFinite(bounds.X) || !float.IsFinite(bounds.Y) || !float.IsFinite(bounds.Width) || !float.IsFinite(bounds.Height)
            || bounds.Width <= 0 || bounds.Height <= 0) return path;
        float diameter = Math.Min((float.IsFinite(radius) ? Math.Max(0, radius) : 0) * 2, Math.Min(bounds.Width, bounds.Height));
        if (diameter <= 0) { path.AddRectangle(bounds); return path; }
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static RectangleF StrokeBounds(Rectangle client, float strokeWidth, float margin = 0)
    {
        if (!float.IsFinite(strokeWidth) || !float.IsFinite(margin) || strokeWidth <= 0) return RectangleF.Empty;
        float inset = Math.Max(0, margin) + strokeWidth / 2;
        float width = client.Width - inset * 2;
        float height = client.Height - inset * 2;
        return width > 0 && height > 0
            ? new RectangleF(client.Left + inset, client.Top + inset, width, height)
            : RectangleF.Empty;
    }
}
