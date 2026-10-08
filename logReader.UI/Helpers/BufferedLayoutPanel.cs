namespace logReader.UI.Helpers;

/// <summary>Buffers managed layout surfaces without compositing native child windows.</summary>
internal sealed class BufferedTableLayoutPanel : TableLayoutPanel
{
    public BufferedTableLayoutPanel() => DoubleBuffered = true;
}

internal sealed class BufferedPanel : Panel
{
    public BufferedPanel() => DoubleBuffered = true;
}
