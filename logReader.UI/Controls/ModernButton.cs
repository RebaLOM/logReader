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
    private string? measuredText;
    private Font? measuredFont;
    private Size measuredTextSize;
    private int measuredDpi;
    private bool measuredMnemonic;
    private PreferredSizeKey? preferredSizeKey;
    private Size preferredSize;

    private readonly record struct PreferredSizeKey(string Text, Font Font, Padding Padding, Size MinimumSize,
        Size MaximumSize, IconKind Icon, int Dpi, bool UseMnemonic);

    protected bool IsHovered => hover;
    protected bool IsPressed => pressed;

    [DefaultValue(ButtonVariant.Secondary)]
    public ButtonVariant Variant { get => variant; set { if (variant == value) return; variant = value; Invalidate(); } }
    [DefaultValue(IconKind.None)]
    public IconKind Icon
    {
        get => icon;
        set
        {
            if (icon == value) return;
            icon = value;
            RequestPreferredSizeLayout(nameof(Icon));
            Invalidate();
        }
    }

    public ModernButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        // ButtonBase is opaque by default, which skips OnPaintBackground. A rounded
        // owner-drawn button must repaint the surrounding surface on every frame.
        SetStyle(ControlStyles.Opaque, false);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        BackColor = AppTheme.Surface;
        ForeColor = AppTheme.TextPrimary;
        Font = Typography.Button;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Size = new Size(144, 40);
        MinimumSize = new Size(80, 36);
        Padding = new Padding(16, 0, 16, 0);
        Margin = new Padding(0, 0, 8, 0);
        Cursor = Cursors.Hand;
        AccessibleRole = AccessibleRole.PushButton;
    }

    public override void NotifyDefault(bool value)
    {
        bool changed = isDefault != value;
        isDefault = value;
        // Owner-drawn Button only invalidates here; retaining the base state keeps
        // native dialog activation and accessibility working.
        base.NotifyDefault(value);
        if (!changed) return;
        if (IsHandleCreated) AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
        Invalidate();
    }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space) { pressed = true; Invalidate(); } base.OnKeyDown(e); }
    protected override void OnKeyUp(KeyEventArgs e) { pressed = false; Invalidate(); base.OnKeyUp(e); }
    protected override void OnLostFocus(EventArgs e) { pressed = false; Invalidate(); base.OnLostFocus(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnEnabledChanged(EventArgs e) { if (!Enabled) { hover = false; pressed = false; } Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (Capture && (e.Button & MouseButtons.Left) != 0)
        {
            bool inside = ClientRectangle.Contains(e.Location);
            if (pressed != inside) { pressed = inside; Invalidate(); }
        }
        base.OnMouseMove(e);
    }
    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        if (!Capture && pressed) { pressed = false; Invalidate(); }
        base.OnMouseCaptureChanged(e);
    }
    protected override void OnChangeUICues(UICuesEventArgs e) { base.OnChangeUICues(e); Invalidate(); }
    protected override void OnTextChanged(EventArgs e)
    {
        if (string.IsNullOrEmpty(AccessibleName) || AccessibleName == automaticName)
        {
            automaticName = AccessibleButtonText();
            AccessibleName = automaticName;
        }
        InvalidateTextMetrics();
        Invalidate();
        base.OnTextChanged(e);
    }

    protected override void OnFontChanged(EventArgs e)
    {
        InvalidateTextMetrics();
        base.OnFontChanged(e);
        Invalidate();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        InvalidateTextMetrics();
        RequestPreferredSizeLayout(nameof(Font));
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        InvalidateTextMetrics();
        RequestPreferredSizeLayout(nameof(DeviceDpi));
        Invalidate();
    }

    private void RequestPreferredSizeLayout(string property)
    {
        if (!AutoSize) return;
        if (Parent is { } parent) parent.PerformLayout(this, property);
        else Size = GetPreferredSize(Size.Empty);
    }

    private void InvalidateTextMetrics()
    {
        measuredFont = null;
        preferredSizeKey = null;
    }

    private TextFormatFlags TextFlags => TextFormatFlags.SingleLine | TextFormatFlags.NoPadding |
        (!UseMnemonic ? TextFormatFlags.NoPrefix : ShowKeyboardCues ? TextFormatFlags.Default : TextFormatFlags.HidePrefix);

    private Size MeasureButtonText()
    {
        if (measuredText == Text && ReferenceEquals(measuredFont, Font) && measuredDpi == DeviceDpi && measuredMnemonic == UseMnemonic)
            return measuredTextSize;
        if (string.IsNullOrEmpty(Text)) measuredTextSize = Size.Empty;
        else if (IsHandleCreated)
        {
            using var graphics = CreateGraphics();
            measuredTextSize = TextRenderer.MeasureText(graphics, Text, Font, new Size(int.MaxValue, int.MaxValue), TextFlags);
        }
        else measuredTextSize = TextRenderer.MeasureText(Text, Font, new Size(int.MaxValue, int.MaxValue), TextFlags);
        measuredText = Text;
        measuredFont = Font;
        measuredDpi = DeviceDpi;
        measuredMnemonic = UseMnemonic;
        return measuredTextSize;
    }

    private string AccessibleButtonText()
    {
        if (!UseMnemonic) return Text;
        var name = new System.Text.StringBuilder(Text.Length);
        for (int i = 0; i < Text.Length; i++)
        {
            if (Text[i] != '&') name.Append(Text[i]);
            else if (i + 1 < Text.Length && Text[i + 1] == '&') { name.Append('&'); i++; }
        }
        return name.ToString();
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var key = new PreferredSizeKey(Text, Font, Padding, MinimumSize, MaximumSize, Icon, DeviceDpi, UseMnemonic);
        if (preferredSizeKey is { } cached && cached == key) return preferredSize;
        Size textSize = MeasureButtonText();
        int iconSize = Icon == IconKind.None ? 0 : UiScale.Px(this, 18);
        int gap = iconSize > 0 && Text.Length > 0 ? UiScale.Px(this, 8) : 0;
        int safePadding = UiScale.Px(this, 3) * 2;
        int width = Math.Max(MinimumSize.Width, textSize.Width + iconSize + gap + Padding.Horizontal + safePadding);
        int height = Math.Max(MinimumSize.Height, Math.Max(UiScale.Px(this, 40), Math.Max(textSize.Height, iconSize) + Padding.Vertical + safePadding));
        if (MaximumSize.Width > 0) width = Math.Min(width, MaximumSize.Width);
        if (MaximumSize.Height > 0) height = Math.Min(height, MaximumSize.Height);
        preferredSizeKey = key;
        preferredSize = new Size(width, height);
        return preferredSize;
    }

    protected override void OnPaintBackground(PaintEventArgs e) =>
        e.Graphics.Clear(PaintGeometry.SurfaceFor(this));

    protected virtual (Color Fill, Color Ink, Color Border) GetAppearance()
    {
        Color fill = AppTheme.Surface;
        Color ink = AppTheme.TextPrimary;
        Color stroke = AppTheme.Border;
        bool primary = Variant == ButtonVariant.Primary;
        bool danger = Variant == ButtonVariant.Danger;
        if (primary)
        {
            fill = pressed ? AppTheme.PrimaryPressed : hover ? AppTheme.PrimaryHover : AppTheme.Primary;
            ink = AppTheme.TextOnPrimary;
            stroke = fill;
        }
        else if (danger)
        {
            fill = pressed ? AppTheme.Error : hover ? AppTheme.ErrorSoft : AppTheme.Surface;
            ink = pressed ? AppTheme.TextOnError : AppTheme.Error;
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
        return (fill, ink, stroke);
    }

    protected virtual void PaintAdornment(Graphics graphics, RectangleF bounds, Color ink) { }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var (fill, ink, stroke) = GetAppearance();
        bool focused = Enabled && Focused && ShowFocusCues;
        bool markedDefault = Enabled && isDefault;
        float strokeWidth = Math.Max(1f, DeviceDpi / 96f) * (focused || markedDefault ? 2f : 1f);
        if (focused) stroke = Variant == ButtonVariant.Primary ? AppTheme.TextOnPrimary : AppTheme.Primary;
        else if (markedDefault) stroke = Variant == ButtonVariant.Primary ? AppTheme.PrimaryHover : AppTheme.Primary;
        var bounds = PaintGeometry.StrokeBounds(ClientRectangle, strokeWidth, .5f);
        if (bounds.IsEmpty) return;
        using var path = PaintGeometry.Rounded(bounds, UiScale.Px(this, 8));
        using var background = new SolidBrush(fill);
        using var border = new Pen(stroke, strokeWidth);
        e.Graphics.FillPath(background, path);
        e.Graphics.DrawPath(border, path);
        PaintAdornment(e.Graphics, bounds, ink);
        PaintContent(e.Graphics, ink);
    }

    private void PaintContent(Graphics graphics, Color ink)
    {
        int safe = UiScale.Px(this, 3);
        var content = new Rectangle(Padding.Left + safe, Padding.Top + safe,
            Math.Max(0, ClientSize.Width - Padding.Horizontal - 2 * safe),
            Math.Max(0, ClientSize.Height - Padding.Vertical - 2 * safe));
        if (content.Width <= 0 || content.Height <= 0) return;
        bool hasIcon = Icon != IconKind.None;
        int iconSize = hasIcon ? Math.Min(UiScale.Px(this, 18), Math.Min(content.Width, content.Height)) : 0;
        int gap = hasIcon && Text.Length > 0 ? Math.Min(UiScale.Px(this, 8), Math.Max(0, content.Width - iconSize)) : 0;
        int textWidth = Math.Min(MeasureButtonText().Width, Math.Max(0, content.Width - iconSize - gap));
        int total = textWidth + iconSize + gap;
        bool rightToLeft = RightToLeft == RightToLeft.Yes;
        bool alignLeft = TextAlign is ContentAlignment.TopLeft or ContentAlignment.MiddleLeft or ContentAlignment.BottomLeft;
        bool alignRight = TextAlign is ContentAlignment.TopRight or ContentAlignment.MiddleRight or ContentAlignment.BottomRight;
        if (rightToLeft) (alignLeft, alignRight) = (alignRight, alignLeft);
        int x = alignLeft ? content.Left : alignRight ? content.Right - total : content.Left + (content.Width - total) / 2;
        bool alignTop = TextAlign is ContentAlignment.TopLeft or ContentAlignment.TopCenter or ContentAlignment.TopRight;
        bool alignBottom = TextAlign is ContentAlignment.BottomLeft or ContentAlignment.BottomCenter or ContentAlignment.BottomRight;
        var state = graphics.Save();
        try
        {
            graphics.SetClip(content, CombineMode.Intersect);
            if (hasIcon)
            {
                int iconX = rightToLeft ? x + textWidth + gap : x;
                int iconY = alignTop ? content.Top : alignBottom ? content.Bottom - iconSize : content.Top + (content.Height - iconSize) / 2;
                IconPainter.Draw(graphics, Icon, new Rectangle(iconX, iconY, iconSize, iconSize), ink);
            }
            if (textWidth > 0)
            {
                int textX = !rightToLeft && hasIcon ? x + iconSize + gap : x;
                TextFormatFlags alignment = alignTop ? TextFormatFlags.Top : alignBottom ? TextFormatFlags.Bottom : TextFormatFlags.VerticalCenter;
                if (rightToLeft) alignment |= TextFormatFlags.Right | TextFormatFlags.RightToLeft;
                TextRenderer.DrawText(graphics, Text, Font, new Rectangle(textX, content.Top, textWidth, content.Height), ink,
                    TextFlags | alignment | TextFormatFlags.EndEllipsis | TextFormatFlags.PreserveGraphicsClipping);
            }
        }
        finally { graphics.Restore(state); }
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new ModernButtonAccessibleObject(this);

    protected class ModernButtonAccessibleObject(ModernButton owner) : ButtonBaseAccessibleObject(owner)
    {
        public override AccessibleStates State => base.State | (owner.isDefault ? AccessibleStates.Default : AccessibleStates.None);
        public override string? Name
        {
            get => string.IsNullOrEmpty(owner.AccessibleName) || owner.AccessibleName == owner.automaticName
                ? owner.AccessibleButtonText() : base.Name;
            set => owner.AccessibleName = value;
        }
    }
}
