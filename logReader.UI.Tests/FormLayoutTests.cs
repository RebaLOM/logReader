using logReader.UI.Controls;

namespace logReader.UI.Tests;

public class FormLayoutTests
{
    // Fourteen distinct forms, plus both other native device-file formats.
    [Theory]
    [InlineData("main")]
    [InlineData("devices-xlsx")]
    [InlineData("devices-dbc")]
    [InlineData("devices-dbf")]
    [InlineData("composites")]
    [InlineData("xlsx-message")]
    [InlineData("dbc-message")]
    [InlineData("dbc-signal")]
    [InlineData("xlsx-field")]
    [InlineData("composite-param")]
    [InlineData("save")]
    [InlineData("conversion")]
    [InlineData("file-kind")]
    [InlineData("device-selection")]
    [InlineData("log-view")]
    [InlineData("help")]
    public void Native_form_tree_loads_and_keeps_actions_at_minimum_size(string scenario) => UiThread.Run(() =>
    {
        using var fixture = new UiFixtures();
        using Form form = scenario switch
        {
            "main" => new MainForm(),
            "devices-xlsx" => new DevicesEditorForm(fixture.Xlsx),
            "devices-dbc" => new DevicesEditorForm(fixture.Dbc),
            "devices-dbf" => new DevicesEditorForm(fixture.Dbf),
            "composites" => new CompositeEditorForm(fixture.Composites),
            "xlsx-message" => new XlsxMessageEditForm(UiFixtures.Definition),
            "dbc-message" => new DbcMessageEditForm(UiFixtures.Message),
            "dbc-signal" => new DbcSignalEditForm(UiFixtures.Signal, 8),
            "xlsx-field" => new DeviceFieldRowEditForm(UiFixtures.FieldRow, 8, 0),
            "composite-param" => new CompositeParamEditForm(UiFixtures.Composite("Speed")),
            "save" => new SaveOptionsForm(OutputFormat.Csv, BatchOutputMode.PerInputFile, new(), fixture.LogFolder, LogFormatKind.All),
            "conversion" => new FormatConversionDialog([new("trc_to_asc", "TRC → ASC", ".trc", ".asc")], fixture.Trc, _ => { }),
            "file-kind" => new FileKindPromptForm(),
            "device-selection" => new Devices_ParametrsForm([UiFixtures.Device], new(), new()),
            "log-view" => new CanLogViewForm(fixture.Trc),
            "help" => new HelpForm(),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
        UiThread.Show(form);
        Assert.True(form.IsHandleCreated);
        Assert.Equal(AutoScaleMode.Dpi, form.AutoScaleMode);
        Assert.NotEmpty(UiThread.Descendants(form).OfType<ModernCard>());

        // Loading real fixtures also exercises each file-backed form's native
        // startup instead of silently testing only an empty constructor tree.
        if (form is DevicesEditorForm) Assert.Single(UiThread.Field<DataGridView>(form, "_grid").Rows.Cast<DataGridViewRow>());
        if (form is CompositeEditorForm) Assert.Equal(2, UiThread.Field<DataGridView>(form, "_grid").RowCount);
        if (form is CanLogViewForm)
        {
            UiThread.Until(() => UiThread.Field<TextBox>(form, "textBoxSearch").Enabled);
            Assert.Single(UiThread.Field<DataGridView>(form, "dataGridPackets").Rows.Cast<DataGridViewRow>());
        }
        if (form is HelpForm) Assert.NotEmpty(UiThread.Field<TreeView>(form, "treeViewTopics").Nodes.Cast<TreeNode>());
        if (form is Devices_ParametrsForm) Assert.True(UiThread.Field<Dictionary<string, bool>>(form, "_deviceEnabled")["123"]);

        form.Size = form.MinimumSize;
        UiThread.Pump();
        UiThread.AssertFooterReachable(form, form.AcceptButton);
        UiThread.AssertFooterReachable(form, form.CancelButton);
        UiThread.AssertVisibleButtonTextFits(form);
        foreach (var input in UiThread.Descendants(form).Where(c => c.Visible && c is TextBox or ComboBox or NumericUpDown))
            Assert.True(input.Width > 0 && input.Height > 0, $"{scenario}: {input.GetType().Name} collapsed after resizing.");
    });
}
