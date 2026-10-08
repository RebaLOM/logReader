using System.Reflection;
using System.Runtime.ExceptionServices;
using logReader.UI;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace logReader.UI.Tests;

internal static class UiThread
{
    internal static void Run(Action test)
    {
        // No disposable synchronization object: a timed-out background STA must
        // never crash the runner when it eventually completes.
        var completed = new TaskCompletionSource<ExceptionDispatchInfo?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            ExceptionDispatchInfo? failure = null;
            try
            {
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException, threadScope: true);
                using var context = new ApplicationContext();
                using var dispatcher = new Control();
                _ = dispatcher.Handle;
                dispatcher.BeginInvoke((Action)(() =>
                {
                    try
                    {
                        Assert.IsType<WindowsFormsSynchronizationContext>(SynchronizationContext.Current);
                        test();
                    }
                    catch (Exception ex)
                    {
                        failure = ExceptionDispatchInfo.Capture(ex);
                    }
                    finally
                    {
                        context.ExitThread();
                    }
                }));
                // A durable outer loop keeps the WinForms synchronization
                // context installed across nested DoEvents and awaits.
                Application.Run(context);
            }
            catch (Exception ex)
            {
                failure ??= ExceptionDispatchInfo.Capture(ex);
            }
            finally
            {
                completed.TrySetResult(failure);
            }
        }) { IsBackground = true, Name = "WinForms regression test" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!completed.Task.Wait(TimeSpan.FromSeconds(30)))
            throw new TimeoutException("WinForms test exceeded 30 seconds. The background STA will not hold the runner open.");
        completed.Task.Result?.Throw();
    }

    internal static void Show(Form form)
    {
        form.ShowInTaskbar = false;
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(20, 20);
        form.Show();
        Pump();
    }

    internal static void Pump()
    {
        // Exercises posted ItemCheck updates and native Load/Shown/layout events.
        for (int i = 0; i < 3; i++) Application.DoEvents();
    }

    internal static void Until(Func<bool> ready)
    {
        var timeout = System.Diagnostics.Stopwatch.StartNew();
        while (!ready())
        {
            if (timeout.Elapsed > TimeSpan.FromSeconds(10))
                throw new TimeoutException("The native UI did not finish its pending operation within 10 seconds.");
            Pump();
            Thread.Sleep(1);
        }
        Pump();
    }

    internal static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }

    internal static T Field<T>(object target, string name)
    {
        var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Missing regression seam: {target.GetType().Name}.{name}");
        return (T)field.GetValue(target)!;
    }

    internal static object? Invoke(object target, string name, params object[] args) =>
        (target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Missing regression seam: {target.GetType().Name}.{name}"))
        .Invoke(target, args);

    internal static void AssertFooterReachable(Form form, IButtonControl? action)
    {
        if (action is not Control control) return;
        Assert.True(control.Visible, $"{form.GetType().Name} action is hidden.");
        var bounds = form.RectangleToClient(control.RectangleToScreen(control.ClientRectangle));
        Assert.True(bounds.Width > 0 && bounds.Height > 0, $"{form.GetType().Name} action collapsed.");
        Assert.True(form.ClientRectangle.Contains(bounds), $"{form.GetType().Name} action is outside its viewport: {bounds}.");
    }
}

internal sealed class UiFixtures : IDisposable
{
    internal string Root { get; } = Path.Combine(Path.GetTempPath(), "logReader-ui-tests-" + Guid.NewGuid().ToString("N"));
    internal string LogFolder => Path.Combine(Root, "logs");
    internal string Trc => Path.Combine(LogFolder, "sample.trc");
    internal string Xlsx => Path.Combine(Root, "devices.xlsx");
    internal string Dbc => Path.Combine(Root, "devices.dbc");
    internal string Dbf => Path.Combine(Root, "devices.dbf");
    internal string Composites => Path.Combine(Root, "composites.xlsx");

    internal static DeviceFieldRow FieldRow => new(0, "Speed", "NUM", 9, 4, true, false, 1, 0, "rpm", null, null, null);
    internal static DbcSignal Signal => new() { Name = "Speed", StartBit = 9, Length = 4, IsLittleEndian = true, Max = 15, Unit = "rpm" };
    internal static DbcMessage Message => new() { Name = "Engine", Id = 0x123, IsExtended = false, Dlc = 8, Signals = [Signal] };
    internal static DeviceDefinition Definition => new() { DeviceId = "123", MessageName = "Engine", Extended = false, Dlc = 8, Rows = [FieldRow] };
    internal static CompositeSignal Composite(string name) => new() { Block = "Engine", Param = name, Pieces = [new("123", 0, 0, 8)] };
    internal static Device Device => new DynamicDevice("123", [new()
    {
        FieldIndex = 0, Header = "Speed", Type = "NUM", StartBit = 9, LengthBit = 4,
        IsLittleEndian = true, Scale = 1
    }]);

    internal UiFixtures()
    {
        Directory.CreateDirectory(LogFolder);
        File.WriteAllText(Trc, "; Start time: 01.01.2026 12:00:00\n1) 0.000 Rx 123 8 01 02 03 04 05 06 07 08\n");
        File.WriteAllText(Path.Combine(LogFolder, "sample.asc"), "date Thu Jan 01 12:00:00.000 2026\nbase hex timestamps absolute\n0.000000 1 123 Rx d 8 01 02 03 04 05 06 07 08\n");
        DeviceExcelFile.WriteAllDevices(Xlsx, [Definition]);
        DbcFile.Write(Dbc, [Message]);
        DbfFile.Write(Dbf, [Message]);
        CompositeExcelFile.WriteAll(Composites, [Composite("Temperature"), Composite("Pressure")]);
    }

    public void Dispose() => Directory.Delete(Root, recursive: true);
}
