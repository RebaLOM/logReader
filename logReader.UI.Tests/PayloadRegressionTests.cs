using logReader.UI.Theme;

namespace logReader.UI.Tests;

public class PayloadRegressionTests
{
    [Fact]
    public void Same_coordinates_repaint_different_bits_when_byte_order_changes() => UiThread.Run(() =>
    {
        using var grid = new CanPayloadGridControl { Mode = CanPayloadGridMode.Edit, ShowLegend = false };
        grid.SetSelectionFromFields(1, 1, 4, littleEndian: true, fireEvent: false);
        Assert.Equal(new[] { 9, 10, 11, 12 }, SelectionBits(grid));
        Assert.Equal(AppTheme.PrimarySoft.ToArgb(), PaintBit(grid, 10));
        Assert.Equal(AppTheme.SurfaceSecondary.ToArgb(), PaintBit(grid, 23));
        grid.SetSelectionFromFields(1, 1, 4, littleEndian: false, fireEvent: false);
        Assert.Equal(9, grid.SelectionStartBit);
        Assert.Equal(4, grid.SelectionLength);
        Assert.Equal(new[] { 8, 9, 22, 23 }, SelectionBits(grid));
        Assert.Equal(AppTheme.SurfaceSecondary.ToArgb(), PaintBit(grid, 10));
        Assert.Equal(AppTheme.PrimarySoft.ToArgb(), PaintBit(grid, 23));
        grid.IsLittleEndian = true;
        Assert.Equal(new[] { 9, 10, 11, 12 }, SelectionBits(grid));
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Editors_synchronize_byte_order_and_selection_in_both_directions(bool xlsx) => UiThread.Run(() =>
    {
        using Form form = xlsx
            ? new DeviceFieldRowEditForm(UiFixtures.FieldRow, 8, 0)
            : new DbcSignalEditForm(UiFixtures.Signal, 8);
        UiThread.Show(form);
        var grid = UiThread.Field<CanPayloadGridControl>(form, "_payloadGrid");
        UiThread.Field<RadioButton>(form, "_rbMotorola").Checked = true;
        Assert.False(grid.IsLittleEndian);
        Assert.Equal(1m, UiThread.Field<NumericUpDown>(form, "_numByteIndex").Value);
        Assert.Equal(1m, UiThread.Field<NumericUpDown>(form, "_numStartBitInByte").Value);
        Assert.Equal(4m, UiThread.Field<NumericUpDown>(form, "_numLength").Value);
        Assert.Equal(new[] { 8, 9, 22, 23 }, SelectionBits(grid));
        Assert.Contains("Motorola", UiThread.Field<Label>(form, "_selectionSummary").Text);
        UiThread.Field<RadioButton>(form, "_rbIntel").Checked = true;
        Assert.Equal(new[] { 9, 10, 11, 12 }, SelectionBits(grid));
        grid.SetSelection(18, 3, fireEvent: true);
        Assert.Equal(2m, UiThread.Field<NumericUpDown>(form, "_numByteIndex").Value);
        Assert.Equal(2m, UiThread.Field<NumericUpDown>(form, "_numStartBitInByte").Value);
        Assert.Equal(3m, UiThread.Field<NumericUpDown>(form, "_numLength").Value);
    });

    [Fact]
    public void Payload_keyboard_edits_selection_and_view_enter_keeps_overlay_action() => UiThread.Run(() =>
    {
        using var host = new Form { ClientSize = new Size(420, 400) };
        var grid = new CanPayloadGridControl { Mode = CanPayloadGridMode.Edit, ShowLegend = false, Location = new Point(16, 16) };
        host.Controls.Add(grid);
        UiThread.Show(host);
        grid.Focus();
        Assert.True(grid.Focused);
        Assert.True(grid.TabStop);
        grid.SetSelection(10, 3, fireEvent: false);
        int changed = 0;
        grid.SelectionChanged += (_, _) => changed++;
        foreach (var (key, start, length) in new[]
        {
            (Keys.Left, 11, 3), (Keys.Shift | Keys.Right, 11, 4),
            (Keys.Down, 19, 4), (Keys.Shift | Keys.Left, 19, 3)
        })
        {
            var e = new KeyEventArgs(key);
            UiThread.Invoke(grid, "OnKeyDown", e);
            Assert.True(e.Handled);
            Assert.True(e.SuppressKeyPress);
            Assert.Equal(start, grid.SelectionStartBit);
            Assert.Equal(length, grid.SelectionLength);
        }
        Assert.Equal(4, changed);
        grid.Mode = CanPayloadGridMode.View;
        grid.Overlays = [new("Speed", 0, 8, true, AppTheme.Primary)];
        Assert.Equal(true, UiThread.Invoke(grid, "IsInputKey", Keys.Enter));
        string? selected = null;
        grid.OverlaySelected += (_, e) => selected = e.OverlayName;
        UiThread.Invoke(grid, "OnKeyDown", new KeyEventArgs(Keys.Enter));
        Assert.Equal("Speed", selected);
    });

    private static int[] SelectionBits(CanPayloadGridControl grid) =>
        UiThread.Field<HashSet<int>>(grid, "_selectionBits").OrderBy(bit => bit).ToArray();

    private static int PaintBit(CanPayloadGridControl grid, int bit)
    {
        using var bitmap = new Bitmap(grid.Width, grid.Height);
        using var graphics = Graphics.FromImage(bitmap);
        UiThread.Invoke(grid, "OnPaint", new PaintEventArgs(graphics, grid.ClientRectangle));
        var rect = (Rectangle)UiThread.Invoke(grid, "CellRect", bit / 8, 7 - bit % 8)!;
        return bitmap.GetPixel(rect.Left + 4, rect.Top + 4).ToArgb();
    }
}
