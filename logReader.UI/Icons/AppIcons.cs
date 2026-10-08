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
    Workspace,
    Settings,
}

// Лёгкие stroke-иконки без внешних пакетов (DPI через size).
public static class AppIcons
{
    public static void Draw(Graphics g, IconKind kind, Rectangle bounds, Color color)
    {
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var pen = new Pen(color, Math.Max(1.4f, bounds.Width / 11f));
        pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
        pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
        pen.LineJoin = System.Drawing.Drawing2D.LineJoin.Round;

        float x = bounds.X;
        float y = bounds.Y;
        float w = bounds.Width;
        float h = bounds.Height;
        float p = w * 0.2f;
        float cx = x + w / 2f;
        float cy = y + h / 2f;

        switch (kind)
        {
            case IconKind.Folder:
                g.DrawRectangle(pen, x + p, y + h * 0.35f, w - 2 * p, h * 0.45f);
                g.DrawLine(pen, x + p, y + h * 0.35f, x + p + w * 0.28f, y + h * 0.22f);
                g.DrawLine(pen, x + p + w * 0.28f, y + h * 0.22f, x + p + w * 0.45f, y + h * 0.35f);
                break;
            case IconKind.File:
                g.DrawRectangle(pen, x + p * 1.1f, y + p, w - 2.2f * p, h - 2 * p);
                g.DrawLine(pen, x + p * 1.1f + (w - 2.2f * p) * 0.55f, y + p, x + w - p * 1.1f, y + p + h * 0.22f);
                break;
            case IconKind.Play:
                PointF[] tri =
                [
                    new(x + p * 1.3f, y + p),
                    new(x + w - p, cy),
                    new(x + p * 1.3f, y + h - p),
                ];
                g.DrawPolygon(pen, tri);
                break;
            case IconKind.Workspace:
                // Сетка из четырёх плиток — «рабочая область», не play.
                float cell = (w - 2.4f * p) / 2f;
                float gap = p * 0.35f;
                g.DrawRectangle(pen, x + p, y + p, cell, cell);
                g.DrawRectangle(pen, x + p + cell + gap, y + p, cell, cell);
                g.DrawRectangle(pen, x + p, y + p + cell + gap, cell, cell);
                g.DrawRectangle(pen, x + p + cell + gap, y + p + cell + gap, cell, cell);
                break;
            case IconKind.Help:
            {
                g.DrawEllipse(pen, x + p, y + p, w - 2 * p, h - 2 * p);
                using var qFont = new Font(Typography.UiFamily, Math.Max(7f, bounds.Width * 0.42f), FontStyle.Bold);
                TextRenderer.DrawText(
                    g,
                    "?",
                    qFont,
                    bounds,
                    color,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                break;
            }
            case IconKind.Convert:
            {
                // Две горизонтальные стрелки обмена форматами.
                float midY1 = y + h * 0.38f;
                float midY2 = y + h * 0.62f;
                g.DrawLine(pen, x + p, midY1, x + w - p * 1.4f, midY1);
                g.DrawLine(pen, x + w - p * 1.4f, midY1, x + w - p * 2.1f, midY1 - p * 0.55f);
                g.DrawLine(pen, x + w - p * 1.4f, midY1, x + w - p * 2.1f, midY1 + p * 0.55f);
                g.DrawLine(pen, x + w - p, midY2, x + p * 1.4f, midY2);
                g.DrawLine(pen, x + p * 1.4f, midY2, x + p * 2.1f, midY2 - p * 0.55f);
                g.DrawLine(pen, x + p * 1.4f, midY2, x + p * 2.1f, midY2 + p * 0.55f);
                break;
            }
            case IconKind.Device:
                g.DrawRectangle(pen, x + p, y + p * 1.3f, w - 2 * p, h - 2.6f * p);
                g.DrawLine(pen, cx, y + h - p * 1.3f, cx, y + h - p * 0.6f);
                g.DrawLine(pen, cx - w * 0.18f, y + h - p * 0.6f, cx + w * 0.18f, y + h - p * 0.6f);
                break;
            case IconKind.Moon:
                g.DrawArc(pen, x + p, y + p, w - 2 * p, h - 2 * p, 40, 280);
                break;
            case IconKind.Sun:
                g.DrawEllipse(pen, cx - w * 0.16f, cy - h * 0.16f, w * 0.32f, h * 0.32f);
                for (int i = 0; i < 8; i++)
                {
                    double a = i * Math.PI / 4;
                    float x1 = cx + (float)Math.Cos(a) * w * 0.28f;
                    float y1 = cy + (float)Math.Sin(a) * h * 0.28f;
                    float x2 = cx + (float)Math.Cos(a) * w * 0.42f;
                    float y2 = cy + (float)Math.Sin(a) * h * 0.42f;
                    g.DrawLine(pen, x1, y1, x2, y2);
                }
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
                    new PointF(cx, y + p),
                    new PointF(x + w - p, y + h - p),
                    new PointF(x + p, y + h - p),
                });
                break;
            case IconKind.Settings:
                g.DrawEllipse(pen, cx - w * 0.14f, cy - h * 0.14f, w * 0.28f, h * 0.28f);
                g.DrawEllipse(pen, x + p * 0.7f, y + p * 0.7f, w - 1.4f * p, h - 1.4f * p);
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
