using logReader.UI.Theme;

namespace logReader.UI.Icons;

public enum IconKind
{
    Folder,
    File,
    Play,
    Help,
    Convert,
    Device,
    Moon,
    Sun,
    Check,
    Alert,
}

// Лёгкие stroke-иконки без внешних пакетов (DPI через size).
public static class AppIcons
{
    public static void Draw(Graphics g, IconKind kind, Rectangle bounds, Color color)
    {
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var pen = new Pen(color, Math.Max(1.2f, bounds.Width / 12f));
        pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
        pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
        pen.LineJoin = System.Drawing.Drawing2D.LineJoin.Round;

        float x = bounds.X;
        float y = bounds.Y;
        float w = bounds.Width;
        float h = bounds.Height;
        float p = w * 0.18f;

        switch (kind)
        {
            case IconKind.Folder:
                g.DrawRectangle(pen, x + p, y + h * 0.35f, w - 2 * p, h * 0.45f);
                g.DrawLine(pen, x + p, y + h * 0.35f, x + p + w * 0.28f, y + h * 0.22f);
                g.DrawLine(pen, x + p + w * 0.28f, y + h * 0.22f, x + p + w * 0.45f, y + h * 0.35f);
                break;
            case IconKind.Play:
                PointF[] tri =
                [
                    new(x + p * 1.2f, y + p),
                    new(x + w - p, y + h / 2f),
                    new(x + p * 1.2f, y + h - p),
                ];
                g.DrawPolygon(pen, tri);
                break;
            case IconKind.Help:
                g.DrawEllipse(pen, x + p, y + p, w - 2 * p, h - 2 * p);
                break;
            case IconKind.Check:
                g.DrawLines(pen, new[]
                {
                    new PointF(x + p, y + h * 0.55f),
                    new PointF(x + w * 0.4f, y + h - p * 1.2f),
                    new PointF(x + w - p, y + p * 1.2f),
                });
                break;
            case IconKind.Alert:
                g.DrawPolygon(pen, new[]
                {
                    new PointF(x + w / 2f, y + p),
                    new PointF(x + w - p, y + h - p),
                    new PointF(x + p, y + h - p),
                });
                break;
            default:
                g.DrawRectangle(pen, x + p, y + p, w - 2 * p, h - 2 * p);
                break;
        }
    }

    public static Bitmap Render(IconKind kind, int size, Color? color = null)
    {
        var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Transparent);
        Draw(g, kind, new Rectangle(0, 0, size, size), color ?? AppTheme.Palette.Text);
        return bmp;
    }
}
