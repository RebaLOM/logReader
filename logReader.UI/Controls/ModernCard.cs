using System.ComponentModel;
using System.Drawing.Drawing2D;
using logReader.UI.Theme;

namespace logReader.UI.Controls;

// Карточка-поверхность Forge: скругление + border, без теней (perf).
public class ModernCard : Panel
{
    private readonly Label _title;

    public ModernCard()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        DoubleBuffered = true;
        Padding = new Padding(Spacing.Md);
        Margin = new Padding(0, 0, 0, Spacing.Sm);
        Tag = ThemeTags.Surface;

        _title = new Label
        {
            AutoSize = true,
            Location = new Point(Spacing.Md, Spacing.Sm),
            Font = Typography.CardTitle(),
            Tag = ThemeTags.Brand,
        };
        Controls.Add(_title);
    }

    [DefaultValue("")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public string Title
    {
        get => _title.Text;
        set => _title.Text = value;
    }

    public int ContentTop => Spacing.Sm + _title.Height + Spacing.Xs;

    public void ApplyTheme(ThemePalette p)
    {
        BackColor = p.Surface;
        _title.ForeColor = p.Text;
        Invalidate();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // Углы вне скругления совпадают с фоном workspace (Surface), не с Canvas окна.
        Color behind = Parent?.BackColor ?? AppTheme.Palette.Surface;
        using (var clear = new SolidBrush(behind))
            e.Graphics.FillRectangle(clear, ClientRectangle);

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), Radius.Lg);
        using var brush = new SolidBrush(BackColor);
        e.Graphics.FillPath(brush, path);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        ThemePalette p = AppTheme.Palette;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(p.Border);
        using var path = RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), Radius.Lg);
        e.Graphics.DrawPath(pen, path);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyTheme(AppTheme.Palette);
    }

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        int d = Math.Max(2, radius * 2);
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
