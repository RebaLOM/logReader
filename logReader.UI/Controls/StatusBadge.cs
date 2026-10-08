using System.ComponentModel;
using logReader.UI.Theme;

namespace logReader.UI.Controls;

public enum StatusBadgeKind
{
    Neutral,
    Success,
    Warning,
    Error,
    Info,
}

public class StatusBadge : Label
{
    private StatusBadgeKind _kind = StatusBadgeKind.Neutral;

    public StatusBadge()
    {
        AutoSize = false;
        AutoEllipsis = true;
        Font = Typography.Caption();
        TextAlign = ContentAlignment.MiddleLeft;
        Padding = new Padding(Spacing.Xs, 0, Spacing.Xs, 0);
        Height = 24;
    }

    [DefaultValue(StatusBadgeKind.Neutral)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public StatusBadgeKind Kind
    {
        get => _kind;
        set
        {
            _kind = value;
            ApplyTheme(AppTheme.Palette);
        }
    }

    public void ApplyTheme(ThemePalette p)
    {
        ForeColor = _kind switch
        {
            StatusBadgeKind.Success => p.Success,
            StatusBadgeKind.Warning => p.Warning,
            StatusBadgeKind.Error => p.Error,
            StatusBadgeKind.Info => p.Info,
            _ => p.Muted,
        };
        BackColor = Color.Transparent;
    }
}
