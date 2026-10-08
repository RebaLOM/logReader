using System.Reflection;
using System.Runtime.ExceptionServices;
using logReader.UI.Controls;

namespace logReader.UI.Tests;

public class MainOperationRegressionTests
{
    [Fact]
    public void Main_operation_reports_progress_rejects_reentry_and_recovers_after_cancellation() => UiThread.Run(() =>
    {
        using var form = new MainForm();
        UiThread.Show(form);
        using var entered = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        ProcessingContext? workerContext = null;
        CancellationToken workerToken = default;
        var operation = StartOperation(form, (context, token) =>
        {
            workerContext = context;
            workerToken = token;
            context.Slice(0, 1, "Чтение тестового лога").ReportProgress(.4);
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("Main operation barrier was not released.");
            token.ThrowIfCancellationRequested();
            return ProcessingResult.Ok("fixture.csv", 1);
        });
        var progress = UiThread.Field<ProgressBar>(form, "progressBarProcess");
        var cancel = UiThread.Field<Button>(form, "buttonCancel");
        try
        {
            UiThread.Until(() => entered.IsSet && progress.Value == 400);
            Assert.Equal("Чтение тестового лога", UiThread.Field<Label>(form, "labelProgress").Text);
            Assert.True(progress.Visible);
            Assert.True(cancel.Enabled);
            Assert.False(UiThread.Field<TextBox>(form, "textBoxCanLog").Enabled);
            Assert.False(UiThread.Field<Button>(form, "buttonProcess").Enabled);
            bool repeatedWorkRan = false;
            var rejected = StartOperation(form, (_, _) =>
            {
                repeatedWorkRan = true;
                return ProcessingResult.Ok("unexpected.csv", 1);
            });
            Assert.True(rejected.IsCompleted);
            Assert.Null(rejected.GetAwaiter().GetResult());
            Assert.False(repeatedWorkRan);

            cancel.PerformClick();
            Assert.True(workerToken.IsCancellationRequested);
            Assert.False(cancel.Enabled);
            Assert.False(operation.IsCompleted);
            Assert.False(UiThread.Field<TextBox>(form, "textBoxCanLog").Enabled);
        }
        finally
        {
            release.Set();
            UiThread.Until(() => operation.IsCompleted);
        }
        Assert.Null(operation.GetAwaiter().GetResult());
        Assert.Null(UiThread.Field<CancellationTokenSource?>(form, "_operation"));
        Assert.True(UiThread.Field<bool>(form, "_operationCancelled"));
        Assert.Contains("Операция отменена.", UiThread.Field<TextBox>(form, "textBoxLog").Text);
        Assert.True(UiThread.Field<TextBox>(form, "textBoxCanLog").Enabled);
        Assert.False(cancel.Visible);
        Assert.False(progress.Visible);

        // A callback posted by an old worker must not revive completed progress.
        workerContext!.ReportProgress(.9);
        UiThread.Pump();
        Assert.Equal(0, progress.Value);
        Assert.False(progress.Visible);
        var restarted = StartOperation(form, (_, _) => ProcessingResult.Ok("retry.csv", 2));
        UiThread.Until(() => restarted.IsCompleted);
        Assert.Equal("retry.csv", restarted.GetAwaiter().GetResult()!.OutputPath);
        Assert.False(UiThread.Field<bool>(form, "_operationCancelled"));
    });

    [Fact]
    public void Main_closes_when_work_finishes_before_the_close_confirmation_is_answered() => UiThread.Run(() =>
    {
        using var form = new MainForm();
        UiThread.Show(form);
        using var entered = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        var operation = StartOperation(form, (_, _) =>
        {
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("Close confirmation did not release its worker.");
            return ProcessingResult.Ok("fixture.csv", 1);
        });
        UiThread.Until(() => entered.IsSet);
        ExceptionDispatchInfo? failure = null;
        bool answered = false;
        using var responder = new System.Windows.Forms.Timer { Interval = 20 };
        responder.Tick += (_, _) =>
        {
            var dialog = Application.OpenForms.Cast<Form>()
                .FirstOrDefault(candidate => candidate != form && candidate.Text == "LOGER" && candidate.Modal);
            if (dialog == null) return;
            // Releasing work inside the native modal loop exercises the race:
            // await/finally completes while the user's answer is still pending.
            release.Set();
            if (!operation.IsCompleted) return;
            responder.Stop();
            try
            {
                Assert.True(operation.GetAwaiter().GetResult()!.Success);
                Assert.Null(UiThread.Field<CancellationTokenSource?>(form, "_operation"));
                UiThread.Descendants(dialog).OfType<ModernButton>()
                    .Single(button => button.DialogResult == DialogResult.Yes).PerformClick();
                answered = true;
            }
            catch (Exception ex)
            {
                failure = ExceptionDispatchInfo.Capture(ex);
                dialog.DialogResult = DialogResult.Cancel;
                dialog.Close();
            }
        };
        responder.Start();
        try { form.Close(); }
        finally
        {
            release.Set();
            UiThread.Until(() => operation.IsCompleted);
        }
        failure?.Throw();
        Assert.True(answered);
        Assert.True(form.IsDisposed);
        Assert.False(UiThread.Field<bool>(form, "_closeWhenIdle"));
    });

    private static Task<ProcessingResult?> StartOperation(MainForm form,
        Func<ProcessingContext, CancellationToken, ProcessingResult?> work)
    {
        var method = typeof(MainForm).GetMethod("RunBusyAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .MakeGenericMethod(typeof(ProcessingResult));
        return Assert.IsAssignableFrom<Task<ProcessingResult?>>(method.Invoke(form, ["Тестовая операция", work, false]));
    }
}
