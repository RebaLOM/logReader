using System.ComponentModel;

namespace logReader.UI.Controls;

public class EmptyState : UserControl
{
    private readonly IconView iconView;
    private readonly Label titleLabel;
    private readonly Label descriptionLabel;
    private TableLayoutPanel? layout;
    [DefaultValue("Пока нет данных")]
    public string Title { get => titleLabel.Text; set { titleLabel.Text = value; AccessibleName = value; } }
    [DefaultValue("Выберите файл, чтобы начать работу.")]
    public string Description { get => descriptionLabel.Text; set { descriptionLabel.Text = value; AccessibleDescription = value; } }
    [DefaultValue(IconKind.File)]
    public IconKind Icon { get => iconView.Icon; set => iconView.Icon = value; }

    public EmptyState()
    {
        BackColor = AppTheme.Surface;
        MinimumSize = new Size(160, 0);
        TabStop = false;
        AccessibleRole = AccessibleRole.StaticText;
        iconView = new IconView { Icon = IconKind.File, ForeColor = AppTheme.TextMuted, Size = new Size(36, 36), Anchor = AnchorStyles.None };
        titleLabel = new Label { Text = "Пока нет данных", Font = Typography.CardTitle, ForeColor = AppTheme.TextPrimary,
            Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, AutoEllipsis = true };
        descriptionLabel = new Label { Text = "Выберите файл, чтобы начать работу.", Font = Typography.Secondary, ForeColor = AppTheme.TextSecondary,
            Dock = DockStyle.Fill, TextAlign = ContentAlignment.TopCenter };
        layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(20), BackColor = AppTheme.Surface };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        layout.Controls.Add(iconView, 0, 1);
        layout.Controls.Add(titleLabel, 0, 2);
        layout.Controls.Add(descriptionLabel, 0, 3);
        descriptionLabel.AutoSize = true;
        Controls.Add(layout);
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        if (layout != null)
        {
            bool compact = Height < UiScale.Px(this, 160);
            var padding = new Padding(UiScale.Px(this, compact ? 8 : 20));
            if (layout.Padding != padding) layout.Padding = padding;
            iconView.Visible = !compact && Icon != IconKind.None;
            float iconHeight = compact ? 0 : UiScale.Px(this, 48);
            if (layout.RowStyles[1].Height != iconHeight) layout.RowStyles[1].Height = iconHeight;
            descriptionLabel.Visible = Height >= UiScale.Px(this, 72);
        }
        base.OnLayout(e);
    }
}

public class IconView : Control
{
    private IconKind icon;
    [DefaultValue(IconKind.None)]
    public IconKind Icon { get => icon; set { icon = value; Invalidate(); } }
    public IconView()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.SupportsTransparentBackColor | ControlStyles.ResizeRedraw, true);
        BackColor = Color.Transparent;
        ForeColor = AppTheme.TextSecondary;
        Size = new Size(24, 24);
        TabStop = false;
        AccessibleRole = AccessibleRole.Graphic;
    }
    protected override void OnPaint(PaintEventArgs e) => IconPainter.Draw(e.Graphics, Icon, ClientRectangle, ForeColor);
}
