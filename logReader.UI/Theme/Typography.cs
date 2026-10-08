namespace logReader.UI.Theme;

// Фабрики Font: WinForms владеет Control.Font и Dispose'ит его — нельзя шарить static-инстансы.
public static class Typography
{
    // Segoe UI Variable на Win11; иначе Segoe UI.
    public static readonly string UiFamily =
        FontFamily.Families.Any(f => f.Name.Equals("Segoe UI Variable", StringComparison.OrdinalIgnoreCase))
            ? "Segoe UI Variable"
            : "Segoe UI";

    public const string MonoFamily = "Consolas";

    public static Font Brand() => new(UiFamily, 18f, FontStyle.Bold);
    public static Font PageTitle() => new(UiFamily, 16f, FontStyle.Bold);
    public static Font Title() => new(UiFamily, 14f, FontStyle.Bold);
    public static Font Section() => new(UiFamily, 11f, FontStyle.Bold);
    public static Font CardTitle() => new(UiFamily, 10.5f, FontStyle.Bold);
    public static Font Body() => new(UiFamily, 9.5f, FontStyle.Regular);
    public static Font Secondary() => new(UiFamily, 9f, FontStyle.Regular);
    public static Font Caption() => new(UiFamily, 8.5f, FontStyle.Regular);
    public static Font Button() => new(UiFamily, 9.5f, FontStyle.Bold);
    public static Font Mono() => new(MonoFamily, 9f, FontStyle.Regular);
}
