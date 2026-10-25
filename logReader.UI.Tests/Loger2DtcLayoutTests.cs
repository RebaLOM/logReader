using System.Reflection;
using System.Runtime.ExceptionServices;

namespace logReader.UI.Tests;

// ThemeManager and Application.OpenForms are process-wide. Keep the STA cases serial,
// including the existing workflow tests, so a theme switch cannot affect another form.
[CollectionDefinition("DTC UI", DisableParallelization = true)]
public sealed class DtcUiCollection;

[Collection("DTC UI")]
public sealed class Loger2DtcLayoutTests
{
    [Fact]
    public void Diagnostics_action_stays_in_source_card_when_workspace_narrows() => Run(() =>
    {
        using var form = new MainForm { ShowInTaskbar = false };
        form.Show();
        var diagnostic = Field<Button>(form, "buttonDtcDiagnostics");
        var select = Field<Button>(form, "buttonCANlog");
        var view = Field<Button>(form, "buttonViewLog");
        var card = Ancestors(diagnostic).OfType<WorkspaceCardPanel>().Single();
        Assert.Equal("Источник данных", card.AccessibleName);
        Assert.Same(select.Parent, diagnostic.Parent);
        Assert.Same(view.Parent, diagnostic.Parent);
        Assert.True(Assert.IsType<FlowLayoutPanel>(diagnostic.Parent).WrapContents);

        foreach (Size size in new[] { form.Size, form.MinimumSize })
        {
            form.Size = size;
            Layout(form);
            AssertVisibleBounds(diagnostic);
            AssertSeparate(select, view, diagnostic);
            Assert.Equal("Диагностика DTC", diagnostic.AccessibleName);
        }

        Invoke(form, "SetBusy", true, "Диагностика");
        Assert.False(diagnostic.Enabled);
        Invoke(form, "SetBusy", false, "");
        Assert.True(diagnostic.Enabled);
    });

    [Theory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public void Main_default_fits_full_HD_without_initial_content_scrollbars(string theme) => Run(() =>
    {
        ThemeMode previous = ThemeManager.Mode;
        try
        {
            ThemeManager.SetMode(Enum.Parse<ThemeMode>(theme), persist: false);
            using var form = new MainForm { ShowInTaskbar = false };
            form.Show();
            DialogLayout.FitToWorkingArea(form, new Rectangle(0, 0, 1920, 1040));
            Layout(form);
            foreach (bool decoder in new[] { false, true })
            {
                Invoke(form, "SelectWorkspacePage", decoder);
                Layout(form);
                foreach (string name in new[] { decoder ? "decoderPage" : "sessionPage", "exportSettings" })
                {
                    var viewport = Assert.IsType<Panel>(Find(form, name));
                    Assert.True(viewport.AutoScroll);
                    Assert.False(viewport.VerticalScroll.Visible, $"Unexpected initial scrollbar in {name}: {viewport.ClientSize}, content {viewport.DisplayRectangle}.");
                    Assert.False(viewport.HorizontalScroll.Visible);
                }
                AssertVisibleBounds(Field<Button>(form, "buttonProcess"));
                AssertVisibleBounds(Field<Button>(form, "buttonDisplaySettings"));
                AssertVisibleBounds(Field<Label>(form, "_readinessOutput"));
            }
            Capture(form, $"main-taller-default-{theme.ToLowerInvariant()}");
        }
        finally { ThemeManager.SetMode(previous, persist: false); }
    });

    [Theory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public void Main_keeps_scrolling_and_actions_accessible_on_short_screens(string theme) => Run(() =>
    {
        ThemeMode previous = ThemeManager.Mode;
        try
        {
            ThemeManager.SetMode(Enum.Parse<ThemeMode>(theme), persist: false);
            using var form = new MainForm { ShowInTaskbar = false };
            form.Show();
            var workArea = new Rectangle(0, 0, 1366, 728);
            DialogLayout.FitToWorkingArea(form, workArea);
            Layout(form);
            Assert.True(workArea.Contains(form.Bounds));
            var viewport = Assert.IsType<Panel>(Find(form, "exportSettings"));
            Assert.True(viewport.AutoScroll);
            Assert.True(viewport.VerticalScroll.Visible);
            Assert.False(viewport.HorizontalScroll.Visible);
            var lastSetting = Field<Label>(form, "_readinessOutput");
            viewport.ScrollControlIntoView(lastSetting);
            Layout(form);
            Assert.True(viewport.AutoScrollPosition.Y < 0);
            AssertVisibleBounds(lastSetting);
            AssertVisibleBounds(Field<Button>(form, "buttonProcess"));
            Capture(form, $"main-short-screen-{theme.ToLowerInvariant()}");
        }
        finally { ThemeManager.SetMode(previous, persist: false); }
    });

    [Fact]
    public void Switching_native_theme_preserves_input_selection_and_scroll_position() => Run(() =>
    {
        ThemeMode previous = ThemeManager.Mode;
        try
        {
            using var form = new MainForm { ShowInTaskbar = false };
            form.Show();
            form.Size = form.MinimumSize;
            var input = Field<TextBox>(form, "textBoxCanLog");
            input.Text = @"C:\fixtures\trace.trc";
            input.Select(3, 6);
            var viewport = Assert.IsType<Panel>(Find(form, "exportSettings"));
            viewport.ScrollControlIntoView(Field<Label>(form, "_readinessOutput"));
            Layout(form);
            Point position = viewport.AutoScrollPosition;
            foreach (ThemeMode theme in new[] { ThemeMode.Dark, ThemeMode.Light, ThemeMode.Dark })
            {
                ThemeManager.SetMode(theme, persist: false);
                Layout(form);
                Assert.Equal(theme == ThemeMode.Dark ? SystemColorMode.Dark : SystemColorMode.Classic, Application.ColorMode);
                Assert.Equal(@"C:\fixtures\trace.trc", input.Text);
                Assert.Equal(3, input.SelectionStart);
                Assert.Equal(6, input.SelectionLength);
                Assert.Equal(position, viewport.AutoScrollPosition);
            }
        }
        finally { ThemeManager.SetMode(previous, persist: false); }
    });

    [Theory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public void Display_settings_action_stays_reachable_in_export_inspector(string theme) => Run(() =>
    {
        ThemeMode previous = ThemeManager.Mode;
        try
        {
            ThemeManager.SetMode(Enum.Parse<ThemeMode>(theme), persist: false);
            using var form = new MainForm { ShowInTaskbar = false };
            form.Show();
            var display = Field<Button>(form, "buttonDisplaySettings");
            var save = Field<Button>(form, "buttonSaveOptions");
            Assert.Contains(Ancestors(display), ancestor => ancestor.Name == "exportInspector");
            Assert.Equal("Настройки отображения", display.AccessibleName);
            Assert.Equal(ThemeManager.Current.Text, display.ForeColor);
            var viewport = Assert.IsType<Panel>(Find(form, "exportSettings"));

            foreach (Size size in new[] { form.Size, form.MinimumSize })
            {
                form.Size = size;
                Layout(form);
                viewport.ScrollControlIntoView(display);
                Layout(form);
                AssertVisibleBounds(display);
                AssertVisibleBounds(save);
                AssertSeparate(display, save);
            }

            Invoke(form, "SetBusy", true, "Проверка настроек");
            Assert.False(display.Enabled);
            Assert.False(save.Enabled);
            Invoke(form, "SetBusy", false, "");
            Assert.True(display.Enabled);
            Assert.True(save.Enabled);
            Capture(form, $"display-settings-action-{theme.ToLowerInvariant()}");
        }
        finally { ThemeManager.SetMode(previous, persist: false); }
    });

    [Theory]
    [InlineData("Dark", OutputFormat.Csv, 0)]
    [InlineData("Light", OutputFormat.Csv, 1)]
    [InlineData("Dark", OutputFormat.CsvDstConnect, 0)]
    [InlineData("Light", OutputFormat.CsvDstConnect, 1)]
    [InlineData("Dark", OutputFormat.Csv, 2)]
    [InlineData("Light", OutputFormat.CsvDstConnect, 2)]
    public void Save_options_Dtc_controls_fit_alongside_existing_export_options(string theme, OutputFormat format, int mode) => Run(() =>
    {
        ThemeMode previous = ThemeManager.Mode;
        try
        {
            ThemeManager.SetMode(Enum.Parse<ThemeMode>(theme), persist: false);
            using var folder = new TestLogFolder();
            using var form = new SaveOptionsForm(format, BatchOutputMode.PerInputFile,
                new DstConnectOptions(), folder.Path, LogFormatKind.All, dtcInclusion: (DtcInclusionMode)mode);
            form.Show();
            Layout(form);
            var controls = new List<Control>
            {
                Field<ComboBox>(form, "_comboOutputFormat"), Field<ComboBox>(form, "_comboBatchMode"),
                Field<CheckBox>(form, "_chkIncludeDeviceIdRow"), Field<ComboBox>(form, "_comboDtcInclusion"),
                Field<NumericUpDown>(form, "_numDtcTimeout"), Field<CheckedListBox>(form, "_formatsList"),
                Assert.IsType<Button>(form.AcceptButton), Assert.IsType<Button>(form.CancelButton)
            };
            if (format == OutputFormat.CsvDstConnect)
            {
                Assert.True(Field<Panel>(form, "_dstPanel").Visible);
                controls.Add(Field<NumericUpDown>(form, "_numBlockPeriod"));
                controls.Add(Field<NumericUpDown>(form, "_numBlockStart"));
            }
            Control dtcSection = Ancestors(Field<ComboBox>(form, "_comboDtcInclusion"))
                .OfType<Panel>().First(panel => Equals(panel.Tag, "surface"));
            controls.AddRange(Descendants(dtcSection).OfType<Label>());
            foreach (Control control in controls) AssertVisibleBounds(control);
            AssertSeparate(controls.ToArray());
            var formats = Field<CheckedListBox>(form, "_formatsList");
            Assert.True(formats.Enabled);
            Assert.Equal(2, formats.Items.Count); // All formats plus the fixture's TRC format.
            Assert.True(formats.ClientSize.Height >= 2 * formats.ItemHeight);
            Capture(form, $"save-options-{format}-{theme.ToLowerInvariant()}-{mode}");
            Assert.Equal(ThemeManager.Current.Background, form.BackColor);
            Assert.Equal(ThemeManager.Current.Surface, Field<NumericUpDown>(form, "_numDtcTimeout").BackColor);
            Assert.Equal(ThemeManager.Current.Text, Field<ComboBox>(form, "_comboDtcInclusion").ForeColor);
            Assert.Equal(mode, Field<ComboBox>(form, "_comboDtcInclusion").SelectedIndex);
            Assert.Equal(mode != (int)DtcInclusionMode.Never, Field<NumericUpDown>(form, "_numDtcTimeout").Enabled);

            // Export uses empty cells for unknown slots; its help must not promise Н/Д.
            string help = string.Join(" ", Descendants(form).OfType<Label>().Select(label => label.Text));
            Assert.Contains("DTC1–DTC6", help);
            Assert.DoesNotContain("Н/Д", help);
        }
        finally { ThemeManager.SetMode(previous, persist: false); }
    });

    [Theory]
    [InlineData("Dark", false)]
    [InlineData("Light", false)]
    [InlineData("Dark", true)]
    [InlineData("Light", true)]
    public void Diagnostics_controls_and_raw_data_fit_default_and_minimum_window(string theme, bool minimumSize) => Run(() =>
    {
        ThemeMode previous = ThemeManager.Mode;
        try
        {
            ThemeManager.SetMode(Enum.Parse<ThemeMode>(theme), persist: false);
            using var form = new DtcDiagnosticsForm("fixture.trc", analyze: (_, _, _) => EmptyResult())
                { ShowInTaskbar = false };
            form.Show();
            Until(() => Field<DtcAnalysisResult?>(form, "_result") != null);
            if (minimumSize) form.Size = form.MinimumSize;
            Field<TabControl>(form, "_detailsTabs").SelectedIndex = 1;
            Layout(form);

            Control[] inputs =
            [
                Find(form, "dtcSource"), Field<NumericUpDown>(form, "_timeout"),
                Field<Button>(form, "_analyzeButton"), Field<Button>(form, "_cancelButton"),
                Field<Label>(form, "_status"), Field<Label>(form, "_counts"),
                Field<TextBox>(form, "_search"), Field<ComboBox>(form, "_fileFilter"),
                Field<DataGridView>(form, "_summaryGrid"), Field<DataGridView>(form, "_snapshotGrid"),
                Field<TextBox>(form, "_rawData")
            ];
            foreach (Control input in inputs) AssertVisibleBounds(input);
            AssertSeparate(inputs);
            Capture(form, $"diagnostics-{theme.ToLowerInvariant()}-{(minimumSize ? "minimum" : "default")}");
            Assert.True(Field<DataGridView>(form, "_summaryGrid").Height >= 100, "Summary must show its header and several codes.");
            int snapshotHeight = Field<DataGridView>(form, "_snapshotGrid").Height;
            Assert.True(snapshotHeight >= 40, $"Snapshots table is only {snapshotHeight}px high.");
            Assert.True(Field<TextBox>(form, "_rawData").Height >= 70, "Raw data needs room for the source and CAN frames.");
            Assert.Equal(ThemeManager.Current.Background, form.BackColor);
            AssertGridTheme(Field<DataGridView>(form, "_summaryGrid"));
            AssertGridTheme(Field<DataGridView>(form, "_snapshotGrid"));
            Assert.Equal(ThemeManager.Current.SurfaceAlt, Field<TextBox>(form, "_rawData").BackColor);
            Assert.IsType<ThemedTabControl>(Field<TabControl>(form, "_detailsTabs"));
        }
        finally { ThemeManager.SetMode(previous, persist: false); }
    });

    [Fact]
    public void Open_Dtc_dialogs_follow_live_theme_changes() => Run(() =>
    {
        ThemeMode previous = ThemeManager.Mode;
        using var diagnostics = new DtcDiagnosticsForm("fixture.trc", analyze: (_, _, _) => EmptyResult())
            { ShowInTaskbar = false };
        using var options = new SaveOptionsForm(OutputFormat.Csv, BatchOutputMode.PerInputFile,
            new DstConnectOptions(), null, LogFormatKind.All);
        using var main = new MainForm { ShowInTaskbar = false };
        try
        {
            main.Show();
            diagnostics.Show();
            options.Show();
            Until(() => Field<DtcAnalysisResult?>(diagnostics, "_result") != null);
            foreach (ThemeMode theme in new[] { ThemeMode.Light, ThemeMode.Dark })
            {
                ThemeManager.SetMode(theme, persist: false);
                Layout(diagnostics);
                Layout(options);
                Layout(main);
                Assert.Equal(ThemeManager.Current.Background, diagnostics.BackColor);
                Assert.Equal(ThemeManager.Current.Background, options.BackColor);
                AssertGridTheme(Field<DataGridView>(diagnostics, "_summaryGrid"));
                Assert.Equal(ThemeManager.Current.Text, Field<ComboBox>(options, "_comboDtcInclusion").ForeColor);
                Assert.Equal(ThemeManager.Current.SurfaceAlt, Field<TextBox>(diagnostics, "_rawData").BackColor);
                Capture(main, $"main-{theme.ToString().ToLowerInvariant()}");
            }
        }
        finally { ThemeManager.SetMode(previous, persist: false); }
    });

    [Fact]
    public void Diagnostics_window_retains_unknown_slots_count_and_completeness() => Run(() =>
    {
        var code = new DtcCode(32770, 0);
        DtcSlot[] partial = [DtcSlot.Present(code), DtcSlot.Unknown, DtcSlot.Empty, DtcSlot.Empty, DtcSlot.Empty, DtcSlot.Empty];
        var result = new DtcAnalysisResult(
            [new("fixture.trc", "0", DtcSnapshot.Unknown(0, "unknown frame")),
                new("fixture.trc", "1", new DtcSnapshot(1, partial, 1, false, "partial frame")),
                new("fixture.trc", "2", new DtcSnapshot(2, Enumerable.Repeat(DtcSlot.Empty, 6).ToArray(), 0, true, "complete frame"))],
            [new("fixture.trc", code, "1", "1", "2", "Отсутствие в полном снимке")],
            [new("fixture.trc", code, "1", "1", 1, "Отсутствует в полном снимке")], []);
        using var form = new DtcDiagnosticsForm("fixture.trc", analyze: (_, _, _) => result) { ShowInTaskbar = false };
        form.Show();
        Until(() => Field<DtcAnalysisResult?>(form, "_result") != null);
        var grid = Field<DataGridView>(form, "_snapshotGrid");
        Field<TabControl>(form, "_detailsTabs").SelectedIndex = 1;
        Layout(form);
        Assert.Equal(new[] { "Файл", "Время", "Количество ошибок", "Полнота списка", "DTC1", "DTC2", "DTC3", "DTC4", "DTC5", "DTC6" },
            grid.Columns.Cast<DataGridViewColumn>().Select(column => column.HeaderText));
        Assert.Equal("Н/Д", grid.Rows[0].Cells[2].Value);
        Assert.Equal("Неполный", grid.Rows[0].Cells[3].Value);
        Assert.All(grid.Rows[0].Cells.Cast<DataGridViewCell>().Skip(4), cell => Assert.Equal("Н/Д", cell.Value));
        Assert.Equal("1", grid.Rows[1].Cells[2].Value);
        Assert.Equal("32770.0", grid.Rows[1].Cells[4].Value);
        Assert.Equal("Н/Д", grid.Rows[1].Cells[5].Value);
        Assert.Equal("", grid.Rows[1].Cells[6].Value);
        Assert.Equal("0", grid.Rows[2].Cells[2].Value);
        Assert.Equal("Полный", grid.Rows[2].Cells[3].Value);
        Assert.All(grid.Rows[2].Cells.Cast<DataGridViewCell>().Skip(4), cell => Assert.Equal("", cell.Value));
        grid.CurrentCell = grid.Rows[1].Cells[1];
        Assert.Contains("partial frame", Field<TextBox>(form, "_rawData").Text);
        grid.CurrentCell = grid.Rows[2].Cells[1];
        Assert.Contains("complete frame", Field<TextBox>(form, "_rawData").Text);
    });

    [Theory]
    [InlineData(1680, 880, 1f, DtcDisplayFormat.Compact)]
    [InlineData(1318, 700, 1f, DtcDisplayFormat.Compact)]
    [InlineData(1318, 700, 1.25f, DtcDisplayFormat.Compact)]
    [InlineData(1318, 700, 1.5f, DtcDisplayFormat.Compact)]
    [InlineData(1680, 880, 1f, DtcDisplayFormat.Detailed)]
    [InlineData(1318, 700, 1f, DtcDisplayFormat.Detailed)]
    [InlineData(1318, 700, 1.25f, DtcDisplayFormat.Detailed)]
    [InlineData(1318, 700, 1.5f, DtcDisplayFormat.Detailed)]
    public void Diagnostics_tables_keep_all_columns_and_wrapped_values_visible(int width, int height, float textScale, DtcDisplayFormat format) => Run(() =>
    {
        const string file = "Очень_длинное_имя_файла_для_проверки_всех_шести_диагностических_позиций_2025-10-16.trc";
        const string time = "2025-10-16 06:45:14.274";
        var code = new DtcCode(524287, 31);
        var slots = Enumerable.Repeat(DtcSlot.Present(code), 6).ToArray();
        var result = new DtcAnalysisResult(
            [new(file, time, new DtcSnapshot(1, slots, 6, true, "18FE2503 / 18FE2603 / 18FE2703"))],
            [new(file, code, time, time, time, "Отсутствие в полном снимке")],
            [new(file, code, time, time, 1, "Отсутствует в полном снимке")], []);
        using var form = new DtcDiagnosticsForm(file, analyze: (_, _, _) => result, displayFormat: format) { ShowInTaskbar = false };
        form.Show();
        Until(() => Field<DtcAnalysisResult?>(form, "_result") != null);
        form.Size = new Size(width, height);

        var summary = Field<DataGridView>(form, "_summaryGrid");
        var episodes = Field<DataGridView>(form, "_episodeGrid");
        var snapshots = Field<DataGridView>(form, "_snapshotGrid");
        // Exercise text metrics at 100/125/150% as well as both screen widths.
        // The process's actual monitor DPI is tested separately in the native audit.
        using var scaledFont = new Font(summary.Font.FontFamily, summary.Font.Size * textScale);
        foreach (DataGridView grid in new[] { summary, episodes, snapshots }) grid.Font = scaledFont;
        var tabs = Field<TabControl>(form, "_detailsTabs");
        tabs.SelectedIndex = 0;
        Layout(form);
        AssertAllColumnsFit(summary);
        AssertAllColumnsFit(episodes);
        AssertWrappedRowFits(summary);
        AssertWrappedRowFits(episodes);
        AssertFullFirstRowVisible(summary);
        AssertFullFirstRowVisible(episodes);

        tabs.SelectedIndex = 1;
        Layout(form);
        AssertAllColumnsFit(snapshots);
        AssertWrappedRowFits(snapshots);
        AssertFullFirstRowVisible(summary);
        AssertFullFirstRowVisible(snapshots);
        Assert.Equal(file, snapshots.Rows[0].Cells[0].Value);
        Assert.Equal(file, snapshots.Rows[0].Cells[0].ToolTipText);
        string displayName = Assert.IsType<string>(snapshots.Rows[0].Cells[0].FormattedValue);
        foreach (string line in displayName.Split(Environment.NewLine))
        {
            int textWidth = TextRenderer.MeasureText(line, snapshots.Font, Size.Empty,
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine).Width;
            Assert.True(textWidth <= snapshots.Columns[0].Width - snapshots.DefaultCellStyle.Padding.Horizontal,
                $"A file name line is cut off: {line}.");
        }
        Assert.Equal(code.ToDisplayString(format), snapshots.Rows[0].Cells[9].Value);
        Assert.Contains("18FE2703", Field<TextBox>(form, "_rawData").Text);
        AssertVisibleBounds(Field<TextBox>(form, "_rawData"));
        Capture(form, $"diagnostics-six-slots-{width}-{textScale.ToString(System.Globalization.CultureInfo.InvariantCulture)}-{format}");

        // At the deliberately smaller minimum, horizontal scrolling preserves
        // the minimum readable widths instead of hiding the rightmost fields.
        form.Size = form.MinimumSize;
        Layout(form);
        Assert.Equal(ScrollBars.Both, snapshots.ScrollBars);
        snapshots.FirstDisplayedScrollingColumnIndex = snapshots.ColumnCount - 1;
        Layout(form);
        Rectangle last = snapshots.GetColumnDisplayRectangle(snapshots.ColumnCount - 1, true);
        Assert.True(last.Width > 0, "DTC6 must remain reachable at the minimum window size.");
    });

    [Fact]
    public void Diagnostics_default_window_fits_its_monitor_working_area() => Run(() =>
    {
        using var form = new DtcDiagnosticsForm("fixture.trc", analyze: (_, _, _) => EmptyResult()) { ShowInTaskbar = false };
        form.Show();
        Until(() => Field<DtcAnalysisResult?>(form, "_result") != null);
        Assert.True(Screen.FromControl(form).WorkingArea.Contains(form.Bounds), $"Diagnostics window is outside the usable screen: {form.Bounds}.");
        Assert.True(form.Width > 1120 || Screen.FromControl(form).WorkingArea.Width <= 1152,
            "The default diagnostics window should use the available space on a larger monitor.");
    });

    private static void AssertAllColumnsFit(DataGridView grid)
    {
        int right = 0;
        foreach (DataGridViewColumn column in grid.Columns)
        {
            Rectangle bounds = grid.GetColumnDisplayRectangle(column.Index, false);
            Assert.True(bounds.Width > 0 && bounds.Left >= 0 && bounds.Right <= grid.ClientSize.Width,
                $"{grid.Name}/{column.HeaderText} is clipped: {bounds}, client {grid.ClientSize}.");
            foreach (string word in column.HeaderText.Split(' '))
                Assert.True(TextRenderer.MeasureText(word, grid.ColumnHeadersDefaultCellStyle.Font ?? grid.Font).Width
                    <= column.Width - grid.ColumnHeadersDefaultCellStyle.Padding.Horizontal,
                    $"{grid.Name}/{column.HeaderText} cuts off the word {word}: width {column.Width}, minimum {column.MinimumWidth}, padding {grid.ColumnHeadersDefaultCellStyle.Padding.Horizontal}, text {TextRenderer.MeasureText(word, grid.ColumnHeadersDefaultCellStyle.Font ?? grid.Font).Width}.");
            right = Math.Max(right, bounds.Right);
        }
        Assert.True(grid.ClientSize.Width - right <= SystemInformation.VerticalScrollBarWidth + 2);
        Assert.Equal(DataGridViewColumnHeadersHeightSizeMode.AutoSize, grid.ColumnHeadersHeightSizeMode);
        Assert.Equal(DataGridViewTriState.True, grid.ColumnHeadersDefaultCellStyle.WrapMode);
    }

    private static void AssertWrappedRowFits(DataGridView grid)
    {
        Assert.Equal(1, grid.RowCount);
        Assert.Equal(DataGridViewTriState.True, grid.DefaultCellStyle.WrapMode);
        int preferred = grid.Rows[0].GetPreferredHeight(0, DataGridViewAutoSizeRowMode.AllCells, true);
        Assert.True(grid.Rows[0].Height >= preferred, $"{grid.Name} row is clipped: {grid.Rows[0].Height}px, needs {preferred}px.");
    }

    private static void AssertFullFirstRowVisible(DataGridView grid)
    {
        Rectangle row = grid.GetRowDisplayRectangle(0, false);
        Assert.True(row.Bottom <= grid.ClientSize.Height, $"{grid.Name} first row is clipped by its viewport: {row}, client {grid.ClientSize}.");
    }

    private static DtcAnalysisResult EmptyResult() => new([], [], [], []);

    private sealed class TestLogFolder : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "loger-dtc-ui-" + Guid.NewGuid().ToString("N"));

        public TestLogFolder()
        {
            Directory.CreateDirectory(Path);
            File.WriteAllText(System.IO.Path.Combine(Path, "fixture.trc"), "1 0.000 Rx 18FE2503 8 00 00 00 00 00 00 00 00");
        }

        public void Dispose()
        {
            File.Delete(System.IO.Path.Combine(Path, "fixture.trc"));
            Directory.Delete(Path);
        }
    }

    private static void Capture(Form form, string name)
    {
        string? directory = Environment.GetEnvironmentVariable("LOGER_DTC_UI_ARTIFACT_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
        bitmap.Save(Path.Combine(directory, name + ".png"), System.Drawing.Imaging.ImageFormat.Png);
    }

    private static void AssertGridTheme(DataGridView grid)
    {
        Assert.Equal(ThemeManager.Current.Surface, grid.BackgroundColor);
        Assert.Equal(ThemeManager.Current.Surface, grid.DefaultCellStyle.BackColor);
        Assert.Equal(ThemeManager.Current.Text, grid.DefaultCellStyle.ForeColor);
        Assert.Equal(ThemeManager.Current.SurfaceAlt, grid.ColumnHeadersDefaultCellStyle.BackColor);
        Assert.False(grid.EnableHeadersVisualStyles);
    }

    private static IEnumerable<Control> Descendants(Control control)
        => control.Controls.Cast<Control>().SelectMany(child => new[] { child }.Concat(Descendants(child)));

    private static IEnumerable<Control> Ancestors(Control control)
    {
        for (Control? parent = control.Parent; parent != null; parent = parent.Parent) yield return parent;
    }

    private static Control Find(Control root, string name) => Descendants(root).Single(control => control.Name == name);

    private static void AssertVisibleBounds(Control control)
    {
        Assert.True(control.Visible, $"{control.Name} ({control.Text}) is hidden.");
        Assert.True(control.Width > 0 && control.Height > 0, $"{control.Name} has no usable bounds: {control.Bounds}.");
        Rectangle bounds = control.RectangleToScreen(control.ClientRectangle);
        foreach (Control parent in Ancestors(control))
        {
            Rectangle available = parent.RectangleToScreen(parent.ClientRectangle);
            available.Inflate(2, 2); // Native borders can round by one device pixel.
            Assert.True(available.Contains(bounds), $"{control.Name} ({bounds}) is clipped by {parent.GetType().Name} {parent.Name} ({available}).");
        }
    }

    private static void AssertSeparate(params Control[] controls)
    {
        for (int i = 0; i < controls.Length; i++)
            for (int j = i + 1; j < controls.Length; j++)
            {
                Rectangle first = controls[i].RectangleToScreen(controls[i].ClientRectangle);
                Rectangle second = controls[j].RectangleToScreen(controls[j].ClientRectangle);
                Assert.False(first.IntersectsWith(second), $"{controls[i].Name} overlaps {controls[j].Name}: {first} / {second}.");
            }
    }

    private static void Layout(Control form)
    {
        form.PerformLayout();
        Application.DoEvents();
        form.PerformLayout();
        Application.DoEvents();
    }

    private static T Field<T>(object instance, string name)
        => (T)instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;

    private static object? Invoke(object instance, string name, params object?[] arguments)
        => instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(instance, arguments);

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
