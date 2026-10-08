using System.Runtime.CompilerServices;

namespace logReader.UI.Theme;

public static class AppTheme
{
    public static ThemePalette CurrentPalette { get; private set; } = ThemePalette.Light;
    public static ThemeMode CurrentMode => CurrentPalette.Mode;
    public static string PreferenceFilePath => _preferencePath ?? ThemePreferences.DefaultPath;
    public static string? LastPersistenceError { get; private set; }
    public static event EventHandler? Changed;
    public static Color Background => CurrentPalette.Background;
    public static Color Surface => CurrentPalette.Surface;
    public static Color SurfaceSecondary => CurrentPalette.SurfaceSecondary;
    public static Color Border => CurrentPalette.Border;
    public static Color BorderHover => CurrentPalette.BorderHover;
    public static Color Primary => CurrentPalette.Primary;
    public static Color PrimaryHover => CurrentPalette.PrimaryHover;
    public static Color PrimaryPressed => CurrentPalette.PrimaryPressed;
    public static Color PrimarySoft => CurrentPalette.PrimarySoft;
    public static Color TextPrimary => CurrentPalette.TextPrimary;
    public static Color TextSecondary => CurrentPalette.TextSecondary;
    public static Color TextMuted => CurrentPalette.TextMuted;
    public static Color TextOnPrimary => CurrentPalette.TextOnPrimary;
    public static Color TextOnError => CurrentPalette.TextOnError;
    public static Color Success => CurrentPalette.Success;
    public static Color Warning => CurrentPalette.Warning;
    public static Color Error => CurrentPalette.Error;
    public static Color Info => CurrentPalette.Info;
    public static Color SuccessSoft => CurrentPalette.SuccessSoft;
    public static Color WarningSoft => CurrentPalette.WarningSoft;
    public static Color ErrorSoft => CurrentPalette.ErrorSoft;
    public static Color DisabledSurface => CurrentPalette.DisabledSurface;
    public static Color ReadOnlySurface => CurrentPalette.ReadOnlySurface;
    public static Color Canvas => CurrentPalette.Canvas;
    public static Color PayloadEmpty => SurfaceSecondary;
    public static Color PayloadConflict => Error;
    public static IReadOnlyList<Color> PayloadColors => CurrentPalette.PayloadColors;

    private static string? _preferencePath;
    private static int _revision = 1;
    private static readonly ConditionalWeakTable<Form, FormAppearance> FormStates = new();
    private static readonly ConditionalWeakTable<DataGridView, GridAppearance> GridStates = new();

    public static void Initialize(string? preferencePath = null)
    {
        _preferencePath = preferencePath;
        var mode = ThemePreferences.Load(PreferenceFilePath);
        LastPersistenceError = ThemePreferences.LastError;
        SetMode(mode, persist: false);
    }

    /// <summary>Call on the WinForms UI thread. Values, focus, data and form instances stay intact.</summary>
    public static bool SetMode(ThemeMode mode, bool persist = true)
    {
        var next = ThemePalette.For(mode);
        if (mode != CurrentMode)
        {
            var previous = CurrentPalette;
            CurrentPalette = next;
            _revision++;
            var forms = FormStates.Select(pair => pair.Key).Concat(Application.OpenForms.Cast<Form>()).Distinct().ToArray();
            foreach (var form in forms)
            {
                if (form.IsDisposed || form.Disposing) continue;
                ApplyPalette(form, previous, firstApply: false);
            }
            Changed?.Invoke(null, EventArgs.Empty);
        }
        if (!persist) return true;
        bool saved = ThemePreferences.Save(mode, PreferenceFilePath);
        LastPersistenceError = ThemePreferences.LastError;
        return saved;
    }

    public static void Apply(Form form)
    {
        var state = FormStates.GetValue(form, _ => new FormAppearance());
        if (state.Revision != _revision)
            ApplyPalette(form, ThemePalette.Light, firstApply: state.Revision == 0);
        WindowSizing.Prepare(form);
    }

    private static void ApplyPalette(Form form, ThemePalette previous, bool firstApply)
    {
        var state = FormStates.GetValue(form, _ => new FormAppearance());
        if (firstApply) form.Font = Typography.Body;
        form.BackColor = Background;
        form.ForeColor = TextPrimary;
        ApplyChildren(form, previous, firstApply);
        ThemeNative.Apply(form);
        state.Revision = _revision;
        form.Invalidate(true);
    }

    private sealed class FormAppearance { public int Revision { get; set; } }

    private static bool IsDefaultText(Color color) =>
        color == SystemColors.ControlText || color == SystemColors.WindowText || color == Color.Black;

    private static Color BackgroundFor(Color value, ThemePalette previous)
    {
        if (value.IsEmpty || value.A != 255) return value;
        if (value == SystemColors.Control || value == SystemColors.Window || value == SystemColors.ButtonFace)
            return Surface;
        return previous.TranslateBackground(value, CurrentPalette);
    }

    private static Color ForegroundFor(Color value, ThemePalette previous)
    {
        if (IsDefaultText(value)) return TextPrimary;
        if (value == SystemColors.GrayText || value == Color.DarkGray || value == Color.DimGray) return TextMuted;
        return previous.TranslateForeground(value, CurrentPalette);
    }

    private static void ApplyChildren(Control parent, ThemePalette previous, bool firstApply)
    {
        foreach (Control child in parent.Controls)
        {
            // Fonts and semantic colours explicitly assigned by a screen stay intact.
            if (firstApply && child.Font.SizeInPoints < 9 && child.Font.Style == FontStyle.Regular)
                child.Font = Typography.Body;
            child.ForeColor = ForegroundFor(child.ForeColor, previous);
            child.BackColor = BackgroundFor(child.BackColor, previous);
            if (child is DataGridView grid)
            {
                TranslateGridStyles(grid, previous);
                StyleGrid(grid);
            }
            else if (child is TextBoxBase text)
            {
                text.BackColor = InputSurface(text, text.ReadOnly);
                if (text is TextBox field && field.BorderStyle == BorderStyle.Fixed3D)
                    field.BorderStyle = BorderStyle.FixedSingle;
            }
            else if (child is ComboBox combo)
            {
                combo.BackColor = InputSurface(combo);
                combo.FlatStyle = FlatStyle.Flat;
            }
            else if (child is NumericUpDown number)
            {
                number.BackColor = InputSurface(number, number.ReadOnly);
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
                if (firstApply) button.Font = Typography.Button;
                button.UseVisualStyleBackColor = false;
            }
            else if (child is GroupBox)
            {
                if (firstApply) child.Font = Typography.CardTitle;
                child.BackColor = parent.BackColor;
            }
            else if (child is TabPage) child.BackColor = Background;
            else if (child is TreeView tree)
            {
                tree.BackColor = Surface;
                tree.LineColor = Border;
                TranslateNodes(tree.Nodes, previous);
            }
            else if (child is ListView list)
            {
                list.BackColor = Surface;
                if (!list.VirtualMode)
                    foreach (ListViewItem item in list.Items)
                    {
                        item.BackColor = BackgroundFor(item.BackColor, previous);
                        item.ForeColor = ForegroundFor(item.ForeColor, previous);
                        foreach (ListViewItem.ListViewSubItem subitem in item.SubItems)
                        {
                            subitem.BackColor = BackgroundFor(subitem.BackColor, previous);
                            subitem.ForeColor = ForegroundFor(subitem.ForeColor, previous);
                        }
                    }
            }
            else if (child is ListBox) child.BackColor = Surface;
            if (child is LinkLabel link)
            {
                link.LinkColor = Primary;
                link.ActiveLinkColor = PrimaryPressed;
                link.VisitedLinkColor = PrimaryHover;
                link.DisabledLinkColor = TextMuted;
            }
            ApplyChildren(child, previous, firstApply);
            if (child is InlineNotice notice) notice.RefreshTheme();
            ThemeNative.Apply(child);
            child.Invalidate();
        }
    }

    internal static Color InputSurface(Control input, bool readOnly = false)
    {
        if (!input.Enabled) return DisabledSurface;
        // NumericUpDown owns a native edit child; it shares the parent's field state.
        var numericOwner = input.Parent as NumericUpDown;
        if (input is IInputValidation { HasError: true } || numericOwner is IInputValidation { HasError: true }) return ErrorSoft;
        return readOnly || numericOwner?.ReadOnly == true ? ReadOnlySurface : Surface;
    }

    /// <summary>Legible text over a semantic signal fill, including blended canvas colours.</summary>
    public static Color ContrastText(Color background)
    {
        static double Channel(byte value)
        {
            double s = value / 255d;
            return s <= .04045 ? s / 12.92 : Math.Pow((s + .055) / 1.055, 2.4);
        }
        double luminance = .2126 * Channel(background.R) + .7152 * Channel(background.G) + .0722 * Channel(background.B);
        return luminance > .179 ? Color.Black : Color.White;
    }

    private static void TranslateNodes(TreeNodeCollection nodes, ThemePalette previous)
    {
        foreach (TreeNode node in nodes)
        {
            node.BackColor = BackgroundFor(node.BackColor, previous);
            node.ForeColor = ForegroundFor(node.ForeColor, previous);
            TranslateNodes(node.Nodes, previous);
        }
    }

    private static void TranslateGridStyles(DataGridView grid, ThemePalette previous)
    {
        void Translate(DataGridViewCellStyle style)
        {
            style.BackColor = BackgroundFor(style.BackColor, previous);
            style.ForeColor = ForegroundFor(style.ForeColor, previous);
            style.SelectionBackColor = BackgroundFor(style.SelectionBackColor, previous);
            style.SelectionForeColor = ForegroundFor(style.SelectionForeColor, previous);
        }
        Translate(grid.DefaultCellStyle);
        Translate(grid.AlternatingRowsDefaultCellStyle);
        Translate(grid.RowHeadersDefaultCellStyle);
        Translate(grid.ColumnHeadersDefaultCellStyle);
        foreach (DataGridViewColumn column in grid.Columns) Translate(column.DefaultCellStyle);
        if (!grid.VirtualMode)
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.HasDefaultCellStyle) Translate(row.DefaultCellStyle);
                foreach (DataGridViewCell cell in row.Cells)
                    if (cell.HasStyle) Translate(cell.Style);
            }
    }

    public static void StyleGrid(DataGridView grid, string emptyMessage = "Пока нет данных")
    {
        var appearance = GridStates.GetValue(grid, g => new GridAppearance(g));
        if (emptyMessage != "Пока нет данных" || appearance.EmptyMessage == "Пока нет данных")
            appearance.EmptyMessage = emptyMessage;
        bool firstStyle = appearance.Revision == 0;
        bool metricsChanged = appearance.Dpi != grid.DeviceDpi;
        if (!firstStyle && !metricsChanged && appearance.Revision == _revision)
        {
            grid.Invalidate();
            return;
        }
        grid.BackgroundColor = Surface;
        grid.BorderStyle = BorderStyle.None;
        grid.EnableHeadersVisualStyles = false;
        grid.GridColor = Border;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        if (metricsChanged) grid.ColumnHeadersHeight = UiScale.Px(grid, 40);
        grid.ColumnHeadersDefaultCellStyle.BackColor = SurfaceSecondary;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = TextSecondary;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = SurfaceSecondary;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = TextPrimary;
        if (firstStyle) grid.ColumnHeadersDefaultCellStyle.Font = Typography.Caption;
        if (metricsChanged) grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(UiScale.Px(grid, 10), 0, UiScale.Px(grid, 8), 0);
        grid.RowHeadersDefaultCellStyle.BackColor = SurfaceSecondary;
        grid.RowHeadersDefaultCellStyle.ForeColor = TextMuted;
        grid.DefaultCellStyle.BackColor = Surface;
        grid.DefaultCellStyle.ForeColor = TextPrimary;
        grid.DefaultCellStyle.SelectionBackColor = PrimarySoft;
        grid.DefaultCellStyle.SelectionForeColor = TextPrimary;
        if (firstStyle) grid.DefaultCellStyle.Font = Typography.Secondary;
        if (metricsChanged)
        {
            grid.DefaultCellStyle.Padding = new Padding(UiScale.Px(grid, 10), UiScale.Px(grid, 4), UiScale.Px(grid, 8), UiScale.Px(grid, 4));
            grid.RowTemplate.Height = UiScale.Px(grid, 36);
            if (grid.VirtualMode)
            {
                grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
                if (grid.RowCount > 0) grid.UpdateRowHeightInfo(0, true);
            }
            else
                foreach (DataGridViewRow row in grid.Rows)
                    if (!row.IsNewRow) row.Height = UiScale.Px(grid, 36);
        }
        appearance.Revision = _revision;
        appearance.Dpi = grid.DeviceDpi;
        grid.Invalidate();
    }

    private sealed class GridAppearance
    {
        private readonly DataGridView grid;
        private int hoverRow = -1;
        public int Revision { get; set; }
        public int Dpi { get; set; }
        public string EmptyMessage { get; set; } = "Пока нет данных";
        public GridAppearance(DataGridView owner)
        {
            grid = owner;
            grid.CellMouseEnter += (_, e) => SetHover(e.RowIndex);
            grid.MouseLeave += (_, _) => SetHover(-1);
            grid.CellPainting += PaintCell;
            grid.RowHeightInfoNeeded += (_, e) =>
            {
                if (grid.VirtualMode) e.Height = UiScale.Px(grid, 36);
            };
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
            if (grid.RowCount > (grid.NewRowIndex >= 0 ? 1 : 0)) return;
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
