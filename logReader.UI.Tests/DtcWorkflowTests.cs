using System.Reflection;
using System.Runtime.ExceptionServices;

namespace logReader.UI.Tests;

[Collection("DTC UI")]
public sealed class DtcWorkflowTests
{
    [Fact]
    public void Save_options_keep_Dtc_opt_in_and_round_trip_timeout() => Run(() =>
    {
        using var defaults = new SaveOptionsForm(OutputFormat.Csv, BatchOutputMode.PerInputFile,
            new DstConnectOptions(), null, LogFormatKind.All);
        Assert.False(defaults.IncludeDtc);
        Assert.Equal(2000, defaults.DtcTimeoutMs);
        var check = Field<CheckBox>(defaults, "_chkIncludeDtc");
        var timeout = Field<NumericUpDown>(defaults, "_numDtcTimeout");
        Assert.False(timeout.Enabled);
        check.Checked = true;
        Assert.True(timeout.Enabled);
        timeout.Value = 3.125M;
        Invoke(defaults, "buttonOk_Click", null, EventArgs.Empty);
        Assert.True(defaults.IncludeDtc);
        Assert.Equal(3125, defaults.DtcTimeoutMs);

        using var restored = new SaveOptionsForm(OutputFormat.Csv, BatchOutputMode.PerInputFile,
            new DstConnectOptions(), null, LogFormatKind.All, false, true, 3125);
        Assert.True(Field<CheckBox>(restored, "_chkIncludeDtc").Checked);
        Assert.Equal(3.125M, Field<NumericUpDown>(restored, "_numDtcTimeout").Value);
        Field<CheckBox>(restored, "_chkIncludeDtc").Checked = false;
        Invoke(restored, "buttonOk_Click", null, EventArgs.Empty);
        Assert.False(restored.IncludeDtc);
    });

    [Fact]
    public void Main_busy_state_disables_Dtc_diagnostics() => Run(() =>
    {
        using var form = new MainForm();
        var button = Field<Button>(form, "buttonDtcDiagnostics");
        Assert.Equal("Диагностика DTC", button.AccessibleName);
        Invoke(form, "SetBusy", true, "test");
        Assert.False(button.Enabled);
        Invoke(form, "SetBusy", false, "");
        Assert.True(button.Enabled);
    });

    [Fact]
    public void Diagnostics_reject_reentry_and_close_after_cancellation() => Run(() =>
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        int calls = 0;
        using var form = new DtcDiagnosticsForm("fixture.trc", analyze: (_, _, context) =>
        {
            Interlocked.Increment(ref calls);
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
            context.ThrowIfCancellationRequested();
            return new DtcAnalysisResult([], [], [], []);
        });
        form.Show();
        try
        {
            Until(() => entered.IsSet);
            Assert.False(Field<Button>(form, "_analyzeButton").Enabled);
            var duplicate = (Task)Invoke(form, "AnalyzeAsync")!;
            Assert.True(duplicate.IsCompleted);
            Assert.Equal(1, calls);
            form.Close();
            Assert.False(form.IsDisposed);
            Assert.True(Field<CancellationTokenSource>(form, "_operation").IsCancellationRequested);
            Assert.False(Field<Button>(form, "_cancelButton").Enabled);
        }
        finally
        {
            release.Set();
            Until(() => form.IsDisposed);
        }
    });

    [Fact]
    public void Diagnostics_virtualize_large_timeline_and_filter_codes_and_files() => Run(() =>
    {
        var code = new DtcCode(32770, 0);
        DtcSlot[] slots = [DtcSlot.Present(code), DtcSlot.Empty, DtcSlot.Empty, DtcSlot.Empty, DtcSlot.Empty, DtcSlot.Empty];
        var observations = Enumerable.Range(0, 35000).Select(i => new DtcObservation(
            i % 2 == 0 ? "a.trc" : "b.trc", i.ToString(), new DtcSnapshot(i, slots, 1, true, $"frame {i}"))).ToArray();
        var result = new DtcAnalysisResult(observations,
            [new("a.trc", code, "0", "34998", null, "Конец файла"), new("b.trc", code, "1", "34999", null, "Конец файла")],
            [new("a.trc", code, "0", "34998", 1, "Присутствует"), new("b.trc", code, "1", "34999", 1, "Присутствует")], []);
        using var form = new DtcDiagnosticsForm("fixtures", analyze: (_, _, _) => result);
        form.Show();
        Assert.True(form.ClientSize.Width >= 840, $"Diagnostics window is unexpectedly narrow: {form.ClientSize}");
        Assert.True(form.ClientSize.Height >= 580, $"Diagnostics window is unexpectedly short: {form.ClientSize}");
        Until(() => Field<CancellationTokenSource?>(form, "_operation") == null && Field<DtcAnalysisResult?>(form, "_result") != null);
        var summary = Field<DataGridView>(form, "_summaryGrid");
        var timeline = Field<DataGridView>(form, "_snapshotGrid");
        Assert.True(timeline.VirtualMode);
        Assert.Equal(2, summary.RowCount);
        Assert.Equal(17500, timeline.RowCount);
        Assert.Equal("SPN 32770 / FMI 0", timeline.Rows[0].Cells[4].Value);
        Assert.Equal("", timeline.Rows[0].Cells[5].Value);
        Field<ComboBox>(form, "_fileFilter").SelectedIndex = 2;
        Assert.Equal(1, summary.RowCount);
        Assert.Equal("b.trc", summary.Rows[0].Cells[0].Value);
        Assert.Equal("1", timeline.Rows[0].Cells[1].Value);
        var statesTab = (TabPage)timeline.Parent!.Parent!;
        ((TabControl)statesTab.Parent!).SelectedTab = statesTab;
        timeline.CurrentCell = timeline.Rows[0].Cells[1];
        Assert.Contains("frame 1", Field<TextBox>(form, "_rawData").Text);
        Field<TextBox>(form, "_search").Text = "99999";
        Assert.Equal(0, summary.RowCount);
        Assert.Equal(17500, timeline.RowCount);
        Field<TextBox>(form, "_search").Text = "32770 / 0";
        Assert.Equal(1, summary.RowCount);
    });

    [Theory]
    [InlineData("zero", "Диагностические посылки прочитаны; подтверждённых ошибок нет", 2, 1)]
    [InlineData("unknown", "Посылки прочитаны; коды не удалось восстановить", 1, 1)]
    [InlineData("none", "Диагностические снимки не найдены", 0, 0)]
    public void Diagnostics_distinguish_zero_count_unknown_slots_and_no_diagnostic_frames(
        string kind, string expectedStatus, int expectedSnapshots, int expectedTab) => Run(() =>
    {
        var snapshot = kind == "zero"
            ? new DtcSnapshot(1000, Enumerable.Repeat(DtcSlot.Empty, 6).ToArray(), 0, true,
                "18FE2503=20 00 62 00 42 20 00 00 | 18FE2603=00 00 00 00 00 00 00 00 | 18FE2703=00 00 00 00 00 00 00 00")
            : DtcSnapshot.Unknown(1000, "18FE2603=00 00 00 00 00 00 00 00");
        DtcObservation[] observations = kind switch
        {
            "none" => [],
            "zero" => [new("fixture.trc", "00:00:00.999", DtcSnapshot.Unknown(999, "18FE2503=20 00 62 00 42 20 00 00")),
                new("fixture.trc", "00:00:01.000", snapshot)],
            _ => [new("fixture.trc", "00:00:01.000", snapshot)]
        };
        var result = new DtcAnalysisResult(observations, [], [], []);
        using var form = new DtcDiagnosticsForm("fixture.trc", analyze: (_, _, _) => result);
        form.Show();
        Until(() => Field<CancellationTokenSource?>(form, "_operation") == null && Field<DtcAnalysisResult?>(form, "_result") != null);
        Assert.Equal(expectedStatus, Field<Label>(form, "_status").Text);
        Assert.Equal(0, Field<DataGridView>(form, "_summaryGrid").RowCount);
        Assert.Equal(expectedSnapshots, Field<DataGridView>(form, "_snapshotGrid").RowCount);
        Assert.Equal(expectedTab, Field<TabControl>(form, "_detailsTabs").SelectedIndex);
        if (expectedSnapshots != 0)
            Assert.Contains(snapshot.RawData, Field<TextBox>(form, "_rawData").Text);
        if (kind == "zero")
            Assert.Equal(1, Field<DataGridView>(form, "_snapshotGrid").CurrentCell!.RowIndex);
        if (kind == "unknown")
            Assert.DoesNotContain("ошибок нет", Field<Label>(form, "_status").Text);
    });

    [Fact]
    public void Folder_file_filter_opens_zero_count_snapshots_but_search_miss_keeps_current_tab() => Run(() =>
    {
        var code = new DtcCode(32770, 0);
        DtcSlot[] slots = [DtcSlot.Present(code), DtcSlot.Empty, DtcSlot.Empty, DtcSlot.Empty, DtcSlot.Empty, DtcSlot.Empty];
        var zero = new DtcSnapshot(1002, Enumerable.Repeat(DtcSlot.Empty, 6).ToArray(), 0, true, "zero FE25; FE26; FE27");
        var result = new DtcAnalysisResult(
            [new("positive.trc", "00:00:01.000", new DtcSnapshot(1000, slots, 1, true, "positive")),
                new("zero.trc", "00:00:01.001", DtcSnapshot.Unknown(1001, "zero FE25")),
                new("zero.trc", "00:00:01.002", zero)],
            [new("positive.trc", code, "00:00:01.000", "00:00:01.000", null, "Конец файла")],
            [new("positive.trc", code, "00:00:01.000", "00:00:01.000", 1, "Присутствует")], []);
        using var form = new DtcDiagnosticsForm("fixtures", analyze: (_, _, _) => result);
        form.Show();
        Until(() => Field<CancellationTokenSource?>(form, "_operation") == null && Field<DtcAnalysisResult?>(form, "_result") != null);
        var tabs = Field<TabControl>(form, "_detailsTabs");
        var search = Field<TextBox>(form, "_search");
        Assert.Equal(0, tabs.SelectedIndex);
        var fileFilter = Field<ComboBox>(form, "_fileFilter");
        // Repeatedly grow and shrink wrapped virtual grids. In WinForms,
        // automatic row sizing during removal used to reenter a deleted row.
        for (int pass = 0; pass < 20; pass++)
        {
            fileFilter.SelectedIndex = 0;
            tabs.SelectedIndex = 0;
            search.Text = "99999";
            Assert.Equal(0, Field<DataGridView>(form, "_summaryGrid").RowCount);
            Assert.Equal(3, Field<DataGridView>(form, "_snapshotGrid").RowCount);
            Assert.Equal(0, tabs.SelectedIndex);
            search.Clear();
            Assert.Equal(1, Field<DataGridView>(form, "_snapshotGrid").RowCount);
            fileFilter.SelectedIndex = 2;
            Assert.Equal(0, Field<DataGridView>(form, "_summaryGrid").RowCount);
            Assert.Equal(1, tabs.SelectedIndex);
            Assert.Equal(2, Field<DataGridView>(form, "_snapshotGrid").RowCount);
            Assert.Equal(1, Field<DataGridView>(form, "_snapshotGrid").CurrentCell!.RowIndex);
            Assert.Equal("Диагностические посылки прочитаны; подтверждённых ошибок нет", Field<Label>(form, "_status").Text);
            Assert.Contains(zero.RawData, Field<TextBox>(form, "_rawData").Text);
            fileFilter.SelectedIndex = 1;
            Assert.Equal(1, Field<DataGridView>(form, "_summaryGrid").RowCount);
            Assert.Equal("Анализ завершён", Field<Label>(form, "_status").Text);
            Application.DoEvents();
        }
    });

    private static object? Invoke(object instance, string name, params object?[] arguments)
        => instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(instance, arguments);

    private static T Field<T>(object instance, string name)
        => (T)instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;

    private static void Until(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("DTC UI operation did not complete.");
            Application.DoEvents();
            Thread.Sleep(10);
        }
    }

    private static void Run(Action action)
    {
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException, threadScope: true);
                action();
            }
            catch (Exception ex) { failure = ExceptionDispatchInfo.Capture(ex); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(40)), "DTC UI test timed out.");
        failure?.Throw();
    }
}
