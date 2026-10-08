using System.Runtime.InteropServices;

namespace logReader.UI.Theme;

// Immersive dark mode для title bar (Win10 1809+ / Win11).
public static class ThemeNative
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    public static void ApplyTitleBar(IntPtr hwnd, ThemeMode mode)
    {
        if (hwnd == IntPtr.Zero)
            return;

        int useDark = mode == ThemeMode.Dark ? 1 : 0;
        if (DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref useDark, sizeof(int)) != 0)
            DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkModeBefore20H1, ref useDark, sizeof(int));
    }

    public static void ApplyTitleBar(Form form, ThemeMode mode)
    {
        if (form.IsHandleCreated)
            ApplyTitleBar(form.Handle, mode);
    }
}
