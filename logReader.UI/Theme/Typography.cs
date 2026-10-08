namespace logReader.UI.Theme;

/// <summary>Application-owned fonts are cached for the lifetime of the UI.</summary>
public static class Typography
{
    private static readonly string[] InstalledFamilies = ResolveFamilies();
    private static readonly string Family = InstalledFamilies.Contains("Segoe UI Variable Text") ? "Segoe UI Variable Text" : "Segoe UI";
    private static readonly string SemiboldFamily = InstalledFamilies.Contains("Segoe UI Semibold") ? "Segoe UI Semibold" : Family;
    public static Font PageTitle { get; } = Semibold(22);
    public static Font SectionTitle { get; } = Semibold(14);
    public static Font CardTitle { get; } = Semibold(11);
    public static Font Body { get; } = Create(10);
    public static Font Secondary { get; } = Create(9.5f);
    public static Font Caption { get; } = Create(9);
    public static Font Button { get; } = Semibold(10);
    public static Font Mono { get; } = new("Consolas", 9.5f, FontStyle.Regular, GraphicsUnit.Point);

    private static Font Create(float size, FontStyle style = FontStyle.Regular) =>
        new(Family, size, style, GraphicsUnit.Point);

    private static Font Semibold(float size) => new(SemiboldFamily, size,
        SemiboldFamily == Family ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Point);

    private static string[] ResolveFamilies()
    {
        using var installed = new System.Drawing.Text.InstalledFontCollection();
        var families = installed.Families;
        try { return families.Select(f => f.Name).ToArray(); }
        finally { foreach (var family in families) family.Dispose(); }
    }
}
