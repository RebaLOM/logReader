namespace logReader.UI;

// One embedded LOGER 2 identity for the executable, main window and every dialog.
internal static class AppIcon
{
    internal const string ResourceName = "logReader.UI.Resources.loger2.ico";

    private static readonly Lazy<Icon> Resource = new(() =>
    {
        using Stream stream = typeof(AppIcon).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("The LOGER 2 application icon is missing.");
        using var icon = new Icon(stream);
        return (Icon)icon.Clone();
    });

    internal static Icon Create() => (Icon)Resource.Value.Clone();

    internal static void Apply(Form form)
    {
        var icon = Create();
        form.Icon = icon;
        form.ShowIcon = true;
        // Each form owns its clone; disposing a dialog cannot invalidate another window.
        form.Disposed += (_, _) => icon.Dispose();
    }
}
