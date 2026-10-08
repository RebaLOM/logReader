namespace logReader.UI.Controls;

public class ModernTabControl : TabControl
{
    public ModernTabControl()
    {
        Font = Typography.Secondary;
        DrawMode = TabDrawMode.OwnerDrawFixed;
        Padding = new Point(18, 10);
        SizeMode = TabSizeMode.Normal;
        DrawItem += DrawTab;
    }
    private void DrawTab(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= TabPages.Count) return;
        bool selected = e.Index == SelectedIndex;
        using var background = new SolidBrush(selected ? AppTheme.Surface : AppTheme.Background);
        e.Graphics.FillRectangle(background, e.Bounds);
        TextRenderer.DrawText(e.Graphics, TabPages[e.Index].Text, selected ? Typography.Button : Typography.Secondary,
            e.Bounds, selected ? AppTheme.Primary : AppTheme.TextSecondary,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        if (selected)
        {
            using var accent = new SolidBrush(AppTheme.Primary);
            e.Graphics.FillRectangle(accent, e.Bounds.Left + UiScale.Px(this, 10), e.Bounds.Bottom - UiScale.Px(this, 3),
                Math.Max(1, e.Bounds.Width - UiScale.Px(this, 20)), UiScale.Px(this, 3));
        }
        if (Focused && ShowFocusCues && selected)
        {
            var focus = e.Bounds;
            focus.Inflate(-UiScale.Px(this, 4), -UiScale.Px(this, 4));
            ControlPaint.DrawFocusRectangle(e.Graphics, focus, AppTheme.Primary, AppTheme.Surface);
        }
    }
}
