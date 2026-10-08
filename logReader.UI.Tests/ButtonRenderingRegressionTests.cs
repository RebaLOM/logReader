using logReader.UI.Controls;
using logReader.UI.Icons;
using logReader.UI.Theme;

namespace logReader.UI.Tests;

public class ButtonRenderingRegressionTests
{
    [Theory]
    [InlineData(ThemeMode.Light)]
    [InlineData(ThemeMode.Dark)]
    public void Rounded_buttons_clear_corners_and_keep_complete_default_focus_and_navigation_frames(ThemeMode mode) => UiThread.Run(() =>
    {
        var previous = AppTheme.CurrentMode;
        try
        {
            AppTheme.SetMode(mode, persist: false);
            using var host = new Form { ClientSize = new Size(420, 270), BackColor = AppTheme.SurfaceSecondary };
            var normal = new FocusVisibleButton { Text = "Проверка", Icon = IconKind.Save, Location = new Point(20, 20), Size = new Size(280, 52) };
            var navigation = new FocusVisibleNavigationItem { Text = "Инструменты", Icon = IconKind.Settings, Selected = true,
                Location = new Point(20, 90), Size = new Size(280, 52) };
            var otherFocus = new TextBox { Location = new Point(20, 180) };
            host.Controls.AddRange([normal, navigation, otherFocus]);
            UiThread.Show(host);
            otherFocus.Focus();
            UiThread.Pump();
            UiThread.Invoke(normal, "OnMouseLeave", EventArgs.Empty);
            UiThread.Invoke(navigation, "OnMouseLeave", EventArgs.Empty);
            Assert.Equal(host.DeviceDpi, normal.DeviceDpi);
            using (var bitmap = Capture(normal))
            {
                AssertCornersMatchParent(normal, bitmap);
                AssertCompleteFrame(bitmap, AppTheme.Border);
            }

            normal.NotifyDefault(true);
            using (var bitmap = Capture(normal))
            {
                AssertCornersMatchParent(normal, bitmap);
                AssertCompleteFrame(bitmap, AppTheme.Primary);
            }
            normal.NotifyDefault(false);
            normal.Focus();
            Assert.True(normal.Focused);
            using (var bitmap = Capture(normal))
            {
                AssertCornersMatchParent(normal, bitmap);
                AssertCompleteFrame(bitmap, AppTheme.Primary);
            }
            navigation.Focus();
            Assert.True(navigation.Focused);
            using (var bitmap = Capture(navigation))
            {
                AssertCornersMatchParent(navigation, bitmap);
                AssertCompleteFrame(bitmap, AppTheme.Primary);
            }
        }
        finally { AppTheme.SetMode(previous, persist: false); }
    });

    [Theory]
    [InlineData(10f)]
    [InlineData(14f)]
    [InlineData(18f)]
    public void Preferred_size_preserves_all_text_and_icon_pixels_at_native_dpi(float fontSize) => UiThread.Run(() =>
    {
        var previous = AppTheme.CurrentMode;
        try
        {
            AppTheme.SetMode(ThemeMode.Light, persist: false);
            using var host = new Form { ClientSize = new Size(1100, 400), BackColor = AppTheme.Background };
            using var font = new Font("Segoe UI", fontSize);
            const string text = "&Обработать && сохранить";
            var tight = new ModernButton { Text = text, Font = font, Icon = IconKind.Save, Variant = ButtonVariant.Primary,
                MinimumSize = Size.Empty, Location = new Point(12, 12) };
            var roomy = new ModernButton { Text = text, Font = font, Icon = IconKind.Save, Variant = ButtonVariant.Primary,
                MinimumSize = Size.Empty, Location = new Point(12, 130) };
            var otherFocus = new TextBox { Location = new Point(12, 260) };
            host.Controls.AddRange([tight, roomy, otherFocus]);
            UiThread.Show(host);
            tight.Size = tight.GetPreferredSize(Size.Empty);
            roomy.Size = new Size(tight.Width + 160, tight.Height);
            otherFocus.Focus();
            UiThread.Pump();
            UiThread.Invoke(tight, "OnMouseLeave", EventArgs.Empty);
            UiThread.Invoke(roomy, "OnMouseLeave", EventArgs.Empty);
            using var small = Capture(tight);
            using var large = Capture(roomy);
            int smallInk = CountContentInk(tight, small, AppTheme.TextOnPrimary);
            int largeInk = CountContentInk(roomy, large, AppTheme.TextOnPrimary);
            Assert.True(largeInk > 40, "The reference button must contain visible text and icon pixels.");
            Assert.InRange(Math.Abs(smallInk - largeInk), 0, Math.Max(3, largeInk / 100));
            Assert.Equal(host.DeviceDpi, tight.DeviceDpi);

            // Escaped ampersands and literal text have identical visible labels.
            tight.Text = "Save && &Close";
            tight.UseMnemonic = true;
            var mnemonic = tight.GetPreferredSize(Size.Empty);
            tight.Text = "Save & Close";
            tight.UseMnemonic = false;
            Assert.Equal(mnemonic, tight.GetPreferredSize(Size.Empty));
        }
        finally { AppTheme.SetMode(previous, persist: false); }
    });

    [Fact]
    public void Autosized_buttons_reflow_when_icons_fonts_and_labels_change() => UiThread.Run(() =>
    {
        using var host = new Form { ClientSize = new Size(900, 260) };
        var row = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false };
        var button = new ModernButton { Text = "Открыть выбранный результат", AutoSize = true, MinimumSize = Size.Empty };
        row.Controls.Add(button);
        host.Controls.Add(row);
        UiThread.Show(host);
        int initial = button.Width;
        button.Icon = IconKind.ExternalLink;
        UiThread.Pump();
        Assert.True(button.Width > initial);
        Assert.True(button.Width >= button.GetPreferredSize(Size.Empty).Width);
        using var largeFont = new Font(button.Font.FontFamily, 18f);
        button.Font = largeFont;
        UiThread.Pump();
        Assert.True(button.Height >= TextRenderer.MeasureText(button.Text, button.Font).Height);
        Assert.True(button.Width >= button.GetPreferredSize(Size.Empty).Width);
        int fullLabelWidth = button.Width;
        button.Text = "";
        UiThread.Pump();
        Assert.True(button.Width < fullLabelWidth);
        Assert.True(button.Width >= button.GetPreferredSize(Size.Empty).Width);
        button.AutoSize = false;
        button.Size = new Size(8, 8);
        using var tiny = Capture(button); // Constrained/tiny paints must remain safe.
    });

    private static Bitmap Capture(Control control)
    {
        var bitmap = new Bitmap(control.Width, control.Height);
        control.DrawToBitmap(bitmap, control.ClientRectangle);
        return bitmap;
    }

    private static void AssertCornersMatchParent(Control control, Bitmap bitmap)
    {
        Color surface = control.Parent!.BackColor;
        foreach (var point in new[] { new Point(0, 0), new Point(bitmap.Width - 1, 0),
            new Point(0, bitmap.Height - 1), new Point(bitmap.Width - 1, bitmap.Height - 1) })
            Assert.True(ColorDistance(surface, bitmap.GetPixel(point.X, point.Y)) <= 12,
                $"{control.GetType().Name}: corner {point} does not match its parent surface.");
    }

    private static void AssertCompleteFrame(Bitmap bitmap, Color expected)
    {
        var edges = new[]
        {
            new Rectangle(bitmap.Width / 2 - 2, 0, 5, Math.Min(5, bitmap.Height)),
            new Rectangle(bitmap.Width / 2 - 2, Math.Max(0, bitmap.Height - 5), 5, Math.Min(5, bitmap.Height)),
            new Rectangle(0, bitmap.Height / 2 - 2, Math.Min(5, bitmap.Width), 5),
            new Rectangle(Math.Max(0, bitmap.Width - 5), bitmap.Height / 2 - 2, Math.Min(5, bitmap.Width), 5)
        };
        foreach (var edge in edges)
            Assert.True(Pixels(edge).Any(point => ColorDistance(bitmap.GetPixel(point.X, point.Y), expected) <= 75),
                $"The rounded frame is missing on edge {edge}.");
    }

    private static int CountContentInk(Control control, Bitmap bitmap, Color ink)
    {
        var content = Rectangle.FromLTRB(control.Padding.Left, Math.Max(4, control.Height / 2 - control.Font.Height),
            control.Width - control.Padding.Right, Math.Min(control.Height - 4, control.Height / 2 + control.Font.Height));
        return Pixels(content).Count(point => ColorDistance(bitmap.GetPixel(point.X, point.Y), ink) <= 12);
    }

    private static IEnumerable<Point> Pixels(Rectangle bounds)
    {
        for (int y = bounds.Top; y < bounds.Bottom; y++)
            for (int x = bounds.Left; x < bounds.Right; x++) yield return new Point(x, y);
    }

    private static int ColorDistance(Color left, Color right) =>
        Math.Abs(left.R - right.R) + Math.Abs(left.G - right.G) + Math.Abs(left.B - right.B);

    private sealed class FocusVisibleButton : ModernButton { protected override bool ShowFocusCues => true; }
    private sealed class FocusVisibleNavigationItem : NavigationItem { protected override bool ShowFocusCues => true; }
}
