using logReader.UI.Theme;

namespace logReader.UI.Tests;

public class AppThemeApplyTests
{
    [Fact]
    public void Apply_on_simple_form_does_not_throw()
    {
        RunSta(() =>
        {
            AppTheme.Current = ThemeMode.Dark;
            using var form = new Form();
            var button = new Button { Name = "buttonProcess", Text = "Обработать" };
            var text = new TextBox { Name = "textBoxLog", Multiline = true };
            form.Controls.Add(button);
            form.Controls.Add(text);

            AppTheme.Apply(form);

            Assert.Equal(AppTheme.Palette.Primary, button.BackColor);
            Assert.Equal(AppTheme.Palette.ConsoleBg, text.BackColor);
        });
    }

    [Fact]
    public void Apply_light_mode_updates_form_canvas()
    {
        RunSta(() =>
        {
            AppTheme.Current = ThemeMode.Light;
            using var form = new Form();
            AppTheme.Apply(form);
            Assert.Equal(ThemePalette.Light.Canvas, form.BackColor);
        });
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
