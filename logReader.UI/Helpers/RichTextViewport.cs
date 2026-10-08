using System.Runtime.InteropServices;

namespace logReader.UI.Helpers;

internal static class RichTextViewport
{
    private const int GetScrollPosition = 0x04DD;
    private const int SetScrollPosition = 0x04DE;

    internal static Point GetScroll(RichTextBox box)
    {
        var position = new NativePoint();
        if (box.IsHandleCreated) SendMessage(box.Handle, GetScrollPosition, IntPtr.Zero, ref position);
        return new Point(position.X, position.Y);
    }

    internal static void SetScroll(RichTextBox box, Point scroll)
    {
        var position = new NativePoint { X = scroll.X, Y = scroll.Y };
        if (box.IsHandleCreated) SendMessage(box.Handle, SetScrollPosition, IntPtr.Zero, ref position);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X; public int Y; }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr parameter, ref NativePoint position);
}
