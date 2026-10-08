using logReader.UI.Theme;

namespace logReader.UI.Tests;

public class ThemePreferencesTests : IDisposable
{
    private readonly string _tempDir;

    public ThemePreferencesTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "LOGER-theme-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        ThemePreferences.SetDirectoryOverride(_tempDir);
    }

    public void Dispose()
    {
        ThemePreferences.SetDirectoryOverride(null);
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // temp cleanup best-effort
        }
    }

    [Fact]
    public void Load_defaults_to_Light_when_file_missing()
    {
        Assert.Equal(ThemeMode.Light, ThemePreferences.Load());
    }

    [Fact]
    public void Preferences_path_uses_v2_filename_so_legacy_Dark_is_not_sticky()
    {
        Assert.EndsWith("ui-theme-v2.txt", ThemePreferences.PreferencesPath, StringComparison.OrdinalIgnoreCase);
        // Старый ui-theme.txt с Dark не влияет на default.
        File.WriteAllText(Path.Combine(_tempDir, "ui-theme.txt"), "Dark");
        Assert.Equal(ThemeMode.Light, ThemePreferences.Load());
    }

    [Fact]
    public void Save_and_Load_round_trip()
    {
        ThemePreferences.Save(ThemeMode.Light);
        Assert.Equal(ThemeMode.Light, ThemePreferences.Load());

        ThemePreferences.Save(ThemeMode.Dark);
        Assert.Equal(ThemeMode.Dark, ThemePreferences.Load());
    }
}
