namespace logReader.UI.Theme;

// Подключение темы к форме: Apply + dark title bar.
public static class ThemeForm
{
    public static void Wire(Form form)
    {
        ArgumentNullException.ThrowIfNull(form);

        void Apply()
        {
            if (form.IsDisposed)
                return;
            AppTheme.Apply(form);
            ThemeNative.ApplyTitleBar(form, AppTheme.Current);
        }

        form.Load += (_, _) => Apply();
        form.Shown += (_, _) => Apply();

        EventHandler onChanged = (_, _) =>
        {
            if (!form.IsDisposed && form.IsHandleCreated)
                form.BeginInvoke(Apply);
        };
        AppTheme.Changed += onChanged;
        form.FormClosed += (_, _) => AppTheme.Changed -= onChanged;
    }
}
