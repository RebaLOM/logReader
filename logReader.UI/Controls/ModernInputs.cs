using System.ComponentModel;

namespace logReader.UI.Controls;

internal interface IInputValidation
{
    bool HasError { get; set; }
    string? ErrorMessage { get; set; }
}

public class ModernTextBox : TextBox, IInputValidation
{
    private bool hasError;
    private string? errorMessage;
    [DefaultValue(false)]
    public bool HasError { get => hasError; set { hasError = value; UpdateState(); } }
    [DefaultValue(null)]
    public string? ErrorMessage { get => errorMessage; set { errorMessage = value; AccessibleDescription = value; } }

    public ModernTextBox()
    {
        Font = Typography.Body;
        ForeColor = AppTheme.TextPrimary;
        BackColor = AppTheme.Surface;
        BorderStyle = BorderStyle.FixedSingle;
        Margin = new Padding(0);
        MinimumSize = new Size(0, 26);
    }

    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); UpdateState(); }
    protected override void OnReadOnlyChanged(EventArgs e) { base.OnReadOnlyChanged(e); UpdateState(); }
    private void UpdateState()
    {
        BackColor = AppTheme.InputSurface(this, ReadOnly);
        ForeColor = Enabled ? AppTheme.TextPrimary : AppTheme.TextMuted;
        Parent?.Invalidate();
        Invalidate();
    }
}

public class ModernComboBox : ComboBox, IInputValidation
{
    private bool hasError;
    private string? errorMessage;
    [DefaultValue(false)]
    public bool HasError { get => hasError; set { hasError = value; UpdateState(); } }
    [DefaultValue(null)]
    public string? ErrorMessage { get => errorMessage; set { errorMessage = value; AccessibleDescription = value; } }

    public ModernComboBox()
    {
        Font = Typography.Body;
        BackColor = AppTheme.Surface;
        ForeColor = AppTheme.TextPrimary;
        FlatStyle = FlatStyle.Flat;
        DrawMode = DrawMode.OwnerDrawFixed;
        UpdateItemHeight();
        Margin = new Padding(0);
        // Native editing, popup, keyboard navigation and UI Automation stay in use.
    }

    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); UpdateState(); }
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); ThemeNative.Apply(this); }
    protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); if (DrawMode != DrawMode.Normal) UpdateItemHeight(); }
    protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e); UpdateItemHeight(); }
    protected override void OnDropDown(EventArgs e) { ThemeNative.Apply(this); base.OnDropDown(e); }
    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        bool selected = (e.State & DrawItemState.Selected) != 0;
        Color background = selected ? AppTheme.PrimarySoft : AppTheme.InputSurface(this);
        Color foreground = Enabled ? AppTheme.TextPrimary : AppTheme.TextMuted;
        using var brush = new SolidBrush(background);
        e.Graphics.FillRectangle(brush, e.Bounds);
        string text = (e.Index >= 0 && e.Index < Items.Count ? GetItemText(Items[e.Index]) : Text) ?? string.Empty;
        Rectangle bounds = e.Bounds;
        int inset = UiScale.Px(this, 6);
        bounds.Inflate(-inset, 0);
        TextRenderer.DrawText(e.Graphics, text, Font, bounds, foreground,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        if ((e.State & DrawItemState.Focus) != 0 && Focused && ShowFocusCues)
            ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(e.Bounds, -1, -1), AppTheme.Primary, background);
        base.OnDrawItem(e);
    }
    private void UpdateItemHeight() => ItemHeight = Math.Max(Font.Height + UiScale.Px(this, 4), UiScale.Px(this, 24));
    private void UpdateState()
    {
        BackColor = AppTheme.InputSurface(this);
        ForeColor = Enabled ? AppTheme.TextPrimary : AppTheme.TextMuted;
        Parent?.Invalidate();
        Invalidate();
    }
}

public class ModernNumericUpDown : NumericUpDown, IInputValidation
{
    private bool hasError;
    private string? errorMessage;
    [DefaultValue(false)]
    public bool HasError { get => hasError; set { hasError = value; UpdateState(); } }
    [DefaultValue(null)]
    public string? ErrorMessage { get => errorMessage; set { errorMessage = value; AccessibleDescription = value; } }

    public ModernNumericUpDown()
    {
        Font = Typography.Body;
        BackColor = AppTheme.Surface;
        ForeColor = AppTheme.TextPrimary;
        BorderStyle = BorderStyle.FixedSingle;
        Margin = new Padding(0);
    }

    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); UpdateState(); }
    private void UpdateState()
    {
        BackColor = AppTheme.InputSurface(this, ReadOnly);
        ForeColor = Enabled ? AppTheme.TextPrimary : AppTheme.TextMuted;
        Parent?.Invalidate();
        Invalidate();
    }
}
