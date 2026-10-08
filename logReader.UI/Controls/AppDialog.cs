using logReader.UI.Theme;

namespace logReader.UI.Controls;

// Базовый диалог: тема + тёмный title bar.
public class AppDialog : Form
{
    public AppDialog()
    {
        Font = Typography.Body();
        StartPosition = FormStartPosition.CenterParent;
        ShowIcon = false;
        MaximizeBox = false;
        MinimizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        AutoScaleMode = AutoScaleMode.Dpi;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ThemeNative.ApplyTitleBar(this, AppTheme.Current);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        AppTheme.Apply(this);
        ThemeNative.ApplyTitleBar(this, AppTheme.Current);
    }
}
