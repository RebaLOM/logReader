using System.Reflection;
using System.Runtime.ExceptionServices;

namespace logReader.UI.Tests;

public sealed class DtcWorkflowTests
{
    [Fact]
    public void Save_options_offer_three_Dtc_modes_and_round_trip_timeout() => Run(() =>
    {
        using var defaults = new SaveOptionsForm(OutputFormat.Csv, BatchOutputMode.PerInputFile,
            new DstConnectOptions(), null, LogFormatKind.All);
        Assert.Equal(DtcInclusionMode.Auto, defaults.DtcInclusion);
        Assert.Equal(2000, defaults.DtcTimeoutMs);
        var combo = Field<ComboBox>(defaults, "_comboDtcInclusion");
        Assert.Equal(new[] { "Авто", "Всегда", "Не добавлять" }, combo.Items.Cast<string>().ToArray());
        Assert.Equal(ComboBoxStyle.DropDownList, combo.DropDownStyle);
        Assert.Equal(0, combo.SelectedIndex);
        var timeout = Field<NumericUpDown>(defaults, "_numDtcTimeout");
        Assert.True(timeout.Enabled);
        combo.SelectedIndex = 2;
        Assert.False(timeout.Enabled);
        combo.SelectedIndex = 1;
        Assert.True(timeout.Enabled);
        timeout.Value = 3.125M;
        Invoke(defaults, "buttonOk_Click", null, EventArgs.Empty);
        Assert.Equal(DtcInclusionMode.Always, defaults.DtcInclusion);
        Assert.Equal(3125, defaults.DtcTimeoutMs);

        using var restored = new SaveOptionsForm(OutputFormat.Csv, BatchOutputMode.PerInputFile,
            new DstConnectOptions(), null, LogFormatKind.All, false, defaults.DtcInclusion, defaults.DtcTimeoutMs);
        Assert.Equal(1, Field<ComboBox>(restored, "_comboDtcInclusion").SelectedIndex);
        Assert.Equal(3.125M, Field<NumericUpDown>(restored, "_numDtcTimeout").Value);
        Field<ComboBox>(restored, "_comboDtcInclusion").SelectedIndex = 2;
        Invoke(restored, "buttonOk_Click", null, EventArgs.Empty);
        Assert.Equal(DtcInclusionMode.Never, restored.DtcInclusion);
        Field<ComboBox>(restored, "_comboDtcInclusion").SelectedIndex = 0;
        Assert.True(Field<NumericUpDown>(restored, "_numDtcTimeout").Enabled);
        Invoke(restored, "buttonOk_Click", null, EventArgs.Empty);
        Assert.Equal(DtcInclusionMode.Auto, restored.DtcInclusion);
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
        Assert.Equal("32770.0", timeline.Rows[0].Cells[4].Value);
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
        Field<TextBox>(form, "_search").Text = "32770.0";
        Assert.Equal(1, summary.RowCount);
    });

    [Fact]
    public void Cancelled_save_options_do_not_apply_the_edited_mode() => Run(() =>
    {
        using var form = new SaveOptionsForm(OutputFormat.Csv, BatchOutputMode.PerInputFile,
            new DstConnectOptions(), null, LogFormatKind.All);
        form.Show();
        Field<ComboBox>(form, "_comboDtcInclusion").SelectedIndex = 1;
        ((Button)form.CancelButton!).PerformClick();
        Assert.Equal(DialogResult.Cancel, form.DialogResult);
        Assert.Equal(DtcInclusionMode.Auto, form.DtcInclusion);
    });

    [Theory]
    [InlineData(DtcDisplayFormat.Compact, "32770.10")]
    [InlineData(DtcDisplayFormat.Detailed, "SPN 32770 / FMI 10")]
    public void Diagnostic_tables_use_selected_format_and_keep_unknown_and_empty_slots_distinct(
        DtcDisplayFormat format, string expected) => Run(() =>
    {
        var code = new DtcCode(32770, 10);
        DtcSlot[] slots = [DtcSlot.Present(code), DtcSlot.Empty, DtcSlot.Unknown,
            DtcSlot.Empty, DtcSlot.Empty, DtcSlot.Empty];
        var result = new DtcAnalysisResult(
            [new("fixture.trc", "1", new DtcSnapshot(1, slots, 1, false, "raw frame"))],
            [new("fixture.trc", code, "1", "1", null, "Конец файла")],
            [new("fixture.trc", code, "1", "1", 1, "Присутствует")], []);
        using var form = new DtcDiagnosticsForm("fixture.trc", analyze: (_, _, _) => result,
            displayFormat: format);
        form.Show();
        Until(() => Field<CancellationTokenSource?>(form, "_operation") == null
            && Field<DtcAnalysisResult?>(form, "_result") != null);
        Assert.Equal(expected, Field<DataGridView>(form, "_summaryGrid").Rows[0].Cells[1].Value);
        Assert.Equal(expected, Field<DataGridView>(form, "_episodeGrid").Rows[0].Cells[1].Value);
        var snapshots = Field<DataGridView>(form, "_snapshotGrid");
        Assert.Equal(expected, snapshots.Rows[0].Cells[4].Value);
        Assert.Equal("", snapshots.Rows[0].Cells[5].Value);
        Assert.Equal("Н/Д", snapshots.Rows[0].Cells[6].Value);
        Field<TextBox>(form, "_search").Text = "32770.10";
        Assert.Equal(1, Field<DataGridView>(form, "_summaryGrid").RowCount);
        Field<TextBox>(form, "_search").Text = "SPN 32770 / FMI 10";
        Assert.Equal(1, Field<DataGridView>(form, "_summaryGrid").RowCount);
    });

    [Theory]
    [InlineData(0, true, false)]
    [InlineData(1, false, true)]
    [InlineData(2, false, false)]
    public void Main_restores_output_mode_and_display_format_independently(
        int mode, bool expectedAuto, bool expectedAlways) => Run(() =>
    {
        string root = Path.Combine(Path.GetTempPath(), "loger-display-main-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new DisplaySettingsStore(Path.Combine(root, "settings.json"));
            using (var form = new MainForm(store))
            {
                var defaults = (OutputSettings)Invoke(form, "BuildOutputSettings", OutputFormat.Csv)!;
                Assert.True(defaults.AutoIncludeDtc);
                Assert.False(defaults.IncludeDtc);
                Assert.Equal(DtcDisplayFormat.Compact, defaults.DtcDisplayFormat);
                var button = Field<Button>(form, "buttonDisplaySettings");
                Assert.Equal("Настройки отображения", button.AccessibleName);
                Invoke(form, "SetBusy", true, "test");
                Assert.False(button.Enabled);
                Invoke(form, "SetBusy", false, "");
                Assert.True(button.Enabled);
                Invoke(form, "ApplyDisplaySettings", new DisplaySettings
                {
                    DtcFormat = DtcDisplayFormat.Detailed
                });
                using var options = new SaveOptionsForm(OutputFormat.Csv, BatchOutputMode.PerInputFile,
                    new DstConnectOptions(), null, LogFormatKind.All);
                Field<ComboBox>(options, "_comboDtcInclusion").SelectedIndex = mode;
                Invoke(options, "buttonOk_Click", null, EventArgs.Empty);
                Invoke(form, "ApplySaveOptions", options);
                var next = (OutputSettings)Invoke(form, "BuildOutputSettings", OutputFormat.Xlsx)!;
                Assert.Equal(expectedAuto, next.AutoIncludeDtc);
                Assert.Equal(expectedAlways, next.IncludeDtc);
                Assert.Equal(DtcDisplayFormat.Detailed, next.DtcDisplayFormat);
                Assert.Equal((DtcInclusionMode)mode, store.Load().DtcInclusion);
                Assert.True(defaults.AutoIncludeDtc);
                Assert.Equal(DtcDisplayFormat.Compact, defaults.DtcDisplayFormat);
            }
            using var restored = new MainForm(store);
            var settings = (OutputSettings)Invoke(restored, "BuildOutputSettings", OutputFormat.Csv)!;
            Assert.Equal(expectedAuto, settings.AutoIncludeDtc);
            Assert.Equal(expectedAlways, settings.IncludeDtc);
            Assert.Equal(DtcDisplayFormat.Detailed, settings.DtcDisplayFormat);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
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
        search.Text = "99999";
        Assert.Equal(0, Field<DataGridView>(form, "_summaryGrid").RowCount);
        Assert.Equal(0, tabs.SelectedIndex);
        search.Clear();
        Field<ComboBox>(form, "_fileFilter").SelectedIndex = 2;
        Assert.Equal(0, Field<DataGridView>(form, "_summaryGrid").RowCount);
        Assert.Equal(1, tabs.SelectedIndex);
        Assert.Equal(2, Field<DataGridView>(form, "_snapshotGrid").RowCount);
        Assert.Equal(1, Field<DataGridView>(form, "_snapshotGrid").CurrentCell!.RowIndex);
        Assert.Equal("Диагностические посылки прочитаны; подтверждённых ошибок нет", Field<Label>(form, "_status").Text);
        Assert.Contains(zero.RawData, Field<TextBox>(form, "_rawData").Text);
        Field<ComboBox>(form, "_fileFilter").SelectedIndex = 1;
        Assert.Equal(1, Field<DataGridView>(form, "_summaryGrid").RowCount);
        Assert.Equal("Анализ завершён", Field<Label>(form, "_status").Text);
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
            try { action(); }
            catch (Exception ex) { failure = ExceptionDispatchInfo.Capture(ex); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(40)), "DTC UI test timed out.");
        failure?.Throw();
    }
}
