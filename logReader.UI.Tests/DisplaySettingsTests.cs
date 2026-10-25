using System.Runtime.ExceptionServices;

namespace logReader.UI.Tests;

[Collection("DTC UI")]
public sealed class DisplaySettingsTests
{
    [Fact]
    public void Missing_settings_use_compact_automatic_defaults_without_creating_files()
    {
        using var directory = new SettingsDirectory();
        string path = Path.Combine(directory.Root, "nested", "display-settings.json");
        var settings = new DisplaySettingsStore(path).Load();

        Assert.Equal(DtcDisplayFormat.Compact, settings.DtcFormat);
        Assert.Equal(DtcInclusionMode.Auto, settings.DtcInclusion);
        Assert.False(Directory.Exists(Path.GetDirectoryName(path)));
    }

    [Theory]
    [InlineData(DtcDisplayFormat.Compact, 0)]
    [InlineData(DtcDisplayFormat.Detailed, 1)]
    [InlineData(DtcDisplayFormat.Detailed, 2)]
    public void User_choices_survive_a_new_store_instance(DtcDisplayFormat format, int mode)
    {
        using var directory = new SettingsDirectory();
        string path = Path.Combine(directory.Root, "nested", "display-settings.json");
        var expected = new DisplaySettings { DtcFormat = format, DtcInclusion = (DtcInclusionMode)mode };

        new DisplaySettingsStore(path).Save(expected);

        Assert.Equal(expected, new DisplaySettingsStore(path).Load());
        Assert.Contains(format.ToString(), File.ReadAllText(path));
        Assert.DoesNotContain("autoIncludeDtc", File.ReadAllText(path));
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(path)!));
    }

    [Theory]
    [InlineData("{}", DtcDisplayFormat.Compact, 0)]
    [InlineData("{\"autoIncludeDtc\":true}", DtcDisplayFormat.Compact, 0)]
    [InlineData("{\"autoIncludeDtc\":false}", DtcDisplayFormat.Compact, 2)]
    [InlineData("{\"dtcFormat\":\"Detailed\"}", DtcDisplayFormat.Detailed, 0)]
    [InlineData("{\"dtcFormat\":999,\"autoIncludeDtc\":false}", DtcDisplayFormat.Compact, 2)]
    [InlineData("{\"dtcInclusion\":\"Always\",\"autoIncludeDtc\":false}", DtcDisplayFormat.Compact, 1)]
    [InlineData("{\"DtcInclusion\":\"Never\",\"AutoIncludeDtc\":true}", DtcDisplayFormat.Compact, 2)]
    [InlineData("{\"dtcInclusion\":999}", DtcDisplayFormat.Compact, 0)]
    public void Missing_fields_and_unknown_numeric_format_have_defined_defaults(
        string json, DtcDisplayFormat expectedFormat, int expectedMode)
    {
        using var directory = new SettingsDirectory();
        string path = Path.Combine(directory.Root, "display-settings.json");
        File.WriteAllText(path, json);

        var settings = new DisplaySettingsStore(path).Load();

        Assert.Equal(expectedFormat, settings.DtcFormat);
        Assert.Equal((DtcInclusionMode)expectedMode, settings.DtcInclusion);
        Assert.Equal(json, File.ReadAllText(path));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{\"dtcFormat\":\"FutureFormat\"}")]
    public void Damaged_settings_fall_back_without_overwriting_the_file(string json)
    {
        using var directory = new SettingsDirectory();
        string path = Path.Combine(directory.Root, "display-settings.json");
        File.WriteAllText(path, json);

        Assert.Equal(new DisplaySettings(), new DisplaySettingsStore(path).Load());
        Assert.Equal(json, File.ReadAllText(path));
    }

    [Fact]
    public void Oversized_settings_are_ignored_without_rewriting_them()
    {
        using var directory = new SettingsDirectory();
        string path = Path.Combine(directory.Root, "display-settings.json");
        string json = "{\"dtcFormat\":\"Detailed\",\"autoIncludeDtc\":false,\"extra\":\"" + new string('x', 65536) + "\"}";
        File.WriteAllText(path, json);

        Assert.Equal(new DisplaySettings(), new DisplaySettingsStore(path).Load());
        Assert.Equal(json, File.ReadAllText(path));
    }

    [Fact]
    public void Failed_atomic_save_keeps_previous_choices_and_removes_temporary_files()
    {
        using var directory = new SettingsDirectory();
        string path = Path.Combine(directory.Root, "display-settings.json");
        var store = new DisplaySettingsStore(path);
        var previous = new DisplaySettings { DtcFormat = DtcDisplayFormat.Detailed, DtcInclusion = DtcInclusionMode.Never };
        store.Save(previous);
        byte[] original = File.ReadAllBytes(path);

        using (var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.Equal(new DisplaySettings(), store.Load());
            Assert.ThrowsAny<IOException>(() => store.Save(new DisplaySettings()));
        }

        Assert.Equal(original, File.ReadAllBytes(path));
        Assert.Equal(previous, store.Load());
        Assert.Single(Directory.GetFiles(directory.Root));
    }

    [Fact]
    public void Display_dialog_defaults_and_labels_match_the_user_choices() => Run(() =>
    {
        using var form = new DisplaySettingsForm(new DisplaySettings());
        form.Show();
        var format = Find<ComboBox>(form, "dtcDisplayFormat");

        Assert.Equal("Настройки отображения", form.Text);
        Assert.Equal(0, format.SelectedIndex);
        Assert.Equal("12345.0", format.Items[0]);
        Assert.Equal("SPN 12345 / FMI 0", format.Items[1]);
        Assert.Empty(Descendants(form).OfType<CheckBox>());
    });

    [Fact]
    public void Cancel_does_not_apply_edited_display_choices() => Run(() =>
    {
        var previous = new DisplaySettings { DtcFormat = DtcDisplayFormat.Detailed, DtcInclusion = DtcInclusionMode.Never };
        using var form = new DisplaySettingsForm(previous);
        form.Show();
        Find<ComboBox>(form, "dtcDisplayFormat").SelectedIndex = 0;

        Find<Button>(form, "displaySettingsCancel").PerformClick();

        Assert.Equal(previous, form.SelectedSettings);
    });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Changing_display_format_preserves_the_output_mode(int mode) => Run(() =>
    {
        var previous = new DisplaySettings { DtcInclusion = (DtcInclusionMode)mode };
        using var form = new DisplaySettingsForm(previous);
        form.Show();
        Find<ComboBox>(form, "dtcDisplayFormat").SelectedIndex = 1;
        Assert.Equal(previous, form.SelectedSettings);

        Find<Button>(form, "displaySettingsOk").PerformClick();

        Assert.Equal(previous with { DtcFormat = DtcDisplayFormat.Detailed }, form.SelectedSettings);
    });

    [Theory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public void Display_dialog_content_fits_the_client_area_and_uses_the_current_theme(string theme) => Run(() =>
    {
        ThemeMode previous = ThemeManager.Mode;
        try
        {
            ThemeManager.SetMode(Enum.Parse<ThemeMode>(theme), persist: false);
            using var form = new DisplaySettingsForm(new DisplaySettings());
            form.Show();
            form.PerformLayout();
            Application.DoEvents();

            foreach (Control control in Descendants(form).Where(control => control.Visible))
            {
                var bounds = form.RectangleToClient(control.RectangleToScreen(control.ClientRectangle));
                Assert.True(form.ClientRectangle.Contains(bounds), $"Clipped {control.GetType().Name} '{control.Text}': {bounds} outside {form.ClientRectangle}");
                if (control is Label { AutoSize: true } label)
                    Assert.True(label.Height >= label.PreferredSize.Height, $"Clipped label: {label.Text}");
            }
            Assert.Equal(ThemeManager.Current.Background, form.BackColor);
            Assert.Equal(ThemeManager.Current.Surface, Find<ComboBox>(form, "dtcDisplayFormat").BackColor);
            Assert.Equal(ThemeManager.Current.Accent, Find<Button>(form, "displaySettingsOk").BackColor);
        }
        finally { ThemeManager.SetMode(previous, persist: false); }
    });

    private static T Find<T>(Control root, string name) where T : Control
        => Assert.IsType<T>(Assert.Single(root.Controls.Find(name, true)));

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control control in root.Controls)
        {
            yield return control;
            foreach (var child in Descendants(control)) yield return child;
        }
    }

    private static void Run(Action action)
    {
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { failure = ExceptionDispatchInfo.Capture(ex); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(40)), "Display settings UI test timed out.");
        failure?.Throw();
    }

    private sealed class SettingsDirectory : IDisposable
    {
        internal string Root { get; } = Directory.CreateTempSubdirectory("loger-display-settings-").FullName;
        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
