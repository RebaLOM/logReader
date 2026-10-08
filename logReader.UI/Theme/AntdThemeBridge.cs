namespace logReader.UI.Theme;

// Связка ThemePalette ↔ AntdUI без пересоздания деревьев контролов.
public static class AntdThemeBridge
{
    private static readonly object Gate = new();
    private static bool _configured;
    private static ThemeMode? _applied;

    public static void EnsureConfigured()
    {
        lock (Gate)
        {
            if (_configured)
                return;

            // Perf: без теней и глобальных анимаций.
            AntdUI.Config.Animation = false;
            AntdUI.Config.ShadowEnabled = false;

            ThemePalette light = ThemePalette.Light;
            ThemePalette dark = ThemePalette.Dark;

            AntdUI.Config.Theme()
                .Light(ToHex(light.Canvas), ToHex(light.Text))
                .Dark(ToHex(dark.Canvas), ToHex(dark.Text))
                .Header(ToHex(light.Surface), ToHex(dark.Surface));

            _configured = true;
        }
    }

    public static void Apply(ThemeMode mode)
    {
        // AntdUI Style/Config — UI-thread; из тестов/фона маршалим через OpenForms.
        if (Application.OpenForms.Count > 0)
        {
            Form? host = Application.OpenForms[0];
            if (host != null && host.IsHandleCreated && host.InvokeRequired)
            {
                host.BeginInvoke(() => Apply(mode));
                return;
            }
        }

        lock (Gate)
        {
            EnsureConfiguredUnlocked();
            if (_applied == mode)
                return;

            ThemePalette p = ThemePalette.For(mode);
            AntdUI.Style.SetPrimary(p.Primary);
            AntdUI.Style.SetSuccess(p.Success);
            AntdUI.Style.SetWarning(p.Warning);
            AntdUI.Style.SetError(p.Error);
            AntdUI.Style.SetInfo(p.Info);
            AntdUI.Config.Mode = mode == ThemeMode.Dark ? AntdUI.TMode.Dark : AntdUI.TMode.Light;
            _applied = mode;
        }
    }

    private static void EnsureConfiguredUnlocked()
    {
        if (_configured)
            return;

        AntdUI.Config.Animation = false;
        AntdUI.Config.ShadowEnabled = false;

        ThemePalette light = ThemePalette.Light;
        ThemePalette dark = ThemePalette.Dark;

        AntdUI.Config.Theme()
            .Light(ToHex(light.Canvas), ToHex(light.Text))
            .Dark(ToHex(dark.Canvas), ToHex(dark.Text))
            .Header(ToHex(light.Surface), ToHex(dark.Surface));

        _configured = true;
    }

    private static string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
}
