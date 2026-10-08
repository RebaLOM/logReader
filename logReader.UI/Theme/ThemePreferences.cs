namespace logReader.UI.Theme;

public static class ThemePreferences
{
    private const string FileName = "ui-theme.txt";
    private static string? _overrideDirectory;

    public static string PreferencesDirectory =>
        _overrideDirectory
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LOGER");

    public static string PreferencesPath => Path.Combine(PreferencesDirectory, FileName);

    // Для тестов: изолированный каталог вместо %LocalAppData%.
    public static void SetDirectoryOverride(string? directory) => _overrideDirectory = directory;

    public static ThemeMode Load()
    {
        try
        {
            if (!File.Exists(PreferencesPath))
                return ThemeMode.Dark;

            string raw = File.ReadAllText(PreferencesPath).Trim();
            if (Enum.TryParse(raw, ignoreCase: true, out ThemeMode mode))
                return mode;
        }
        catch
        {
            // Повреждённый файл настроек — безопасный default.
        }

        return ThemeMode.Dark;
    }

    public static void Save(ThemeMode mode)
    {
        Directory.CreateDirectory(PreferencesDirectory);
        File.WriteAllText(PreferencesPath, mode.ToString());
    }
}
