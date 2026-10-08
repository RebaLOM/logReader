using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing.Drawing2D;

namespace logReader.UI.Controls;

public class InlineNotice : UserControl
{
    private readonly Label message;
    private readonly IconView icon;
    private StatusTone tone = StatusTone.Info;
    [DefaultValue(StatusTone.Info)]
    public StatusTone Tone { get => tone; set { tone = value; UpdateTone(); } }
    [Browsable(true), DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    [AllowNull]
    public override string Text { get => message?.Text ?? base.Text; set { base.Text = value; if (message != null) { message.Text = value; AccessibleName = value; PerformLayout(); } } }

    public InlineNotice()
    {
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        MinimumSize = new Size(120, 44);
        Padding = new Padding(12);
        TabStop = false;
        AccessibleRole = AccessibleRole.StaticText;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 28));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        icon = new IconView { Icon = IconKind.Info, Size = new Size(18, 18), Anchor = AnchorStyles.Top | AnchorStyles.Left, Margin = new Padding(0, 2, 8, 0) };
        message = new Label { AutoSize = true, Dock = DockStyle.Fill, Font = Typography.Secondary, Margin = new Padding(0), TextAlign = ContentAlignment.TopLeft };
        layout.Controls.Add(icon, 0, 0);
        layout.Controls.Add(message, 1, 0);
        Controls.Add(layout);
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        UpdateTone();
    }
    private void UpdateTone()
    {
        var (ink, fill) = StatusBadge.Colors(Tone);
        BackColor = fill;
        if (message != null) message.ForeColor = ink;
        if (icon != null)
        {
            icon.ForeColor = ink;
            icon.Icon = Tone switch { StatusTone.Error or StatusTone.Warning => IconKind.Warning, StatusTone.Success => IconKind.Check, _ => IconKind.Info };
        }
        Invalidate();
    }
    protected override void OnLayout(LayoutEventArgs e)
    {
        if (message != null) message.MaximumSize = new Size(Math.Max(1, ClientSize.Width - Padding.Horizontal - UiScale.Px(this, 28)), 0);
        base.OnLayout(e);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(StatusBadge.Colors(Tone).Ink, Math.Max(1f, DeviceDpi / 96f));
        e.Graphics.DrawLine(pen, UiScale.Px(this, 1), UiScale.Px(this, 8), UiScale.Px(this, 1), Math.Max(UiScale.Px(this, 8), Height - UiScale.Px(this, 8)));
    }
}
