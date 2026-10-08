namespace logReader.UI.Theme;

// Фабрики Font: WinForms владеет Control.Font и Dispose'ит его — нельзя шарить static-инстансы.
public static class Typography
{
    public const string UiFamily = "Segoe UI";
    public const string MonoFamily = "Consolas";

    public static Font Title() => new(UiFamily, 16f, FontStyle.Bold);
    public static Font Section() => new(UiFamily, 9.5f, FontStyle.Bold);
    public static Font Body() => new(UiFamily, 9f, FontStyle.Regular);
    public static Font Caption() => new(UiFamily, 8.5f, FontStyle.Regular);
    public static Font Button() => new(UiFamily, 9f, FontStyle.Regular);
    public static Font Mono() => new(MonoFamily, 9f, FontStyle.Regular);
}
