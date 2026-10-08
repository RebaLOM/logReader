namespace logReader.UI;

public partial class MainForm
{
    private ModernButton _themeButton = null!;

    private void BuildThemeSwitch(TableLayoutPanel sidebar)
    {
        _themeButton = new ModernButton
        {
            Name = nameof(_themeButton), Variant = ButtonVariant.Ghost,
            AutoSize = true, Dock = DockStyle.Top, Margin = new Padding(0, 12, 0, 0),
            AccessibleName = "Переключить тему оформления"
        };
        sidebar.RowCount = 6;
        sidebar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        sidebar.Controls.Add(_themeButton, 0, 5);
        _themeButton.Click += (_, _) =>
        {
            ThemeMode next = AppTheme.CurrentMode == ThemeMode.Dark ? ThemeMode.Light : ThemeMode.Dark;
            if (!AppTheme.SetMode(next))
                ShowWorkspaceNotice("Тема изменена. Не удалось сохранить выбор для следующего запуска.", StatusTone.Warning);
        };
        AppTheme.Changed += ThemeChanged;
        Disposed += (_, _) => AppTheme.Changed -= ThemeChanged;
        UpdateThemeSwitch();
    }

    private void ThemeChanged(object? sender, EventArgs e)
    {
        if (IsDisposed || Disposing) return;
        if (InvokeRequired)
        {
            if (IsHandleCreated) BeginInvoke((Action)UpdateThemeSwitch);
            return;
        }
        UpdateThemeSwitch();
    }

    private void UpdateThemeSwitch()
    {
        bool dark = AppTheme.CurrentMode == ThemeMode.Dark;
        _themeButton.Text = dark ? "Светлая тема" : "Тёмная тема";
        _themeButton.Icon = dark ? IconKind.Sun : IconKind.Moon;
        _themeButton.AccessibleDescription = dark
            ? "Включена тёмная тема. Переключить на светлую."
            : "Включена светлая тема. Переключить на тёмную.";
    }
}
