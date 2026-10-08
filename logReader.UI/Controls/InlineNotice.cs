using logReader.UI.Theme;

namespace logReader.UI.Controls;

// Статусный/hint-текст с muted цветом из палитры.
public class InlineNotice : Label
{
    public InlineNotice()
    {
        AutoSize = false;
        AutoEllipsis = true;
        Font = Typography.Caption();
        ForeColor = AppTheme.Palette.Muted;
        BackColor = Color.Transparent;
    }

    public void ApplyTheme(ThemePalette p)
    {
        ForeColor = p.Muted;
        BackColor = Color.Transparent;
    }
}
