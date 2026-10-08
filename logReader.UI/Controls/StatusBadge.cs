using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace logReader.UI.Controls;

public enum StatusTone { Neutral, Info, Success, Warning, Error }

public class StatusBadge : Control
{
    private StatusTone tone;
    [DefaultValue(StatusTone.Neutral)]
    public StatusTone Tone { get => tone; set { tone = value; Invalidate(); } }
    public StatusBadge()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Font = Typography.Caption;
        Size = new Size(100, 28);
        AutoSize = true;
        TabStop = false;
        AccessibleRole = AccessibleRole.StaticText;
    }
    internal static (Color Ink, Color Fill) Colors(StatusTone value) => value switch
    {
        StatusTone.Info => (AppTheme.Info, AppTheme.PrimarySoft),
        StatusTone.Success => (AppTheme.Success, AppTheme.SuccessSoft),
        StatusTone.Warning => (AppTheme.Warning, AppTheme.WarningSoft),
        StatusTone.Error => (AppTheme.Error, AppTheme.ErrorSoft),
        _ => (AppTheme.TextSecondary, AppTheme.DisabledSurface)
    };
    public override Size GetPreferredSize(Size proposedSize) =>
        new(TextRenderer.MeasureText(Text, Font).Width + UiScale.Px(this, 22), UiScale.Px(this, 28));
    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); AccessibleName = Text; if (AutoSize) Size = GetPreferredSize(Size.Empty); Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var (ink, fill) = Colors(Tone);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = PaintGeometry.Rounded(new RectangleF(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1)), UiScale.Px(this, 6));
        using var background = new SolidBrush(fill);
        e.Graphics.FillPath(background, path);
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ink,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }
}
