using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace logReader.UI.Tests;

public class ClassicWorkflowTests
{
    [Fact]
    public void Main_operation_rejects_reentry_cancels_and_ignores_completed_progress() => Run(() =>
    {
        using var form = new MainForm();
        form.Show();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        ProcessingContext? context = null;
        var operation = Start(form, (worker, token) =>
        {
            context = worker;
            worker.ReportProgress(.4);
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
            token.ThrowIfCancellationRequested();
            return ProcessingResult.Ok("unexpected.csv", 1);
        });
        try
        {
            Until(() => entered.IsSet && Field<ProgressBar>(form, "progressBarProcess").Value == 400);
            bool repeated = false;
            var rejected = Start(form, (_, _) => { repeated = true; return ProcessingResult.Ok("repeat.csv", 1); });
            Assert.Null(rejected.GetAwaiter().GetResult());
            Assert.False(repeated);
            Field<Button>(form, "buttonCancel").PerformClick();
            Assert.False(Field<Button>(form, "buttonCancel").Enabled);
        }
        finally
        {
            release.Set();
            Until(() => operation.IsCompleted);
        }
        Assert.Null(operation.GetAwaiter().GetResult());
        Assert.Null(Field<CancellationTokenSource?>(form, "_operation"));
        context!.ReportProgress(.9);
        Application.DoEvents();
        Assert.Equal(0, Field<ProgressBar>(form, "progressBarProcess").Value);
        var retry = Start(form, (_, _) => ProcessingResult.Ok("retry.csv", 2));
        Until(() => retry.IsCompleted);
        Assert.Equal("retry.csv", retry.GetAwaiter().GetResult()!.OutputPath);
    });

    [Fact]
    public void Main_closes_if_work_finishes_during_native_confirmation() => Run(() =>
    {
        using var form = new MainForm();
        form.Show();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var operation = Start(form, (_, _) =>
        {
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
            return ProcessingResult.Ok("fixture.csv", 1);
        });
        Until(() => entered.IsSet);
        bool answered = false;
        using var timer = new System.Windows.Forms.Timer { Interval = 20 };
        timer.Tick += (_, _) =>
        {
            var dialog = FindWindow("#32770", "LOGER");
            if (dialog == IntPtr.Zero) return;
            release.Set();
            if (!operation.IsCompleted) return;
            timer.Stop();
            answered = true;
            SendMessage(dialog, 0x0111, (IntPtr)6, IntPtr.Zero); // WM_COMMAND / IDYES
        };
        timer.Start();
        try { form.Close(); }
        finally { release.Set(); Until(() => operation.IsCompleted); }
        Assert.True(answered);
        Assert.True(form.IsDisposed);
    });

    [Fact]
    public void Conversion_rejects_reentry_and_cancellation_preserves_previous_output() => Run(() =>
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string input = Path.Combine(root, "log.trc");
            string output = Path.ChangeExtension(input, ".asc");
            File.WriteAllText(input, string.Concat(Enumerable.Repeat("1) 0.000 Rx 123 8 01 02 03 04 05 06 07 08\n", 8192)));
            File.WriteAllText(output, "previous output");
            using var entered = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            var messages = new ConcurrentQueue<string>();
            using var form = new FormatConversionDialog([new("trc_to_asc", "TRC -> ASC", ".trc", ".asc")], input, message =>
            {
                messages.Enqueue(message);
                if (!message.StartsWith("Предупреждение: не найдено стартовое время", StringComparison.Ordinal)) return;
                entered.Set();
                if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
            });
            form.Show();
            Field<Button>(form, "_convertButton").PerformClick();
            try
            {
                Until(() => entered.IsSet);
                typeof(FormatConversionDialog).GetMethod("convertButton_Click", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(form, [null, EventArgs.Empty]);
                form.Close();
                Assert.False(form.IsDisposed);
                Field<Button>(form, "_closeButton").PerformClick();
                Assert.True(Field<CancellationTokenSource>(form, "_conversionCancellation").IsCancellationRequested);
            }
            finally
            {
                release.Set();
                Until(() => !Field<bool>(form, "_busy"));
            }
            Assert.Equal("previous output", File.ReadAllText(output));
            Assert.False(Field<Button>(form, "_openButton").Enabled);
            Assert.Contains(messages, m => m.StartsWith("Преобразование остановлено", StringComparison.Ordinal));
            Assert.DoesNotContain(Directory.EnumerateFiles(root), p => Path.GetFileName(p).Contains(".tmp"));
        }
        finally { Directory.Delete(root, true); }
    });

    [Fact]
    public void Deselected_current_row_is_not_an_edit_target() => Run(() =>
    {
        using var grid = new DataGridView { SelectionMode = DataGridViewSelectionMode.FullRowSelect };
        grid.Columns.Add("Name", "Name");
        int index = grid.Rows.Add("signal");
        grid.Rows[index].Tag = 7;
        grid.CurrentCell = grid.Rows[index].Cells[0];
        grid.Rows[index].Selected = true;
        Assert.Equal(7, MessageEditFormHelpers.SelectedSourceIndex(grid));
        grid.ClearSelection();
        Assert.Equal(-1, MessageEditFormHelpers.SelectedSourceIndex(grid));
    });

    [Fact]
    public void Viewer_sums_packet_counts_without_int_overflow() => Run(() =>
    {
        using var form = new CanLogViewForm("unused.trc");
        typeof(CanLogViewForm).GetField("_packets", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(form, new List<(string, int)> { ("123", int.MaxValue), ("124", 1) });
        typeof(CanLogViewForm).GetMethod("ApplyFilter", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null);
        Assert.Contains(((long)int.MaxValue + 1).ToString("N0"), Field<Label>(form, "labelCount").Text);
    });

    private static Task<ProcessingResult?> Start(MainForm form, Func<ProcessingContext, CancellationToken, ProcessingResult?> work)
        => (Task<ProcessingResult?>)typeof(MainForm).GetMethod("RunBusyAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .MakeGenericMethod(typeof(ProcessingResult)).Invoke(form, ["Test", work])!;

    private static T Field<T>(object instance, string name)
        => (T)instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;

    private static void Until(Func<bool> predicate)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!predicate())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("UI operation did not complete.");
            Application.DoEvents();
            Thread.Sleep(10);
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
        Assert.True(thread.Join(TimeSpan.FromSeconds(40)), "UI test timed out.");
        failure?.Throw();
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string className, string title);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}
