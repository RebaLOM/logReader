namespace logReader.UI;

// Decorative headings share the card surface; input controls retain their native behavior.
internal sealed class WorkspaceCardPanel : Panel
{
    private readonly string _title;
    private readonly string _eyebrow;
    private readonly Font _titleFont;
    private readonly Font _eyebrowFont;
    private readonly Control _body;
    private bool _sizing;

    public WorkspaceCardPanel(string title, string eyebrow, Font titleFont, Font eyebrowFont, Control body)
    {
        _title = title;
        _eyebrow = eyebrow;
        _titleFont = titleFont;
        _eyebrowFont = eyebrowFont;
        _body = body;
        Tag = "surface";
        Dock = DockStyle.Top;
        Margin = new Padding(0, 0, 0, 10);
        AccessibleRole = AccessibleRole.Grouping;
        AccessibleName = title;
        AccessibleDescription = eyebrow;
        Padding = new Padding(16, 16 + HeaderHeight(), 16, 16);
        body.Dock = DockStyle.Top;
        body.Margin = Padding.Empty;
        Controls.Add(body);
        body.SizeChanged += (_, _) => UpdateHeight();
        Height = body.PreferredSize.Height + Padding.Vertical;
    }

    private int HeaderHeight() => TextRenderer.MeasureText(_eyebrow, _eyebrowFont).Height + 4
        + TextRenderer.MeasureText(_title, _titleFont).Height + 12;

    private void UpdateHeight()
    {
        if (_sizing) return;
        _sizing = true;
        try { Height = _body.Height + Padding.Vertical; }
        finally { _sizing = false; }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        int y = LogicalToDeviceUnits(16);
        int width = Math.Max(1, ClientSize.Width - LogicalToDeviceUnits(32));
        int eyebrowHeight = TextRenderer.MeasureText(e.Graphics, _eyebrow, _eyebrowFont).Height;
        TextRenderer.DrawText(e.Graphics, _eyebrow, _eyebrowFont,
            new Rectangle(LogicalToDeviceUnits(16), y, width, eyebrowHeight), ThemeManager.Current.Muted,
            TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        y += eyebrowHeight + LogicalToDeviceUnits(4);
        TextRenderer.DrawText(e.Graphics, _title, _titleFont,
            new Rectangle(LogicalToDeviceUnits(16), y, width, Padding.Top - y), ThemeManager.Current.Text,
            TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
    }
}
