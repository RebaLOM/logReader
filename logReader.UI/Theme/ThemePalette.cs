namespace logReader.UI.Theme;

public enum ThemeMode { Light, Dark }

/// <summary>Semantic colours; signal identities deliberately stay stable in both modes.</summary>
public sealed record ThemePalette
{
    public required ThemeMode Mode { get; init; }
    public required Color Background { get; init; }
    public required Color Surface { get; init; }
    public required Color SurfaceSecondary { get; init; }
    public required Color Border { get; init; }
    public required Color BorderHover { get; init; }
    public required Color Primary { get; init; }
    public required Color PrimaryHover { get; init; }
    public required Color PrimaryPressed { get; init; }
    public required Color PrimarySoft { get; init; }
    public required Color TextPrimary { get; init; }
    public required Color TextSecondary { get; init; }
    public required Color TextMuted { get; init; }
    public required Color TextOnPrimary { get; init; }
    public required Color TextOnError { get; init; }
    public required Color Success { get; init; }
    public required Color Warning { get; init; }
    public required Color Error { get; init; }
    public required Color SuccessSoft { get; init; }
    public required Color WarningSoft { get; init; }
    public required Color ErrorSoft { get; init; }
    public required Color DisabledSurface { get; init; }
    public Color Info => Primary;
    public Color ReadOnlySurface => SurfaceSecondary;
    public Color Canvas => Surface;
    public Color PayloadEmpty => SurfaceSecondary;
    public Color PayloadConflict => Error;
    public IReadOnlyList<Color> PayloadColors => Signals;

    private static Color Hex(int rgb) => Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
    private static readonly IReadOnlyList<Color> Signals = Array.AsReadOnly(new[]
    {
        Hex(0x5B8DEF), Hex(0xF6993F), Hex(0x57BB8A), Hex(0xD66096),
        Hex(0xA876DC), Hex(0x3CAAC1), Hex(0xDCB04A), Hex(0x8C96A5),
        Hex(0x6FB780), Hex(0xC4785A), Hex(0x7891D2), Hex(0xAF82BE),
        Hex(0x64AFA0), Hex(0xD28C78), Hex(0x82A064), Hex(0xAA6E6E)
    });

    // Чёрно-жёлтая палитра (DESIGN.md): жёлтый только как акцент, статусы отдельно.
    public static ThemePalette Light { get; } = new()
    {
        Mode = ThemeMode.Light, Background = Hex(0xF5F5F4), Surface = Hex(0xFFFFFF),
        SurfaceSecondary = Hex(0xF0F0EE), Border = Hex(0xE2E2DE), BorderHover = Hex(0xA8A8A0),
        // Primary глубже canary: Info на PrimarySoft должен держать ≥4.5.
        Primary = Hex(0x8B6400), PrimaryHover = Hex(0x7A5800), PrimaryPressed = Hex(0x6B4C00), PrimarySoft = Hex(0xFFF6CC),
        TextPrimary = Hex(0x141414), TextSecondary = Hex(0x4A4A46), TextMuted = Hex(0x7A7A74),
        TextOnPrimary = Hex(0xFFFFFF), TextOnError = Hex(0xFFFFFF),
        Success = Hex(0x15803D), Warning = Hex(0xB45309), Error = Hex(0xB91C1C),
        SuccessSoft = Hex(0xF0FDF4), WarningSoft = Hex(0xFFFBEB), ErrorSoft = Hex(0xFEF2F2),
        DisabledSurface = Hex(0xEEEEEC)
    };

    public static ThemePalette Dark { get; } = new()
    {
        Mode = ThemeMode.Dark, Background = Hex(0x0A0A0A), Surface = Hex(0x141414),
        SurfaceSecondary = Hex(0x1C1C1C), Border = Hex(0x2E2E2A), BorderHover = Hex(0x5A5A52),
        Primary = Hex(0xFFD60A), PrimaryHover = Hex(0xFFE566), PrimaryPressed = Hex(0xE6C008), PrimarySoft = Hex(0x2A2410),
        TextPrimary = Hex(0xF2F2F0), TextSecondary = Hex(0xB8B8B0), TextMuted = Hex(0x8A8A82),
        TextOnPrimary = Hex(0x0A0A0A), TextOnError = Hex(0x2C080B),
        Success = Hex(0x4ADE80), Warning = Hex(0xFBBF24), Error = Hex(0xF87171),
        SuccessSoft = Hex(0x173A2A), WarningSoft = Hex(0x3E3015), ErrorSoft = Hex(0x3F2029),
        DisabledSurface = Hex(0x242422)
    };

    public static ThemePalette For(ThemeMode mode) => mode switch
    {
        ThemeMode.Light => Light,
        ThemeMode.Dark => Dark,
        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };

    internal Color TranslateBackground(Color value, ThemePalette target)
    {
        if (value.A != 255 || value.IsEmpty) return value;
        Color[] from = [Background, Surface, SurfaceSecondary, Border, BorderHover, Primary, PrimaryHover,
            PrimaryPressed, PrimarySoft, Success, Warning, Error, SuccessSoft, WarningSoft, ErrorSoft, DisabledSurface];
        Color[] to = [target.Background, target.Surface, target.SurfaceSecondary, target.Border, target.BorderHover, target.Primary,
            target.PrimaryHover, target.PrimaryPressed, target.PrimarySoft, target.Success, target.Warning, target.Error,
            target.SuccessSoft, target.WarningSoft, target.ErrorSoft, target.DisabledSurface];
        for (int i = 0; i < from.Length; i++) if (value.ToArgb() == from[i].ToArgb()) return to[i];
        return value;
    }

    internal Color TranslateForeground(Color value, ThemePalette target)
    {
        if (value.A != 255 || value.IsEmpty) return value;
        Color[] from = [TextPrimary, TextSecondary, TextMuted, Primary, PrimaryHover, PrimaryPressed, Success, Warning, Error, TextOnPrimary, TextOnError];
        Color[] to = [target.TextPrimary, target.TextSecondary, target.TextMuted, target.Primary, target.PrimaryHover,
            target.PrimaryPressed, target.Success, target.Warning, target.Error, target.TextOnPrimary, target.TextOnError];
        for (int i = 0; i < from.Length; i++) if (value.ToArgb() == from[i].ToArgb()) return to[i];
        return value;
    }
}
