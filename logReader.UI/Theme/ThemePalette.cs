namespace logReader.UI.Theme;

// Семантические цвета одной темы; жёлтый — только Primary/акцент.
public sealed class ThemePalette
{
    public required Color Canvas { get; init; }
    public required Color Surface { get; init; }
    public required Color SurfaceSecondary { get; init; }
    public required Color Elevated { get; init; }
    public required Color Text { get; init; }
    public required Color TextSecondary { get; init; }
    public required Color Muted { get; init; }
    public required Color Border { get; init; }
    public required Color BorderHover { get; init; }
    public required Color Primary { get; init; }
    public required Color PrimaryHover { get; init; }
    public required Color PrimaryPressed { get; init; }
    public required Color OnPrimary { get; init; }
    public required Color Success { get; init; }
    public required Color Warning { get; init; }
    public required Color Error { get; init; }
    public required Color Info { get; init; }
    public required Color Selection { get; init; }
    public required Color FocusRing { get; init; }
    public required Color ConsoleBg { get; init; }
    public required Color ConsoleFg { get; init; }
    public required Color GridLine { get; init; }
    public required Color EmptyCell { get; init; }

    public static ThemePalette Dark { get; } = new()
    {
        Canvas = Color.FromArgb(0x0B, 0x0E, 0x11),
        Surface = Color.FromArgb(0x1E, 0x23, 0x29),
        SurfaceSecondary = Color.FromArgb(0x16, 0x1A, 0x1F),
        Elevated = Color.FromArgb(0x2B, 0x31, 0x39),
        Text = Color.FromArgb(0xEA, 0xEC, 0xEF),
        TextSecondary = Color.FromArgb(0xB7, 0xBD, 0xC8),
        Muted = Color.FromArgb(0x84, 0x8E, 0x9C),
        Border = Color.FromArgb(0x2B, 0x31, 0x39),
        BorderHover = Color.FromArgb(0x3A, 0x42, 0x4C),
        Primary = Color.FromArgb(0xFC, 0xD5, 0x35),
        PrimaryHover = Color.FromArgb(0xF0, 0xB9, 0x0B),
        PrimaryPressed = Color.FromArgb(0xD9, 0xA6, 0x0A),
        OnPrimary = Color.FromArgb(0x18, 0x1A, 0x20),
        Success = Color.FromArgb(0x0E, 0xCB, 0x81),
        Warning = Color.FromArgb(0xF0, 0xB9, 0x0B),
        Error = Color.FromArgb(0xF6, 0x46, 0x5D),
        Info = Color.FromArgb(0x3B, 0x82, 0xF6),
        Selection = Color.FromArgb(0x3A, 0x3A, 0x1F),
        FocusRing = Color.FromArgb(0xFC, 0xD5, 0x35),
        ConsoleBg = Color.FromArgb(0x0B, 0x0E, 0x11),
        ConsoleFg = Color.FromArgb(0xEA, 0xEC, 0xEF),
        GridLine = Color.FromArgb(0x3A, 0x42, 0x4C),
        EmptyCell = Color.FromArgb(0x16, 0x1A, 0x1F),
    };

    public static ThemePalette Light { get; } = new()
    {
        Canvas = Color.FromArgb(0xF7, 0xF8, 0xFA),
        Surface = Color.FromArgb(0xFF, 0xFF, 0xFF),
        SurfaceSecondary = Color.FromArgb(0xF0, 0xF2, 0xF5),
        Elevated = Color.FromArgb(0xEE, 0xF0, 0xF3),
        Text = Color.FromArgb(0x18, 0x1A, 0x20),
        TextSecondary = Color.FromArgb(0x4B, 0x55, 0x63),
        Muted = Color.FromArgb(0x70, 0x7A, 0x8A),
        Border = Color.FromArgb(0xE5, 0xE7, 0xEB),
        BorderHover = Color.FromArgb(0xD1, 0xD5, 0xDB),
        Primary = Color.FromArgb(0xF0, 0xB9, 0x0B),
        PrimaryHover = Color.FromArgb(0xD9, 0xA6, 0x0A),
        PrimaryPressed = Color.FromArgb(0xB4, 0x8A, 0x08),
        OnPrimary = Color.FromArgb(0x18, 0x1A, 0x20),
        Success = Color.FromArgb(0x0E, 0xCB, 0x81),
        Warning = Color.FromArgb(0xD9, 0x77, 0x06),
        Error = Color.FromArgb(0xDC, 0x26, 0x26),
        Info = Color.FromArgb(0x25, 0x63, 0xEB),
        Selection = Color.FromArgb(0xFF, 0xF6, 0xCC),
        FocusRing = Color.FromArgb(0xF0, 0xB9, 0x0B),
        ConsoleBg = Color.FromArgb(0x1E, 0x23, 0x29),
        ConsoleFg = Color.FromArgb(0xEA, 0xEC, 0xEF),
        GridLine = Color.FromArgb(0xC8, 0xCE, 0xD6),
        EmptyCell = Color.FromArgb(0xF0, 0xF2, 0xF5),
    };

    public static ThemePalette For(ThemeMode mode) => mode == ThemeMode.Light ? Light : Dark;

    public static double ContrastRatio(Color a, Color b)
    {
        double la = RelativeLuminance(a);
        double lb = RelativeLuminance(b);
        double lighter = Math.Max(la, lb);
        double darker = Math.Min(la, lb);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance(Color c)
    {
        static double Channel(byte v)
        {
            double s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
    }
}
