using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace logReader.UI.Controls;

public enum ButtonVariant { Primary, Secondary, Ghost, Danger }

public class ModernButton : Button
{
    private ButtonVariant variant = ButtonVariant.Secondary;
    private IconKind icon;
    private bool hover;
    private bool pressed;
    private bool isDefault;
    private string? automaticName;

    [DefaultValue(ButtonVariant.Secondary)]
    public ButtonVariant Variant { get => variant; set { variant = value; Invalidate(); } }
    [DefaultValue(IconKind.None)]
    public IconKind Icon { get => icon; set { icon = value; Invalidate(); PerformLayout(); } }

    public ModernButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        BackColor = AppTheme.Surface;
        ForeColor = AppTheme.TextPrimary;
        Font = Typography.Button;
        Size = new Size(144, 40);
        MinimumSize = new Size(80, 36);
        Padding = new Padding(16, 0, 16, 0);
        Margin = new Padding(0, 0, 8, 0);
        Cursor = Cursors.Hand;
        AccessibleRole = AccessibleRole.PushButton;
    }

    public override void NotifyDefault(bool value) { isDefault = value; base.NotifyDefault(value); Invalidate(); }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space) { pressed = true; Invalidate(); } base.OnKeyDown(e); }
    protected override void OnKeyUp(KeyEventArgs e) { pressed = false; Invalidate(); base.OnKeyUp(e); }
    protected override void OnLostFocus(EventArgs e) { pressed = false; Invalidate(); base.OnLostFocus(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnTextChanged(EventArgs e)
    {
        if (string.IsNullOrEmpty(AccessibleName) || AccessibleName == automaticName)
        {
            automaticName = Text.Replace("&", "");
            AccessibleName = automaticName;
        }
        Invalidate();
        base.OnTextChanged(e);
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        int iconWidth = Icon == IconKind.None ? 0 : UiScale.Px(this, 26);
        return new Size(Math.Max(MinimumSize.Width, TextRenderer.MeasureText(Text, Font).Width + Padding.Horizontal + iconWidth),
            Math.Max(MinimumSize.Height, UiScale.Px(this, 40)));
    }

    protected override void OnPaintBackground(PaintEventArgs e) =>
        e.Graphics.Clear(PaintGeometry.SurfaceFor(this));

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Color fill = AppTheme.Surface;
        Color ink = AppTheme.TextPrimary;
        Color stroke = AppTheme.Border;
        bool primary = Variant == ButtonVariant.Primary;
        bool danger = Variant == ButtonVariant.Danger;
        if (primary)
        {
            fill = pressed ? AppTheme.PrimaryPressed : hover ? AppTheme.PrimaryHover : AppTheme.Primary;
            ink = Color.White;
            stroke = fill;
        }
        else if (danger)
        {
            fill = pressed ? AppTheme.Error : hover ? AppTheme.ErrorSoft : AppTheme.Surface;
            ink = pressed ? Color.White : AppTheme.Error;
            stroke = hover || pressed ? AppTheme.Error : AppTheme.Border;
        }
        else if (Variant == ButtonVariant.Ghost)
        {
            fill = pressed ? AppTheme.PrimarySoft : hover ? AppTheme.SurfaceSecondary : PaintGeometry.SurfaceFor(this);
            stroke = fill;
            ink = hover ? AppTheme.Primary : ForeColor;
        }
        else
        {
            fill = pressed ? AppTheme.PrimarySoft : hover ? AppTheme.SurfaceSecondary : AppTheme.Surface;
            stroke = hover ? AppTheme.BorderHover : AppTheme.Border;
        }
        if (!Enabled) { fill = AppTheme.DisabledSurface; ink = AppTheme.TextMuted; stroke = AppTheme.Border; }
        float inset = UiScale.Px(this, 1);
        var bounds = new RectangleF(inset, inset, Math.Max(1, Width - inset * 2 - 1), Math.Max(1, Height - inset * 2 - 1));
        using var path = PaintGeometry.Rounded(bounds, UiScale.Px(this, 8));
        using var background = new SolidBrush(fill);
        using var border = new Pen(stroke, Math.Max(1f, DeviceDpi / 96f));
        e.Graphics.FillPath(background, path);
        e.Graphics.DrawPath(border, path);
        if (Enabled && ((Focused && ShowFocusCues) || isDefault))
        {
            using var focus = new Pen(AppTheme.Primary, Math.Max(1f, DeviceDpi / 96f));
            using var focusPath = PaintGeometry.Rounded(new RectangleF(0.5f, 0.5f, Math.Max(1, Width - 2), Math.Max(1, Height - 2)), UiScale.Px(this, 9));
            e.Graphics.DrawPath(focus, focusPath);
        }
        var content = Rectangle.FromLTRB(Padding.Left, 0, Width - Padding.Right, Height);
        int iconSize = UiScale.Px(this, 18);
        int gap = UiScale.Px(this, 8);
        bool hasIcon = Icon != IconKind.None;
        int textWidth = TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.NoPadding).Width;
        int total = textWidth + (hasIcon ? iconSize + (Text.Length > 0 ? gap : 0) : 0);
        int x = TextAlign is ContentAlignment.MiddleLeft or ContentAlignment.TopLeft or ContentAlignment.BottomLeft
            ? content.Left : content.Left + Math.Max(0, (content.Width - total) / 2);
        if (hasIcon)
        {
            IconPainter.Draw(e.Graphics, Icon, new Rectangle(x, (Height - iconSize) / 2, iconSize, iconSize), ink);
            x += iconSize + gap;
        }
        TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(x, 0, Math.Max(1, content.Right - x), Height), ink,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
            (ShowKeyboardCues ? TextFormatFlags.Default : TextFormatFlags.HidePrefix));
    }
}
