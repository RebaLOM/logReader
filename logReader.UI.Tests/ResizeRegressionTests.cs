using logReader.UI.Helpers;

namespace logReader.UI.Tests;

public class ResizeRegressionTests
{
    [Fact]
    public void Resize_bursts_commit_latest_geometry_once_and_disposal_discards_pending_work() => UiThread.Run(() =>
    {
        using var host = new Form { ClientSize = new Size(400, 260) };
        UiThread.Show(host);
        int layoutPasses = 0;
        Size lastLayout = Size.Empty;
        using var coordinator = new ResizeCoordinator(host, () =>
        {
            layoutPasses++;
            lastLayout = host.ClientSize;
        });
        host.SizeChanged += (_, _) => coordinator.Request();
        for (int width = 401; width <= 440; width++) host.ClientSize = new Size(width, 260);
        Assert.Equal(0, layoutPasses);
        coordinator.Flush();
        Assert.Equal(1, layoutPasses);
        Assert.Equal(host.ClientSize, lastLayout);
        coordinator.Flush();
        UiThread.Pump();
        Assert.Equal(1, layoutPasses);

        // Native drag events use the same coalescing contract, without relying
        // on wall-clock timing or the Windows runner's frame rate.
        UiThread.Invoke(host, "OnResizeBegin", EventArgs.Empty);
        for (int height = 261; height <= 300; height++) host.ClientSize = new Size(440, height);
        Assert.Equal(1, layoutPasses);
        UiThread.Invoke(host, "OnResizeEnd", EventArgs.Empty);
        Assert.Equal(2, layoutPasses);
        Assert.Equal(host.ClientSize, lastLayout);
        coordinator.Request();
        coordinator.Dispose();
        UiThread.Pump();
        Assert.Equal(2, layoutPasses);
    });

    [Fact]
    public void Main_height_resizes_keep_journal_height_values_and_cached_action_layout() => UiThread.Run(() =>
    {
        using var form = new MainForm();
        UiThread.Show(form);
        var input = UiThread.Field<TextBox>(form, "textBoxCanLog");
        input.Text = @"C:\missing-fixture\do-not-rewrite.trc";
        var split = UiThread.Field<SplitContainer>(form, "contentSplit");
        var coordinator = UiThread.Field<ResizeCoordinator>(form, "_workspaceResize");
        coordinator.Flush();
        UiThread.Pump();
        int desiredDistance = Math.Clamp(split.Height - split.SplitterWidth - 140,
            split.Panel1MinSize, split.Height - split.SplitterWidth - split.Panel2MinSize);
        split.SplitterDistance = desiredDistance;
        UiThread.Pump();
        int journalHeight = split.Panel2.Height;
        var actionBar = UiThread.Field<TableLayoutPanel>(form, "_actionBar");
        var readiness = UiThread.Field<Label>(form, "_readinessLabel");
        var columns = actionBar.ColumnStyles.Cast<ColumnStyle>().Select(style => (style.SizeType, style.Width)).ToArray();
        Size labelConstraint = readiness.MaximumSize;
        int measuredWidth = UiThread.Field<int>(form, "_naturalActionsWidth");
        int baselineHeight = form.Height;
        for (int pass = 0; pass < 20; pass++)
        {
            form.Height = baselineHeight + (pass % 2 == 0 ? 16 : 32);
            Assert.InRange(Math.Abs(split.Panel2.Height - journalHeight), 0, 1);
        }
        coordinator.Flush();
        UiThread.Pump();
        Assert.InRange(Math.Abs(split.Panel2.Height - journalHeight), 0, 1);
        Assert.Equal(@"C:\missing-fixture\do-not-rewrite.trc", input.Text);
        Assert.Equal(measuredWidth, UiThread.Field<int>(form, "_naturalActionsWidth"));
        Assert.Equal(columns, actionBar.ColumnStyles.Cast<ColumnStyle>().Select(style => (style.SizeType, style.Width)).ToArray());
        Assert.Equal(labelConstraint, readiness.MaximumSize);

        int redundantLayoutPasses = 0;
        actionBar.Layout += (_, _) => redundantLayoutPasses++;
        for (int pass = 0; pass < 40; pass++) UiThread.Invoke(form, "ReflowActionBar");
        Assert.Equal(0, redundantLayoutPasses);

        var process = UiThread.Field<Button>(form, "buttonProcess");
        process.Text = "Повторно обработать выбранные логи";
        coordinator.Flush();
        UiThread.Pump();
        Assert.True(UiThread.Field<int>(form, "_naturalActionsWidth") > measuredWidth);
        Assert.True(process.Width >= process.GetPreferredSize(Size.Empty).Width);
        UiThread.AssertFooterReachable(form, form.AcceptButton);
    });
}
