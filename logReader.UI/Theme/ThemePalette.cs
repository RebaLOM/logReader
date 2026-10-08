namespace logReader.UI.Theme;

// Семантические цвета одной темы; жёлтый — только Primary/акцент, не Warning.
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
        Canvas = Color.FromArgb(0x0C, 0x0D, 0x10),
        Surface = Color.FromArgb(0x16, 0x18, 0x1D),
        SurfaceSecondary = Color.FromArgb(0x12, 0x14, 0x1A),
        Elevated = Color.FromArgb(0x1E, 0x21, 0x28),
        Text = Color.FromArgb(0xF3, 0xF4, 0xF6),
        TextSecondary = Color.FromArgb(0xA1, 0xA8, 0xB3),
        Muted = Color.FromArgb(0x8B, 0x92, 0x9E),
        Border = Color.FromArgb(0x2A, 0x2E, 0x36),
        BorderHover = Color.FromArgb(0x3D, 0x44, 0x50),
        Primary = Color.FromArgb(0xFA, 0xCC, 0x15),
        PrimaryHover = Color.FromArgb(0xEA, 0xB3, 0x08),
        PrimaryPressed = Color.FromArgb(0xCA, 0x8A, 0x04),
        OnPrimary = Color.FromArgb(0x11, 0x18, 0x27),
        Success = Color.FromArgb(0x34, 0xD3, 0x99),
        Warning = Color.FromArgb(0xFB, 0x92, 0x3C),
        Error = Color.FromArgb(0xF8, 0x71, 0x71),
        Info = Color.FromArgb(0x60, 0xA5, 0xFA),
        Selection = Color.FromArgb(0x3F, 0x3A, 0x1E),
        FocusRing = Color.FromArgb(0xFA, 0xCC, 0x15),
        ConsoleBg = Color.FromArgb(0x0A, 0x0B, 0x0E),
        ConsoleFg = Color.FromArgb(0xE5, 0xE7, 0xEB),
        GridLine = Color.FromArgb(0x2A, 0x2E, 0x36),
        EmptyCell = Color.FromArgb(0x12, 0x14, 0x1A),
    };

    public static ThemePalette Light { get; } = new()
    {
        Canvas = Color.FromArgb(0xF3, 0xF4, 0xF6),
        Surface = Color.FromArgb(0xFF, 0xFF, 0xFF),
        SurfaceSecondary = Color.FromArgb(0xEB, 0xED, 0xF0),
        Elevated = Color.FromArgb(0xF0, 0xF1, 0xF4),
        Text = Color.FromArgb(0x11, 0x18, 0x27),
        TextSecondary = Color.FromArgb(0x4B, 0x55, 0x63),
        Muted = Color.FromArgb(0x6B, 0x72, 0x80),
        Border = Color.FromArgb(0xD1, 0xD5, 0xDB),
        BorderHover = Color.FromArgb(0x9C, 0xA3, 0xAF),
        Primary = Color.FromArgb(0xEA, 0xB3, 0x08),
        PrimaryHover = Color.FromArgb(0xCA, 0x8A, 0x04),
        PrimaryPressed = Color.FromArgb(0xA1, 0x62, 0x07),
        OnPrimary = Color.FromArgb(0x11, 0x18, 0x27),
        Success = Color.FromArgb(0x05, 0x96, 0x69),
        Warning = Color.FromArgb(0xC2, 0x41, 0x0C),
        Error = Color.FromArgb(0xB9, 0x1C, 0x1C),
        Info = Color.FromArgb(0x1D, 0x4E, 0xD8),
        Selection = Color.FromArgb(0xFE, 0xF3, 0xC7),
        FocusRing = Color.FromArgb(0xEA, 0xB3, 0x08),
        ConsoleBg = Color.FromArgb(0xEE, 0xF0, 0xF3),
        ConsoleFg = Color.FromArgb(0x11, 0x18, 0x27),
        GridLine = Color.FromArgb(0xD1, 0xD5, 0xDB),
        EmptyCell = Color.FromArgb(0xF0, 0xF1, 0xF4),
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
