namespace logReader.UI.Theme;

public static class UiScale
{
    /// <summary>Scale a 96-DPI painting metric once at the point of use.</summary>
    public static int Px(Control control, int value) =>
        (int)Math.Round(value * control.DeviceDpi / 96d);

    public const int SpaceXs = 4;
    public const int SpaceSm = 8;
    public const int SpaceMd = 12;
    public const int SpaceLg = 16;
    public const int SpaceXl = 20;
    public const int Space2Xl = 24;
    public const int Space3Xl = 32;
    public const int RadiusSm = 6;
    public const int RadiusMd = 8;
    public const int RadiusLg = 12;
}
