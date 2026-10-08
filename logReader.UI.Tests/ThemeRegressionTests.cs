using logReader.UI.Theme;

namespace logReader.UI.Tests;

public class ThemeRegressionTests
{
    private const double MinContrast = 4.5;

    public static IEnumerable<object[]> Modes()
    {
        yield return [ThemeMode.Dark];
        yield return [ThemeMode.Light];
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void Text_on_Canvas_meets_wcag_aa(ThemeMode mode)
    {
        ThemePalette p = ThemePalette.For(mode);
        Assert.True(ThemePalette.ContrastRatio(p.Text, p.Canvas) >= MinContrast);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void OnPrimary_on_Primary_meets_wcag_aa(ThemeMode mode)
    {
        ThemePalette p = ThemePalette.For(mode);
        Assert.True(ThemePalette.ContrastRatio(p.OnPrimary, p.Primary) >= MinContrast);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void Error_on_Canvas_meets_wcag_aa(ThemeMode mode)
    {
        ThemePalette p = ThemePalette.For(mode);
        Assert.True(ThemePalette.ContrastRatio(p.Error, p.Canvas) >= MinContrast);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void ConsoleFg_on_ConsoleBg_meets_wcag_aa(ThemeMode mode)
    {
        ThemePalette p = ThemePalette.For(mode);
        Assert.True(ThemePalette.ContrastRatio(p.ConsoleFg, p.ConsoleBg) >= MinContrast);
    }
}
