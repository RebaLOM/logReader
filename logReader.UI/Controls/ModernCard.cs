using System.ComponentModel;
using logReader.UI.Theme;

namespace logReader.UI.Controls;

// Карточка-поверхность: border + padding, без теней (perf).
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
        // Полная заливка — иначе при ресайзе остаются «полоски» от предыдущих кадров.
        using var brush = new SolidBrush(BackColor);
        e.Graphics.FillRectangle(brush, ClientRectangle);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        ThemePalette p = AppTheme.Palette;
        using var pen = new Pen(p.Border);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyTheme(AppTheme.Palette);
    }
}
