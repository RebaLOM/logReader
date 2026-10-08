using System.Runtime.CompilerServices;

namespace logReader.UI.Theme;

public static class AppTheme
{
    public static Color Background { get; } = Color.FromArgb(244, 246, 250);
    public static Color Surface { get; } = Color.White;
    public static Color SurfaceSecondary { get; } = Color.FromArgb(248, 250, 252);
    public static Color Border { get; } = Color.FromArgb(226, 232, 240);
    public static Color BorderHover { get; } = Color.FromArgb(148, 163, 184);
    public static Color Primary { get; } = Color.FromArgb(37, 99, 235);
    public static Color PrimaryHover { get; } = Color.FromArgb(29, 78, 216);
    public static Color PrimaryPressed { get; } = Color.FromArgb(30, 64, 175);
    public static Color PrimarySoft { get; } = Color.FromArgb(239, 246, 255);
    public static Color TextPrimary { get; } = Color.FromArgb(23, 32, 51);
    public static Color TextSecondary { get; } = Color.FromArgb(82, 96, 120);
    public static Color TextMuted { get; } = Color.FromArgb(124, 135, 154);
    public static Color Success { get; } = Color.FromArgb(21, 128, 61);
    public static Color Warning { get; } = Color.FromArgb(180, 83, 9);
    public static Color Error { get; } = Color.FromArgb(185, 28, 28);
    public static Color Info => Primary;
    public static Color SuccessSoft { get; } = Color.FromArgb(240, 253, 244);
    public static Color WarningSoft { get; } = Color.FromArgb(255, 251, 235);
    public static Color ErrorSoft { get; } = Color.FromArgb(254, 242, 242);
    public static Color DisabledSurface { get; } = Color.FromArgb(241, 245, 249);
    public static Color PayloadEmpty => SurfaceSecondary;
    public static Color PayloadConflict => Error;
    public static IReadOnlyList<Color> PayloadColors { get; } = Array.AsReadOnly(new[]
    {
        Color.FromArgb(91, 141, 239), Color.FromArgb(246, 153, 63),
        Color.FromArgb(87, 187, 138), Color.FromArgb(214, 96, 150),
        Color.FromArgb(168, 118, 220), Color.FromArgb(60, 170, 193),
        Color.FromArgb(220, 176, 74), Color.FromArgb(140, 150, 165),
        Color.FromArgb(111, 183, 128), Color.FromArgb(196, 120, 90),
        Color.FromArgb(120, 145, 210), Color.FromArgb(175, 130, 190),
        Color.FromArgb(100, 175, 160), Color.FromArgb(210, 140, 120),
        Color.FromArgb(130, 160, 100), Color.FromArgb(170, 110, 110)
    });

    private static readonly ConditionalWeakTable<DataGridView, GridAppearance> GridStates = new();

    public static void Apply(Form form)
    {
        form.SuspendLayout();
        form.BackColor = Background;
        form.Font = Typography.Body;
        if (IsDefaultText(form.ForeColor)) form.ForeColor = TextPrimary;
        ApplyChildren(form);
        form.ResumeLayout(true);
        WindowSizing.Prepare(form);
    }

    private static bool IsDefaultText(Color color) =>
        color == SystemColors.ControlText || color == SystemColors.WindowText || color == Color.Black;

    private static void ApplyChildren(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            // Fonts and semantic colours explicitly assigned by a screen stay intact.
            if (child.Font.SizeInPoints < 9 && child.Font.Style == FontStyle.Regular)
                child.Font = Typography.Body;
            if (IsDefaultText(child.ForeColor)) child.ForeColor = TextPrimary;
            if (child is DataGridView grid) StyleGrid(grid);
            else if (child is TextBoxBase text)
            {
                text.BackColor = !text.Enabled ? DisabledSurface : text is IInputValidation { HasError: true } ? ErrorSoft : Surface;
                if (text is TextBox field && field.BorderStyle == BorderStyle.Fixed3D)
                    field.BorderStyle = BorderStyle.FixedSingle;
            }
            else if (child is ComboBox combo)
            {
                combo.BackColor = !combo.Enabled ? DisabledSurface : combo is IInputValidation { HasError: true } ? ErrorSoft : Surface;
                combo.FlatStyle = FlatStyle.Flat;
            }
            else if (child is NumericUpDown number)
            {
                number.BackColor = !number.Enabled ? DisabledSurface : number is IInputValidation { HasError: true } ? ErrorSoft : Surface;
                if (!UiFactory.IsHostedInput(number)) number.BorderStyle = BorderStyle.FixedSingle;
            }
            else if (child is Button button && child is not ModernButton)
            {
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderColor = Border;
                button.FlatAppearance.BorderSize = 1;
                button.FlatAppearance.MouseOverBackColor = SurfaceSecondary;
                button.FlatAppearance.MouseDownBackColor = PrimarySoft;
                button.BackColor = Surface;
                button.Font = Typography.Button;
                button.UseVisualStyleBackColor = false;
            }
            else if (child is GroupBox)
            {
                child.Font = Typography.CardTitle;
                child.BackColor = parent.BackColor;
            }
            else if (child is TabPage) child.BackColor = Background;
            else if (child is Panel && child is not ModernCard && child is not InlineNotice &&
                     child.BackColor == SystemColors.Control) child.BackColor = parent.BackColor;
            ApplyChildren(child);
        }
    }

    public static void StyleGrid(DataGridView grid, string emptyMessage = "Пока нет данных")
    {
        grid.BackgroundColor = Surface;
        grid.BorderStyle = BorderStyle.None;
        grid.EnableHeadersVisualStyles = false;
        grid.GridColor = Border;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.ColumnHeadersHeight = UiScale.Px(grid, 40);
        grid.ColumnHeadersDefaultCellStyle.BackColor = SurfaceSecondary;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = TextSecondary;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = SurfaceSecondary;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = TextPrimary;
        grid.ColumnHeadersDefaultCellStyle.Font = Typography.Caption;
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(UiScale.Px(grid, 10), 0, UiScale.Px(grid, 8), 0);
        grid.RowHeadersDefaultCellStyle.BackColor = SurfaceSecondary;
        grid.RowHeadersDefaultCellStyle.ForeColor = TextMuted;
        grid.DefaultCellStyle.BackColor = Surface;
        grid.DefaultCellStyle.ForeColor = TextPrimary;
        grid.DefaultCellStyle.SelectionBackColor = PrimarySoft;
        grid.DefaultCellStyle.SelectionForeColor = TextPrimary;
        grid.DefaultCellStyle.Font = Typography.Secondary;
        grid.DefaultCellStyle.Padding = new Padding(UiScale.Px(grid, 10), UiScale.Px(grid, 4), UiScale.Px(grid, 8), UiScale.Px(grid, 4));
        grid.RowTemplate.Height = UiScale.Px(grid, 36);
        foreach (DataGridViewRow row in grid.Rows)
            if (!row.IsNewRow) row.Height = UiScale.Px(grid, 36);
        var appearance = GridStates.GetValue(grid, g => new GridAppearance(g));
        if (emptyMessage != "Пока нет данных" || appearance.EmptyMessage == "Пока нет данных")
            appearance.EmptyMessage = emptyMessage;
        grid.Invalidate();
    }

    private sealed class GridAppearance
    {
        private readonly DataGridView grid;
        private int hoverRow = -1;
        public string EmptyMessage { get; set; } = "Пока нет данных";
        public GridAppearance(DataGridView owner)
        {
            grid = owner;
            grid.CellMouseEnter += (_, e) => SetHover(e.RowIndex);
            grid.MouseLeave += (_, _) => SetHover(-1);
            grid.CellPainting += PaintCell;
            grid.Paint += PaintEmpty;
            grid.DpiChangedAfterParent += (_, _) => StyleGrid(grid, EmptyMessage);
            grid.RowsAdded += (_, _) => grid.Invalidate();
            grid.RowsRemoved += (_, _) => grid.Invalidate();
        }
        private void SetHover(int index)
        {
            if (index == hoverRow) return;
            int old = hoverRow;
            hoverRow = index;
            if (old >= 0 && old < grid.RowCount) grid.InvalidateRow(old);
            if (index >= 0 && index < grid.RowCount) grid.InvalidateRow(index);
        }
        private void PaintCell(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            bool selected = (e.State & DataGridViewElementStates.Selected) != 0;
            if (e.RowIndex != hoverRow || selected || e.CellStyle?.BackColor != Surface) return;
            using var brush = new SolidBrush(SurfaceSecondary);
            e.Graphics?.FillRectangle(brush, e.CellBounds);
            e.Paint(e.ClipBounds, DataGridViewPaintParts.All & ~(DataGridViewPaintParts.Background | DataGridViewPaintParts.ContentBackground));
            e.Handled = true;
        }
        private void PaintEmpty(object? sender, PaintEventArgs e)
        {
            if (grid.Rows.Cast<DataGridViewRow>().Any(row => !row.IsNewRow)) return;
            Rectangle area = grid.ClientRectangle;
            area.Y += grid.ColumnHeadersVisible ? grid.ColumnHeadersHeight : 0;
            area.Height -= grid.ColumnHeadersVisible ? grid.ColumnHeadersHeight : 0;
            if (area.Height < UiScale.Px(grid, 64)) return;
            int iconSize = UiScale.Px(grid, 30);
            int cy = area.Top + area.Height / 2;
            IconPainter.Draw(e.Graphics, IconKind.Table,
                new Rectangle(area.Left + (area.Width - iconSize) / 2, cy - UiScale.Px(grid, 32), iconSize, iconSize), TextMuted);
            int margin = UiScale.Px(grid, 16);
            var textBounds = new Rectangle(area.Left + margin, cy + UiScale.Px(grid, 8), Math.Max(1, area.Width - margin * 2), UiScale.Px(grid, 44));
            TextRenderer.DrawText(e.Graphics, EmptyMessage, Typography.Secondary, textBounds, TextMuted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        }
    }
}
