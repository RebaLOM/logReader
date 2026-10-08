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
    public void Load_defaults_to_Dark_when_file_missing()
    {
        Assert.Equal(ThemeMode.Dark, ThemePreferences.Load());
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
