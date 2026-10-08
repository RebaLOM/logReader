using System.ComponentModel;

namespace logReader.UI.Controls;

public class NavigationItem : ModernButton
{
    private bool selected;
    [DefaultValue(false)]
    public bool Selected
    {
        get => selected;
        set
        {
            if (selected == value) return;
            selected = value;
            if (IsHandleCreated) AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
            Invalidate();
        }
    }

    public NavigationItem()
    {
        Variant = ButtonVariant.Ghost;
        TextAlign = ContentAlignment.MiddleLeft;
        Height = 44;
        Padding = new Padding(16, 0, 12, 0);
        Margin = new Padding(0, 0, 0, 4);
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new NavigationAccessibleObject(this);

    private sealed class NavigationAccessibleObject(NavigationItem owner) : ModernButtonAccessibleObject(owner)
    {
        public override AccessibleStates State => base.State | (owner.Selected ? AccessibleStates.Selected : AccessibleStates.None);
    }

    protected override (Color Fill, Color Ink, Color Border) GetAppearance()
    {
        if (!Selected || !Enabled) return base.GetAppearance();
        return (AppTheme.PrimarySoft, AppTheme.Primary,
            IsHovered || IsPressed ? AppTheme.BorderHover : AppTheme.PrimarySoft);
    }

    protected override void PaintAdornment(Graphics graphics, RectangleF bounds, Color ink)
    {
        if (!Selected) return;
        float inset = UiScale.Px(this, 8);
        float height = bounds.Height - inset * 2;
        if (height <= 0) return;
        using var stripe = new SolidBrush(Enabled ? AppTheme.Primary : AppTheme.TextMuted);
        graphics.FillRectangle(stripe, bounds.Left + UiScale.Px(this, 2), bounds.Top + inset,
            UiScale.Px(this, 3), height);
    }
}
