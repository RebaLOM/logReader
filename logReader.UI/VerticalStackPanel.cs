using System.Windows.Forms.Layout;

namespace logReader.UI;

// A lightweight one-column layout for the workspace vertical groups.
internal sealed class VerticalStackPanel : Panel
{
    private static readonly LayoutEngine Engine = new VerticalLayoutEngine();
    private readonly List<Control> _layoutChildren = new();
    private bool _arranging;
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Func<Control, bool>? IncludeChild { get; set; }
    public override LayoutEngine LayoutEngine => Engine;

    public override Size GetPreferredSize(Size proposedSize)
    {
        int width = proposedSize.Width > 0 && proposedSize.Width < int.MaxValue
            ? proposedSize.Width : Math.Max(1, Width);
        return new Size(width, MeasureHeight(width, false));
    }

    private int MeasureHeight(int width, bool arrange)
    {
        int top = Padding.Top;
        foreach (Control child in _layoutChildren)
        {
            if (IncludeChild != null && !IncludeChild(child)) continue;
            int available = Math.Max(1, width - Padding.Horizontal - child.Margin.Horizontal);
            int height = child.AutoSize ? child.GetPreferredSize(new Size(available, 0)).Height : child.Height;
            height = Math.Max(child.MinimumSize.Height, height);
            if (child.MaximumSize.Height > 0) height = Math.Min(child.MaximumSize.Height, height);
            top += child.Margin.Top;
            if (arrange)
            {
                child.SetBounds(Padding.Left + child.Margin.Left, top, available, height, BoundsSpecified.All);
                // A card can reduce its own height when its wrapped body receives this width.
                height = child.Height;
            }
            top += height + child.Margin.Bottom;
        }
        return top + Padding.Bottom;
    }

    protected override void OnControlAdded(ControlEventArgs e)
    {
        if (e.Control != null) _layoutChildren.Add(e.Control);
        base.OnControlAdded(e);
    }

    protected override void OnControlRemoved(ControlEventArgs e)
    {
        if (e.Control != null) _layoutChildren.Remove(e.Control);
        base.OnControlRemoved(e);
    }

    private void ArrangeChildren()
    {
        if (_arranging) return;
        _arranging = true;
        try
        {
            int height = MeasureHeight(ClientSize.Width, true);
            if (AutoSize && Height != height)
                SetBounds(Left, Top, Width, height, BoundsSpecified.Height);
        }
        finally { _arranging = false; }
    }

    private sealed class VerticalLayoutEngine : LayoutEngine
    {
        public override bool Layout(object container, LayoutEventArgs layoutEventArgs)
        {
            ((VerticalStackPanel)container).ArrangeChildren();
            return false;
        }
    }
}
