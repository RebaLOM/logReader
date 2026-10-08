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

    private sealed class NavigationAccessibleObject(NavigationItem owner) : ButtonBaseAccessibleObject(owner)
    {
        public override AccessibleStates State => base.State | (owner.Selected ? AccessibleStates.Selected : AccessibleStates.None);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Selected && Enabled)
        {
            base.OnPaint(e);
            using var background = new SolidBrush(AppTheme.PrimarySoft);
            using var rounded = PaintGeometry.Rounded(new RectangleF(1, 1, Math.Max(1, Width - 3), Math.Max(1, Height - 3)), UiScale.Px(this, 8));
            e.Graphics.FillPath(background, rounded);
            using var stripe = new SolidBrush(AppTheme.Primary);
            e.Graphics.FillRectangle(stripe, UiScale.Px(this, 2), UiScale.Px(this, 12), UiScale.Px(this, 3), Math.Max(1, Height - UiScale.Px(this, 24)));
            int size = UiScale.Px(this, 18);
            int x = Padding.Left;
            if (Icon != IconKind.None)
            {
                IconPainter.Draw(e.Graphics, Icon, new Rectangle(x, (Height - size) / 2, size, size), AppTheme.Primary);
                x += size + UiScale.Px(this, 8);
            }
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(x, 0, Math.Max(1, Width - x - Padding.Right), Height), AppTheme.Primary,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if (Focused && ShowFocusCues)
                ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -UiScale.Px(this, 5), -UiScale.Px(this, 5)), AppTheme.Primary, AppTheme.PrimarySoft);
        }
        else base.OnPaint(e);
    }
}
