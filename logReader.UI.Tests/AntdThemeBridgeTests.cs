using logReader.UI.Theme;

namespace logReader.UI.Tests;

public class AntdThemeBridgeTests
{
    [Fact]
    public void Apply_light_and_dark_does_not_throw()
    {
        AntdThemeBridge.EnsureConfigured();
        AntdThemeBridge.Apply(ThemeMode.Light);
        AntdThemeBridge.Apply(ThemeMode.Dark);
        AntdThemeBridge.Apply(ThemeMode.Light);
        Assert.Equal(AntdUI.TMode.Light, AntdUI.Config.Mode);
    }
}
