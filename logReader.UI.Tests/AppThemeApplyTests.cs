using logReader.UI.Theme;

namespace logReader.UI.Tests;

public class AppThemeApplyTests
{
    private static readonly object UiGate = new();

    [Fact]
    public void Apply_on_simple_form_does_not_throw()
    {
        lock (UiGate)
        {
            RunSta(() =>
            {
                AppTheme.Current = ThemeMode.Dark;
                using var form = new Form();
                var button = new Button
                {
                    Name = "buttonProcess",
                    Text = "Обработать",
                    Tag = ThemeTags.Primary
                };
                var muted = new Button { Name = "buttonSecondary", Text = "Вторичная" };
                var text = new TextBox { Name = "textBoxLog", Multiline = true };
                form.Controls.Add(button);
                form.Controls.Add(muted);
                form.Controls.Add(text);

                AppTheme.Apply(form);

                Assert.Equal(AppTheme.Palette.Primary, button.BackColor);
                Assert.Equal(AppTheme.Palette.Elevated, muted.BackColor);
                Assert.Equal(AppTheme.Palette.ConsoleBg, text.BackColor);
            });
        }
    }

    [Fact]
    public void Apply_light_mode_updates_form_canvas()
    {
        lock (UiGate)
        {
            RunSta(() =>
            {
                AppTheme.Current = ThemeMode.Light;
                using var form = new Form();
                AppTheme.Apply(form);
                Assert.Equal(ThemePalette.Light.Canvas, form.BackColor);
            });
        }
    }

    [Fact]
    public void Apply_light_then_dark_round_trip_updates_canvas()
    {
        lock (UiGate)
        {
            RunSta(() =>
            {
                using var form = new Form();
                AppTheme.Current = ThemeMode.Light;
                AppTheme.Apply(form);
                Assert.Equal(ThemePalette.Light.Canvas, form.BackColor);

                AppTheme.Current = ThemeMode.Dark;
                AppTheme.Apply(form);
                Assert.Equal(ThemePalette.Dark.Canvas, form.BackColor);

                AppTheme.Current = ThemeMode.Light;
                AppTheme.Apply(form);
                Assert.Equal(ThemePalette.Light.Canvas, form.BackColor);
            });
        }
    }

    private static void RunSta(Action action)
    {
        Exception? caught = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                caught = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (caught != null)
            throw new Xunit.Sdk.XunitException(caught.ToString());
    }
}
