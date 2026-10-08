using System.ComponentModel;
using logReader.UI.Theme;

namespace logReader.UI.Controls;

public class EmptyState : Panel
{
    private readonly Label _title;
    private readonly Label _body;

    public EmptyState()
    {
        DoubleBuffered = true;
        Tag = ThemeTags.Surface;

        _title = new Label
        {
            AutoSize = true,
            Font = Typography.Section(),
            Tag = ThemeTags.Brand,
        };
        _body = new Label
        {
            AutoSize = true,
            Font = Typography.Secondary(),
            Tag = ThemeTags.Muted,
            MaximumSize = new Size(360, 0),
        };

        Controls.Add(_title);
        Controls.Add(_body);
        Resize += (_, _) => LayoutChildren();
    }

    [DefaultValue("")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public string TitleText
    {
        get => _title.Text;
        set { _title.Text = value; LayoutChildren(); }
    }

    [DefaultValue("")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public string BodyText
    {
        get => _body.Text;
        set { _body.Text = value; LayoutChildren(); }
    }

    private void LayoutChildren()
    {
        int x = Math.Max(Spacing.Md, (Width - 360) / 2);
        _title.Location = new Point(x, Math.Max(Spacing.Xl, Height / 2 - 28));
        _body.Location = new Point(x, _title.Bottom + Spacing.Xs);
        _body.MaximumSize = new Size(Math.Max(200, Width - x * 2), 0);
    }

    public void ApplyTheme(ThemePalette p)
    {
        BackColor = p.Surface;
        _title.ForeColor = p.Text;
        _body.ForeColor = p.Muted;
    }
}
