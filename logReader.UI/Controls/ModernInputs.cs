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
    private void UpdateState()
    {
        BackColor = !Enabled ? AppTheme.DisabledSurface : HasError ? AppTheme.ErrorSoft : AppTheme.Surface;
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
        Margin = new Padding(0);
        // Native editing, popup, keyboard navigation and UI Automation stay in use.
    }

    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); UpdateState(); }
    private void UpdateState()
    {
        BackColor = !Enabled ? AppTheme.DisabledSurface : HasError ? AppTheme.ErrorSoft : AppTheme.Surface;
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
        BackColor = !Enabled ? AppTheme.DisabledSurface : HasError ? AppTheme.ErrorSoft : AppTheme.Surface;
        Parent?.Invalidate();
        Invalidate();
    }
}
