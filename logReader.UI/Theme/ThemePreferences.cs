using System.Text.Json;

namespace logReader.UI.Theme;

/// <summary>A small, application-owned preference file; never stored in a checkout.</summary>
public static class ThemePreferences
{
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LOGER", "theme.json");
    public static string? LastError { get; private set; }

    public static ThemeMode Load(string? filePath = null)
    {
        LastError = null;
        try
        {
            string path = filePath ?? DefaultPath;
            if (!File.Exists(path)) return ThemeMode.Light;
            var info = new FileInfo(path);
            if (info.Length > 4096) return ThemeMode.Light;
            using var json = JsonDocument.Parse(File.ReadAllText(path));
            return json.RootElement.TryGetProperty("theme", out var theme) &&
                theme.ValueKind == JsonValueKind.String &&
                string.Equals(theme.GetString(), "dark", StringComparison.OrdinalIgnoreCase)
                ? ThemeMode.Dark : ThemeMode.Light;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or ArgumentException or NotSupportedException)
        {
            LastError = ex.Message;
            return ThemeMode.Light;
        }
    }

    public static bool Save(ThemeMode mode, string? filePath = null)
    {
        _ = ThemePalette.For(mode);
        LastError = null;
        string? temporary = null;
        try
        {
            string path = Path.GetFullPath(filePath ?? DefaultPath);
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(new { theme = mode == ThemeMode.Dark ? "dark" : "light" }));
            File.Move(temporary, path, overwrite: true);
            temporary = null;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            LastError = ex.Message;
            return false;
        }
        finally
        {
            if (temporary != null)
            {
                try { File.Delete(temporary); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
        }
    }
}
