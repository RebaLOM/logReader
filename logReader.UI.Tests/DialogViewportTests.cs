using System.Reflection;
using System.Runtime.ExceptionServices;

namespace logReader.UI.Tests;

[Collection("DTC UI")]
public sealed class DialogViewportTests
{
    [Theory]
    [InlineData(1366, 728)]
    [InlineData(1280, 680)]
    [InlineData(1024, 720)]
    public void Export_options_scroll_on_short_screens_but_keep_apply_and_cancel_visible(int width, int height) => Run(() =>
    {
        using var form = new SaveOptionsForm(OutputFormat.CsvDstConnect, BatchOutputMode.PerInputFile,
            new DstConnectOptions(), Directory.GetCurrentDirectory(), LogFormatKind.All, dtcInclusion: DtcInclusionMode.Always);
        form.Show();
        DialogLayout.FitToWorkingArea(form, new Rectangle(0, 0, width, height));
        Pump(form);
        Assert.True(new Rectangle(0, 0, width, height).Contains(form.Bounds));
        var scroll = form.Controls.Find("saveOptionsScroll", true).Cast<Panel>().Single();
        Assert.True(scroll.AutoScroll);
        Assert.True(scroll.VerticalScroll.Visible, "Short screens need an accessible scrollable settings body.");
        Assert.False(scroll.HorizontalScroll.Visible, "Settings must fit the viewport width without a second scrollbar.");
        AssertVisible(form, Assert.IsType<Button>(form.AcceptButton));
        AssertVisible(form, Assert.IsType<Button>(form.CancelButton));
        foreach (string field in new[] { "_comboOutputFormat", "_numBlockPeriod", "_comboDtcInclusion", "_numDtcTimeout", "_formatsList" })
        {
            var control = (Control)typeof(SaveOptionsForm).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(form)!;
            // WinForms keeps the focused editor visible during layout. Move the
            // keyboard focus with the viewport, as actual Tab navigation does.
            Assert.True(control.Focus(), $"The setting {field} must remain reachable by keyboard.");
            scroll.ScrollControlIntoView(control);
            Point requestedScroll = scroll.AutoScrollPosition;
            Pump(form);
            Assert.Equal(requestedScroll, scroll.AutoScrollPosition);
            AssertVisible(form, control);
            AssertVisible(form, Assert.IsType<Button>(form.AcceptButton));
        }

        var body = form.Controls.Find("saveOptionsBody", true).Single();
        int expandedHeight = body.Height;
        var outputFormat = Field<ComboBox>(form, "_comboOutputFormat");
        outputFormat.SelectedIndex = 1;
        Pump(form);
        Assert.False(Field<Panel>(form, "_dstPanel").Visible);
        Assert.True(body.Height < expandedHeight, "Hiding DST options must remove their space from the scroll body.");
        var formats = Field<CheckedListBox>(form, "_formatsList");
        Assert.True(formats.Focus());
        scroll.ScrollControlIntoView(formats);
        Pump(form);
        AssertVisible(form, formats);
        Assert.False(scroll.HorizontalScroll.Visible);

        outputFormat.SelectedIndex = 2;
        Pump(form);
        Assert.Equal(expandedHeight, body.Height);
        var anchor = Field<NumericUpDown>(form, "_numBlockStart");
        Assert.True(anchor.Focus());
        scroll.ScrollControlIntoView(anchor);
        Pump(form);
        AssertVisible(form, anchor);
        Assert.False(scroll.HorizontalScroll.Visible);
        AssertVisible(form, Assert.IsType<Button>(form.AcceptButton));
        AssertVisible(form, Assert.IsType<Button>(form.CancelButton));
    });

    [Fact]
    public void Signal_color_column_reserves_the_whole_header() => Run(() =>
    {
        using var grid = new DataGridView();
        MessageEditFormHelpers.AddSignalColorColumn(grid);
        MessageEditFormHelpers.ApplySignalListColumnWeights(grid);
        var column = grid.Columns["Color"]!;
        int needed = TextRenderer.MeasureText(column.HeaderText, grid.ColumnHeadersDefaultCellStyle.Font ?? grid.Font).Width;
        Assert.True(column.Width >= needed + 8);
        Assert.Equal("Цвет", column.HeaderText);
        Assert.False(string.IsNullOrWhiteSpace(column.ToolTipText));
    });

    [Fact]
    public void Work_area_clamping_handles_negative_monitor_coordinates_and_oversized_minimum() => Run(() =>
    {
        using var form = new Form { Location = new Point(-2000, -100), Size = new Size(1900, 1100), MinimumSize = new Size(1800, 1000) };
        var monitor = new Rectangle(-1366, 0, 1366, 728);
        DialogLayout.FitToWorkingArea(form, monitor);
        Assert.True(monitor.Contains(form.Bounds));
        Assert.True(form.MinimumSize.Width <= monitor.Width);
        Assert.True(form.MinimumSize.Height <= monitor.Height);
    });

    private static void AssertVisible(Form form, Control control)
    {
        Rectangle bounds = control.RectangleToScreen(control.ClientRectangle);
        for (Control? parent = control.Parent; parent != null; parent = parent.Parent)
        {
            Rectangle available = parent.RectangleToScreen(parent.ClientRectangle);
            available.Inflate(2, 2);
            Assert.True(available.Contains(bounds), $"{control.GetType().Name} {control.Name} is clipped by {parent.GetType().Name}: {bounds} / {available}. " +
                (parent is ScrollableControl sc ? $"Scroll {sc.AutoScrollPosition}; display {sc.DisplayRectangle}; vertical {sc.VerticalScroll.Value}/{sc.VerticalScroll.Maximum}, enabled {sc.VerticalScroll.Enabled}; children {string.Join(", ", sc.Controls.Cast<Control>().Select(c => c.Bounds.ToString()))}." : ""));
        }
        Assert.True(form.Visible && control.Visible);
    }

    private static void Pump(Control form)
    {
        form.PerformLayout(); Application.DoEvents();
        form.PerformLayout(); Application.DoEvents();
    }

    private static T Field<T>(SaveOptionsForm form, string name)
        => (T)typeof(SaveOptionsForm).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(form)!;

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
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Dialog viewport test timed out.");
        failure?.Throw();
    }
}
