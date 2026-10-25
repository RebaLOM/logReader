using System.Reflection;
using System.Runtime.ExceptionServices;

namespace logReader.UI.Tests;

[Collection("DTC UI")]
public sealed class AppIconTests
{
    [Fact]
    public void Embedded_icon_contains_every_Windows_size()
    {
        using Stream stream = typeof(AppIcon).Assembly.GetManifestResourceStream(AppIcon.ResourceName)!;
        Assert.NotNull(stream);
        using var reader = new BinaryReader(stream);
        Assert.Equal(0, reader.ReadUInt16());
        Assert.Equal(1, reader.ReadUInt16());
        int count = reader.ReadUInt16();
        var sizes = new List<int>();
        for (int index = 0; index < count; index++)
        {
            int width = reader.ReadByte();
            int height = reader.ReadByte();
            Assert.Equal(width, height);
            sizes.Add(width == 0 ? 256 : width);
            stream.Seek(14, SeekOrigin.Current);
        }
        Assert.Equal(new[] { 16, 20, 24, 32, 48, 64, 128, 256 }, sizes);
    }

    [Fact]
    public void Every_application_window_gets_the_embedded_icon_without_an_open_main_window() => Run(() =>
    {
        string directory = Path.Combine(Path.GetTempPath(), "loger-icon-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string devices = Path.Combine(directory, "devices.xlsx");
        string composites = Path.Combine(directory, "composites.xlsx");
        DeviceExcelFile.CreateDevicesExcelTemplate(devices);
        CompositeExcelFile.CreateTemplate(composites);
        Func<Form>[] factories =
        [
            () => new MainForm(),
            () => new HelpForm(),
            () => new DisplaySettingsForm(new DisplaySettings()),
            () => new CanLogViewForm("fixture.trc"),
            () => new DtcDiagnosticsForm("fixture.trc", analyze: (_, _, _) => new([], [], [], [])),
            () => new SaveOptionsForm(OutputFormat.Csv, BatchOutputMode.PerInputFile,
                new DstConnectOptions(), null, LogFormatKind.All),
            () => new FileKindPromptForm(),
            () => new FormatConversionDialog([new("trc_to_asc", "TRC → ASC", ".trc", ".asc")], "", _ => { }),
            () => new Devices_ParametrsForm([], new(), new()),
            () => new DevicesEditorForm(devices),
            () => new CompositeEditorForm(composites),
            () => new CompositeParamEditForm(null),
            () => new DbcMessageEditForm(null),
            () => new DbcSignalEditForm(null, 8),
            () => new XlsxMessageEditForm(null),
            () => new DeviceFieldRowEditForm(null, 8, 0)
        ];
        var covered = new HashSet<Type>();
        try
        {
            foreach (Func<Form> create in factories)
            {
                using Form form = create();
                covered.Add(form.GetType());
                Assert.True(form.ShowIcon, form.GetType().Name);
                AssertIcon(form.Icon);
                bool taskbar = form.ShowInTaskbar;
                ThemeManager.Attach(form); // Attaching again must retain ownership and taskbar behavior.
                Assert.Equal(taskbar, form.ShowInTaskbar);
                AssertIcon(form.Icon);
            }
            Type[] declaredForms = typeof(MainForm).Assembly.GetTypes()
                .Where(type => !type.IsAbstract && typeof(Form).IsAssignableFrom(type)).ToArray();
            Assert.All(declaredForms, type => Assert.Contains(type, covered));
        }
        finally
        {
            File.Delete(devices);
            File.Delete(composites);
            Directory.Delete(directory);
        }
    });

    [Fact]
    public void Themed_confirmation_uses_application_icon_and_stays_out_of_taskbar() => Run(() =>
    {
        using var owner = new Form();
        ThemeManager.Attach(owner);
        ExceptionDispatchInfo? failure = null;
        using var timer = new System.Windows.Forms.Timer { Interval = 20 };
        timer.Tick += (_, _) =>
        {
            Form? dialog = Application.OpenForms.Cast<Form>().FirstOrDefault(form => form.Text == "Icon verification");
            if (dialog == null) return;
            timer.Stop();
            try
            {
                Assert.True(dialog.ShowIcon);
                Assert.False(dialog.ShowInTaskbar);
                AssertIcon(dialog.Icon);
            }
            catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
            dialog.DialogResult = DialogResult.OK;
            dialog.Close();
        };
        timer.Start();
        Assert.Equal(DialogResult.OK, ThemedMessageBox.Show(owner, "Icon verification", "Icon verification"));
        failure?.Throw();
    });

    [Fact]
    public void Executable_contains_the_same_icon_as_its_windows()
    {
        string executable = Path.ChangeExtension(typeof(MainForm).Assembly.Location, ".exe");
        Assert.True(File.Exists(executable), executable);
        using Icon? extracted = Icon.ExtractAssociatedIcon(executable);
        AssertIcon(extracted);
    }

    [Fact]
    public void Disposing_one_window_keeps_other_windows_icons_valid() => Run(() =>
    {
        using var first = new Form();
        using var second = new Form { ShowInTaskbar = false };
        ThemeManager.Attach(first);
        ThemeManager.Attach(second);
        Assert.NotSame(first.Icon, second.Icon);
        first.Dispose();
        AssertIcon(second.Icon);
        using Icon third = AppIcon.Create();
        AssertIcon(third);
    });

    private static void AssertIcon(Icon? actual)
    {
        Assert.NotNull(actual);
        using Icon expected = AppIcon.Create();
        using Bitmap expectedImage = expected.ToBitmap();
        using Bitmap actualImage = actual.ToBitmap();
        Assert.Equal(expectedImage.Size, actualImage.Size);
        for (int y = 0; y < expectedImage.Height; y++)
            for (int x = 0; x < expectedImage.Width; x++)
                Assert.Equal(expectedImage.GetPixel(x, y), actualImage.GetPixel(x, y));
    }

    private static void Run(Action action)
    {
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Icon verification timed out.");
        failure?.Throw();
    }
}
