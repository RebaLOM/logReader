using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace logReader.UI.Theme;

/// <summary>Optional Windows chrome theming; content colours never depend on these APIs.</summary>
internal static class ThemeNative
{
    private static readonly ConditionalWeakTable<Control, NativeState> States = new();

    public static void Apply(Control control)
    {
        if (!OperatingSystem.IsWindows()) return;
        if (control is not (Form or TextBoxBase or ComboBox or UpDownBase or TreeView or ListView or ListBox or ScrollBar)) return;
        _ = States.GetValue(control, owner => new NativeState(owner));
        ApplyHandle(control);
    }

    private sealed class NativeState
    {
        public NativeState(Control owner) => owner.HandleCreated += (_, _) => ApplyHandle(owner);
    }

    private static void ApplyHandle(Control control)
    {
        if (!control.IsHandleCreated || control.IsDisposed || control.Disposing) return;
        bool dark = AppTheme.CurrentMode == ThemeMode.Dark;
        try
        {
            if (control is Form)
            {
                int enabled = dark ? 1 : 0;
                // Windows 11/current Windows 10, with the older Windows 10 identifier as fallback.
                if (DwmSetWindowAttribute(control.Handle, 20, ref enabled, sizeof(int)) != 0)
                    _ = DwmSetWindowAttribute(control.Handle, 19, ref enabled, sizeof(int));
            }
            else
            {
                _ = SetWindowTheme(control.Handle, dark ? "DarkMode_Explorer" : "Explorer", null);
                if (control is ComboBox)
                {
                    var info = new ComboBoxInfo { Size = Marshal.SizeOf<ComboBoxInfo>() };
                    if (GetComboBoxInfo(control.Handle, ref info))
                    {
                        if (info.List != IntPtr.Zero) _ = SetWindowTheme(info.List, dark ? "DarkMode_Explorer" : "Explorer", null);
                        if (info.Item != IntPtr.Zero) _ = SetWindowTheme(info.Item, dark ? "DarkMode_Explorer" : "Explorer", null);
                    }
                }
                else if (control is ListView)
                {
                    IntPtr header = SendMessage(control.Handle, 0x101F, IntPtr.Zero, IntPtr.Zero);
                    if (header != IntPtr.Zero) _ = SetWindowTheme(header, dark ? "DarkMode_ItemsView" : "Explorer", null);
                }
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct ComboBoxInfo
    {
        public int Size;
        public NativeRect ItemRect, ButtonRect;
        public uint ButtonState;
        public IntPtr Combo, Item, List;
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr window, string? subApplicationName, string? subIdList);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetComboBoxInfo(IntPtr window, ref ComboBoxInfo info);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}
