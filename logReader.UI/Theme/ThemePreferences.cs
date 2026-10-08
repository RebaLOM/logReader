namespace logReader.UI.Theme;

public static class ThemePreferences
{
    // v2: светлая по умолчанию для premium UI; старый ui-theme.txt с Dark не подхватываем.
    private const string FileName = "ui-theme-v2.txt";
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
                return ThemeMode.Light;

            string raw = File.ReadAllText(PreferencesPath).Trim();
            if (Enum.TryParse(raw, ignoreCase: true, out ThemeMode mode))
                return mode;
        }
        catch
        {
            // Повреждённый файл настроек — безопасный default.
        }

        return ThemeMode.Light;
    }

    public static void Save(ThemeMode mode)
    {
        Directory.CreateDirectory(PreferencesDirectory);
        File.WriteAllText(PreferencesPath, mode.ToString());
    }
}
