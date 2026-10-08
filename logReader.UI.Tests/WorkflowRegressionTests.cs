using logReader.UI.Controls;

namespace logReader.UI.Tests;

public class WorkflowRegressionTests
{
    [Fact]
    public void Navigation_routes_keep_existing_workflow_actions_and_journal() => UiThread.Run(() =>
    {
        using var form = new MainForm();
        UiThread.Show(form);
        var pages = UiThread.Field<Panel[]>(form, "_pages");
        var navigation = UiThread.Field<NavigationItem[]>(form, "_navigation");
        Assert.Equal(3, pages.Length);
        Assert.Equal(3, navigation.Length);
        string[] titles = ["Обработка логов", "Библиотеки посылок", "Инструменты"];
        string[][] requiredFields =
        [
            ["textBoxCanLog", "textBoxDevices", "textBoxComposites", "textBoxOutput", "buttonCANlog", "buttonViewLog", "buttonDevices", "buttonDevicesParams", "buttonOutput", "buttonSaveOptions"],
            ["buttonDevicesCreateOrAdd", "buttonCompositesCreateOrAdd"],
            ["buttonTrcToAsc"]
        ];
        for (int route = 0; route < 3; route++)
        {
            navigation[route].PerformClick();
            UiThread.Pump();
            Assert.Equal(pages[route], Assert.Single(pages, p => p.Visible));
            Assert.Equal(navigation[route], Assert.Single(navigation, n => n.Selected));
            Assert.True(navigation[route].AccessibilityObject.State.HasFlag(AccessibleStates.Selected));
            Assert.Equal(titles[route], UiThread.Field<Label>(form, "_pageTitle").Text);
            var contents = UiThread.Descendants(pages[route]).ToList();
            foreach (string field in requiredFields[route])
                Assert.Contains(UiThread.Field<Control>(form, field), contents);
            Assert.True(UiThread.Field<Button>(form, "buttonProcess").Visible);
            Assert.True(UiThread.Field<TextBox>(form, "textBoxLog").Visible);
        }
        navigation[1].PerformClick();
        var libraryButtons = UiThread.Descendants(pages[1]).OfType<Button>().ToList();
        Assert.Equal(2, libraryButtons.Count(b => b.Text == "Выбрать файл"));
        Assert.Contains(libraryButtons, b => b.Text == "Устройства и параметры");
        libraryButtons.Single(b => b.Text == "К обработке логов").PerformClick();
        Assert.True(pages[0].Visible);
        navigation[2].PerformClick();
        var toolButtons = UiThread.Descendants(pages[2]).OfType<Button>().ToList();
        Assert.Contains(toolButtons, b => b.Text == "Настроить сохранение");
        Assert.Contains(toolButtons, b => b.Text == "Открыть справку");
    });

    [Fact]
    public void Error_words_in_paths_do_not_mark_failure_and_actual_errors_survive_resize() => UiThread.Run(() =>
    {
        using var form = new MainForm();
        UiThread.Show(form);
        var status = UiThread.Field<StatusBadge>(form, "_workspaceStatus");
        UiThread.Invoke(form, "PresentLogState", "Готово: C:\\логи\\ошибки\\result.csv");
        Assert.False(UiThread.Field<bool>(form, "_runHadError"));
        Assert.False(UiThread.Field<bool>(form, "_hasOperationResult"));
        Assert.NotEqual(StatusTone.Error, status.Tone);
        UiThread.Invoke(form, "PresentLogState", "Ошибка: файл занят другой программой.");
        Assert.True(UiThread.Field<bool>(form, "_runHadError"));
        Assert.True(UiThread.Field<bool>(form, "_hasOperationResult"));
        string detail = UiThread.Field<Label>(form, "_readinessLabel").Text;
        form.Size = form.MinimumSize;
        UiThread.Pump();
        Assert.Equal(StatusTone.Error, status.Tone);
        Assert.Equal(detail, UiThread.Field<Label>(form, "_readinessLabel").Text);
        UiThread.Field<TextBox>(form, "textBoxCanLog").Text = "another-input.trc";
        Assert.False(UiThread.Field<bool>(form, "_hasOperationResult"));
        Assert.NotEqual(StatusTone.Error, status.Tone);
    });

    [Fact]
    public void Filtered_composite_selection_retains_source_index_and_action_state() => UiThread.Run(() =>
    {
        using var fixture = new UiFixtures();
        using var form = new CompositeEditorForm(fixture.Composites);
        UiThread.Show(form);
        var search = UiThread.Field<TextBox>(form, "_txtSearch");
        var grid = UiThread.Field<DataGridView>(form, "_grid");
        search.Text = "Pressure";
        UiThread.Pump();
        Assert.Equal(1, grid.RowCount);
        grid.CurrentCell = grid.Rows[0].Cells[0];
        grid.Rows[0].Selected = true;
        UiThread.Pump();
        Assert.Equal(1, UiThread.Invoke(form, "SelectedIndex"));
        Assert.Equal("Pressure", grid.Rows[0].Cells["Param"].Value);
        Assert.True(UiThread.Field<Button>(form, "_btnEdit").Enabled);
        Assert.True(UiThread.Field<Button>(form, "_btnDelete").Enabled);
        search.Text = "unmatched fixture parameter";
        UiThread.Pump();
        Assert.Equal(0, grid.RowCount);
        Assert.Equal(-1, UiThread.Invoke(form, "SelectedIndex"));
        Assert.False(UiThread.Field<Button>(form, "_btnEdit").Enabled);
        Assert.False(UiThread.Field<Button>(form, "_btnDelete").Enabled);
    });

    [Fact]
    public void File_kind_requires_confirmation_and_keeps_cancel_unselected() => UiThread.Run(() =>
    {
        using (var cancelled = new FileKindPromptForm())
        {
            UiThread.Show(cancelled);
            UiThread.Descendants(cancelled).OfType<Button>().Single(b => b.AccessibleName == "Выбрать DBF .dbf").PerformClick();
            Assert.Equal(FileKindPromptForm.FileKind.None, cancelled.SelectedKind);
            cancelled.CancelButton!.PerformClick();
            Assert.Equal(FileKindPromptForm.FileKind.None, cancelled.SelectedKind);
        }
        using var confirmed = new FileKindPromptForm();
        UiThread.Show(confirmed);
        UiThread.Descendants(confirmed).OfType<Button>().Single(b => b.AccessibleName == "Выбрать DBF .dbf").PerformClick();
        confirmed.AcceptButton!.PerformClick();
        Assert.Equal(DialogResult.OK, confirmed.DialogResult);
        Assert.Equal(FileKindPromptForm.FileKind.Dbf, confirmed.SelectedKind);
    });

    [Fact]
    public void Conversion_blocks_reentry_and_closing_then_reports_the_real_output() => UiThread.Run(() =>
    {
        using var fixture = new UiFixtures();
        string original = File.ReadAllText(fixture.Trc);
        using var release = new ManualResetEventSlim(false);
        var messages = new System.Collections.Concurrent.ConcurrentQueue<string>();
        using var form = new FormatConversionDialog([new("trc_to_asc", "TRC → ASC", ".trc", ".asc")], fixture.Trc, message =>
        {
            messages.Enqueue(message);
            // Hold the worker at its real completion callback so the busy checks
            // cannot race a tiny fixture that converts in less than one UI turn.
            if (message.StartsWith("Конвертация завершена", StringComparison.Ordinal)
                && !release.Wait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("Conversion test did not release its completion barrier.");
        });
        UiThread.Show(form);
        var convert = UiThread.Field<Button>(form, "_convertButton");
        convert.PerformClick();
        try
        {
            Assert.True(UiThread.Field<bool>(form, "_busy"));
            foreach (string member in new[] { "_inputPathTextBox", "_outputPathTextBox", "_pairComboBox", "_browseInputButton", "_browseOutputButton", "_closeButton", "_convertButton" })
                Assert.False(UiThread.Field<Control>(form, member).Enabled);
            Assert.True(UiThread.Field<TableLayoutPanel>(form, "_progressPanel").Visible);
            Assert.True(UiThread.Field<Button>(form, "_stopButton").Enabled);
            convert.PerformClick();
            form.Close();
            Assert.False(form.IsDisposed);
            Assert.True(form.Visible);
        }
        finally
        {
            release.Set();
            UiThread.Until(() => !UiThread.Field<bool>(form, "_busy"));
        }
        Assert.Single(messages, message => message.StartsWith("Конвертация завершена", StringComparison.Ordinal));
        Assert.True(File.Exists(Path.ChangeExtension(fixture.Trc, ".asc")));
        Assert.Equal(original, File.ReadAllText(fixture.Trc));
        Assert.Equal(StatusTone.Success, UiThread.Field<InlineNotice>(form, "_outcome").Tone);
        Assert.True(UiThread.Field<Button>(form, "_openButton").Enabled);
        Assert.True(convert.Enabled);
    });

    [Fact]
    public void Cancelling_conversion_keeps_existing_output_and_cleans_temporary_file() => UiThread.Run(() =>
    {
        using var fixture = new UiFixtures();
        // No date header creates a deterministic log callback before output is
        // written; enough frames exercise cooperative cancellation during reads.
        File.WriteAllText(fixture.Trc, string.Concat(Enumerable.Repeat("1) 0.000 Rx 123 8 01 02 03 04 05 06 07 08\n", 8192)));
        string output = Path.ChangeExtension(fixture.Trc, ".asc");
        string previous = File.ReadAllText(output);
        using var entered = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        using var form = new FormatConversionDialog([new("trc_to_asc", "TRC → ASC", ".trc", ".asc")], fixture.Trc, message =>
        {
            if (!message.StartsWith("Предупреждение: не найдено стартовое время", StringComparison.Ordinal)) return;
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Cancellation barrier was not released.");
        });
        UiThread.Show(form);
        UiThread.Field<Button>(form, "_convertButton").PerformClick();
        try
        {
            UiThread.Until(() => entered.IsSet);
            var stop = UiThread.Field<Button>(form, "_stopButton");
            stop.PerformClick();
            Assert.False(stop.Enabled);
            Assert.True(UiThread.Field<bool>(form, "_busy"));
        }
        finally
        {
            release.Set();
            UiThread.Until(() => !UiThread.Field<bool>(form, "_busy"));
        }
        Assert.Equal(previous, File.ReadAllText(output));
        Assert.Equal(StatusTone.Warning, UiThread.Field<InlineNotice>(form, "_outcome").Tone);
        Assert.Contains("остановлено", UiThread.Field<InlineNotice>(form, "_outcome").Text);
        Assert.False(UiThread.Field<Button>(form, "_openButton").Enabled);
        Assert.DoesNotContain(Directory.EnumerateFiles(fixture.LogFolder), path => Path.GetFileName(path).Contains(".tmp", StringComparison.Ordinal));
    });

    [Fact]
    public void Conversion_failure_uses_core_error_without_overwriting_existing_output() => UiThread.Run(() =>
    {
        using var fixture = new UiFixtures();
        string legacy = Path.Combine(fixture.Root, "legacy.csv");
        File.WriteAllText(legacy, "Шаг;Время;Speed\n0;12:00:00;1\n");
        string output = Path.ChangeExtension(legacy, ".asc");
        File.WriteAllText(output, "Existing output must survive a rejected source.");
        var messages = new System.Collections.Concurrent.ConcurrentQueue<string>();
        using var form = new FormatConversionDialog([new("csv_to_asc", "CSV → ASC", ".csv", ".asc")], legacy, messages.Enqueue);
        UiThread.Show(form);
        UiThread.Field<Button>(form, "_convertButton").PerformClick();
        UiThread.Until(() => !UiThread.Field<bool>(form, "_busy"));
        var notice = UiThread.Field<InlineNotice>(form, "_outcome");
        Assert.Equal(StatusTone.Error, notice.Tone);
        Assert.Contains("legacy CSV", notice.Text);
        Assert.Contains(notice.Text, messages);
        Assert.Equal("Existing output must survive a rejected source.", File.ReadAllText(output));
        Assert.False(UiThread.Field<Button>(form, "_openButton").Enabled);
    });

    [Fact]
    public void Device_selection_is_committed_only_by_confirmation_and_clones_arrays() => UiThread.Run(() =>
    {
        var enabled = new Dictionary<string, bool> { ["123"] = true };
        var parameters = new Dictionary<string, bool[]> { ["123"] = [true] };
        using (var cancelled = new Devices_ParametrsForm([UiFixtures.Device], enabled, parameters))
        {
            UiThread.Show(cancelled);
            UiThread.Invoke(cancelled, "SetAll", false);
            Assert.True(enabled["123"]);
            Assert.True(parameters["123"][0]);
            cancelled.CancelButton!.PerformClick();
            Assert.True(enabled["123"]);
            Assert.True(parameters["123"][0]);
        }
        using var confirmed = new Devices_ParametrsForm([UiFixtures.Device], enabled, parameters);
        UiThread.Show(confirmed);
        UiThread.Invoke(confirmed, "SetAll", false);
        bool[] snapshot = UiThread.Field<Dictionary<string, bool[]>>(confirmed, "_paramEnabled")["123"];
        confirmed.AcceptButton!.PerformClick();
        Assert.False(enabled["123"]);
        Assert.False(parameters["123"][0]);
        Assert.NotSame(snapshot, parameters["123"]);
    });

    [Fact]
    public void Editing_filtered_composite_changes_memory_until_explicit_save() => UiThread.Run(() =>
    {
        using var fixture = new UiFixtures();
        using var form = new CompositeEditorForm(fixture.Composites);
        UiThread.Show(form);
        UiThread.Field<TextBox>(form, "_txtSearch").Text = "Pressure";
        var grid = UiThread.Field<DataGridView>(form, "_grid");
        grid.CurrentCell = grid.Rows[0].Cells[0];
        grid.Rows[0].Selected = true;
        System.Runtime.ExceptionServices.ExceptionDispatchInfo? failure = null;
        using var editResponder = new System.Windows.Forms.Timer { Interval = 20 };
        editResponder.Tick += (_, _) =>
        {
            var editor = Application.OpenForms.OfType<CompositeParamEditForm>().FirstOrDefault();
            if (editor == null) return;
            editResponder.Stop();
            try
            {
                UiThread.Field<TextBox>(editor, "_txtParam").Text = "ChangedPressure";
                editor.AcceptButton!.PerformClick();
            }
            catch (Exception ex)
            {
                failure = System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex);
                editor.DialogResult = DialogResult.Cancel;
                editor.Close();
            }
        };
        editResponder.Start();
        UiThread.Field<Button>(form, "_btnEdit").PerformClick();
        failure?.Throw();
        Assert.True(UiThread.Field<bool>(form, "_dirty"));
        Assert.False(form.Modified);
        Assert.Equal("Pressure", CompositeExcelFile.ReadAll(fixture.Composites)[1].Param);
        UiThread.Field<Button>(form, "_btnSave").PerformClick();
        Assert.False(UiThread.Field<bool>(form, "_dirty"));
        Assert.True(form.Modified);
        var persisted = CompositeExcelFile.ReadAll(fixture.Composites);
        Assert.Equal("Temperature", persisted[0].Param);
        Assert.Equal("ChangedPressure", persisted[1].Param);
    });

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    public void Save_options_preserve_mappings_ranges_and_deferred_format_checks(int format, int batch) => UiThread.Run(() =>
    {
        using var fixture = new UiFixtures();
        using var form = new SaveOptionsForm((OutputFormat)format, (BatchOutputMode)batch,
            new() { BlockPeriodMs = 25, BlockStartIndex = 17 }, fixture.LogFolder, LogFormatKind.All, true);
        UiThread.Show(form);
        var formats = UiThread.Field<CheckedListBox>(form, "_formatsList");
        var ok = UiThread.Field<Button>(form, "_okButton");
        var period = UiThread.Field<NumericUpDown>(form, "_numBlockPeriod");
        var anchor = UiThread.Field<NumericUpDown>(form, "_numBlockStart");
        Assert.Equal(3, formats.Items.Count); // All, TRC and ASC from the fixture folder.
        Assert.Equal(1m, period.Minimum);
        Assert.Equal(3600000m, period.Maximum);
        Assert.Equal(0m, anchor.Minimum);
        Assert.Equal(10000000m, anchor.Maximum);
        Assert.Equal(25m, period.Value);
        Assert.Equal(17m, anchor.Value);
        Assert.Equal(format == 2, UiThread.Field<Panel>(form, "_dstPanel").Visible);

        formats.SetItemChecked(0, false);
        UiThread.Pump();
        Assert.Empty(formats.CheckedItems.Cast<object>());
        Assert.False(ok.Enabled);
        formats.SetItemChecked(1, true);
        UiThread.Pump();
        Assert.True(ok.Enabled);
        Assert.False(formats.GetItemChecked(0));
        formats.SetItemChecked(2, true);
        UiThread.Pump();
        Assert.True(formats.GetItemChecked(0));
        formats.SetItemChecked(1, false);
        formats.SetItemChecked(2, false);
        UiThread.Pump();
        Assert.False(ok.Enabled);
        Assert.False(formats.GetItemChecked(0));
        formats.SetItemChecked(0, true);
        UiThread.Pump();
        Assert.True(ok.Enabled);
        Assert.Equal(3, formats.CheckedItems.Count);
        period.Value = 123;
        anchor.Value = 42;
        ok.PerformClick();
        Assert.Equal((OutputFormat)format, form.SelectedOutputFormat);
        Assert.Equal((BatchOutputMode)batch, form.SelectedBatchMode);
        Assert.Equal(LogFormatKind.Trc | LogFormatKind.Asc, form.SelectedFolderFormats);
        Assert.Equal(123, form.SelectedDstConnectOptions.BlockPeriodMs);
        Assert.Equal(42, form.SelectedDstConnectOptions.BlockStartIndex);
        Assert.True(form.IncludeDeviceIdHeaderRow);
    });
}
