using System.ComponentModel;
using logReader.UI.Theme;

namespace logReader.UI.Controls;

public class ModernButton : Button
{
    private ButtonKind _kind = ButtonKind.Secondary;

    public ModernButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 1;
        UseVisualStyleBackColor = false;
        Cursor = Cursors.Hand;
        Font = Typography.Button();
        Height = 30;
        MinimumSize = new Size(72, 28);
    }

    [DefaultValue(ButtonKind.Secondary)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public ButtonKind Kind
    {
        get => _kind;
        set
        {
            _kind = value;
            if (IsHandleCreated)
                ApplyTheme(AppTheme.Palette);
        }
    }

    public void ApplyTheme(ThemePalette p)
    {
        FlatStyle = FlatStyle.Flat;
        UseVisualStyleBackColor = false;

        switch (_kind)
        {
            case ButtonKind.Primary:
                BackColor = p.Primary;
                ForeColor = p.OnPrimary;
                FlatAppearance.BorderColor = p.Primary;
                FlatAppearance.BorderSize = 0;
                FlatAppearance.MouseOverBackColor = p.PrimaryHover;
                FlatAppearance.MouseDownBackColor = p.PrimaryHover;
                break;
            case ButtonKind.Ghost:
                BackColor = p.Canvas;
                ForeColor = p.Muted;
                FlatAppearance.BorderColor = p.Border;
                FlatAppearance.BorderSize = 1;
                FlatAppearance.MouseOverBackColor = p.Elevated;
                FlatAppearance.MouseDownBackColor = p.Surface;
                break;
            case ButtonKind.Danger:
                BackColor = p.Error;
                ForeColor = Color.White;
                FlatAppearance.BorderColor = p.Error;
                FlatAppearance.BorderSize = 0;
                FlatAppearance.MouseOverBackColor = Color.FromArgb(
                    Math.Max(0, p.Error.R - 20),
                    Math.Max(0, p.Error.G - 20),
                    Math.Max(0, p.Error.B - 20));
                FlatAppearance.MouseDownBackColor = FlatAppearance.MouseOverBackColor;
                break;
            default:
                BackColor = p.Elevated;
                ForeColor = p.Text;
                FlatAppearance.BorderColor = p.Border;
                FlatAppearance.BorderSize = 1;
                FlatAppearance.MouseOverBackColor = p.Surface;
                FlatAppearance.MouseDownBackColor = p.Border;
                break;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyTheme(AppTheme.Palette);
    }
}
