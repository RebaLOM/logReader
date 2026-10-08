using logReader.UI.Controls;

namespace logReader.UI.Theme;

public static class AppTheme
{
    private static ThemeMode _current = ThemeMode.Light;

    public static ThemeMode Current
    {
        get => _current;
        set
        {
            if (_current == value)
                return;
            _current = value;
            AntdThemeBridge.Apply(_current);
            Changed?.Invoke(null, EventArgs.Empty);
        }
    }

    public static ThemePalette Palette => ThemePalette.For(_current);

    public static event EventHandler? Changed;

    public static void InitializeFromPreferences()
    {
        _current = ThemePreferences.Load();
        AntdThemeBridge.Apply(_current);
    }

    public static void SetAndPersist(ThemeMode mode)
    {
        Current = mode;
        ThemePreferences.Save(mode);
    }

    public static void Apply(Control root)
    {
        ArgumentNullException.ThrowIfNull(root);
        AntdThemeBridge.Apply(Current);
        ThemePalette p = Palette;
        ApplyRecursive(root, p);

        if (root is Form form)
            ThemeNative.ApplyTitleBar(form, Current);
    }

    private static void ApplyRecursive(Control control, ThemePalette p)
    {
        switch (control)
        {
            case ModernButton mb:
                mb.ApplyTheme(p);
                break;
            case ModernCard card:
                card.ApplyTheme(p);
                break;
            case NavigationItem:
                control.Invalidate();
                break;
            case StatusBadge badge:
                badge.ApplyTheme(p);
                break;
            case EmptyState empty:
                empty.ApplyTheme(p);
                break;
            case InlineNotice notice:
                notice.ApplyTheme(p);
                break;
            case Button btn:
                ApplyStockButton(btn, p);
                break;
            case TextBox tb when IsConsole(tb):
                tb.BackColor = p.ConsoleBg;
                tb.ForeColor = p.ConsoleFg;
                tb.BorderStyle = BorderStyle.FixedSingle;
                EnsureFont(tb, Typography.MonoFamily, 9f, FontStyle.Regular);
                break;
            case TextBox tb:
                tb.BackColor = p.Surface;
                tb.ForeColor = p.Text;
                tb.BorderStyle = BorderStyle.FixedSingle;
                break;
            case RichTextBox rtb:
                rtb.BackColor = p.Surface;
                rtb.ForeColor = p.Text;
                break;
            case Label lbl:
                if (ReferenceEquals(lbl.Tag, ThemeTags.Muted) || LooksMuted(lbl))
                    lbl.ForeColor = p.Muted;
                else if (ReferenceEquals(lbl.Tag, ThemeTags.Brand))
                    lbl.ForeColor = p.Text;
                else
                    lbl.ForeColor = p.Text;
                lbl.BackColor = Color.Transparent;
                break;
            case CheckBox cb:
                cb.ForeColor = p.Text;
                cb.BackColor = Color.Transparent;
                break;
            case RadioButton rb:
                rb.ForeColor = p.Text;
                rb.BackColor = Color.Transparent;
                break;
            case ComboBox combo:
            {
                // FlatStyle на ComboBox сбрасывает SelectedIndex — из‑за этого «CSV» мог
                // превратиться в «CSV ДСТ Коннект» после Apply темы.
                int selected = combo.SelectedIndex;
                combo.BackColor = p.Surface;
                combo.ForeColor = p.Text;
                if (combo.FlatStyle != FlatStyle.Flat)
                    combo.FlatStyle = FlatStyle.Flat;
                if (selected >= 0 && selected < combo.Items.Count && combo.SelectedIndex != selected)
                    combo.SelectedIndex = selected;
                break;
            }
            case NumericUpDown nud:
                nud.BackColor = p.Surface;
                nud.ForeColor = p.Text;
                break;
            case ListBox list:
                list.BackColor = p.Surface;
                list.ForeColor = p.Text;
                break;
            case TreeView tree:
                tree.BackColor = p.Surface;
                tree.ForeColor = p.Text;
                tree.LineColor = p.Border;
                break;
            case ListView lv:
                lv.BackColor = p.Surface;
                lv.ForeColor = p.Text;
                break;
            case DataGridView grid:
                ApplyDataGrid(grid, p);
                break;
            case TabControl tabs:
                tabs.BackColor = p.Canvas;
                break;
            case TabPage page:
                page.BackColor = p.Canvas;
                page.ForeColor = p.Text;
                break;
            case ProgressBar:
                // Системный ProgressBar; фон родителя задаёт контекст.
                break;
            case SplitContainer split:
                split.BackColor = p.Border;
                split.Panel1.BackColor = p.Canvas;
                split.Panel2.BackColor = p.Canvas;
                break;
            case GroupBox gb:
                gb.ForeColor = p.Text;
                gb.BackColor = p.Canvas;
                break;
            case Panel panel:
                if (ReferenceEquals(panel.Tag, ThemeTags.Surface))
                    panel.BackColor = p.Surface;
                else if (ReferenceEquals(panel.Tag, ThemeTags.Elevated))
                    panel.BackColor = p.Elevated;
                else if (ReferenceEquals(panel.Tag, ThemeTags.Header))
                    panel.BackColor = p.Surface;
                else
                    panel.BackColor = p.Canvas;
                break;
            case Form form:
                form.BackColor = p.Canvas;
                form.ForeColor = p.Text;
                break;
            default:
                if (control is not UserControl)
                {
                    try
                    {
                        control.BackColor = p.Canvas;
                        control.ForeColor = p.Text;
                    }
                    catch
                    {
                        // Некоторые контролы не поддерживают BackColor.
                    }
                }
                break;
        }

        foreach (Control child in control.Controls)
            ApplyRecursive(child, p);
    }

    private static void ApplyStockButton(Button btn, ThemePalette p)
    {
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderSize = 1;
        btn.UseVisualStyleBackColor = false;
        EnsureFont(btn, Typography.UiFamily, 9f, FontStyle.Regular);

        if (ReferenceEquals(btn.Tag, ThemeTags.Primary) || IsPrimaryNamed(btn))
        {
            btn.BackColor = p.Primary;
            btn.ForeColor = p.OnPrimary;
            btn.FlatAppearance.BorderColor = p.Primary;
            btn.FlatAppearance.MouseOverBackColor = p.PrimaryHover;
            btn.FlatAppearance.MouseDownBackColor = p.PrimaryHover;
        }
        else
        {
            btn.BackColor = p.Elevated;
            btn.ForeColor = p.Text;
            btn.FlatAppearance.BorderColor = p.Border;
            btn.FlatAppearance.MouseOverBackColor = p.Surface;
            btn.FlatAppearance.MouseDownBackColor = p.Border;
        }
    }

    private static void ApplyDataGrid(DataGridView grid, ThemePalette p)
    {
        grid.BackgroundColor = p.Surface;
        grid.GridColor = p.GridLine;
        grid.BorderStyle = BorderStyle.FixedSingle;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersDefaultCellStyle.BackColor = p.Elevated;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = p.Text;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = p.Elevated;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = p.Text;
        grid.DefaultCellStyle.BackColor = p.Surface;
        grid.DefaultCellStyle.ForeColor = p.Text;
        grid.DefaultCellStyle.SelectionBackColor = p.Selection;
        grid.DefaultCellStyle.SelectionForeColor = p.Text;
        grid.RowHeadersDefaultCellStyle.BackColor = p.Elevated;
        grid.RowHeadersDefaultCellStyle.ForeColor = p.Muted;
        grid.RowHeadersDefaultCellStyle.SelectionBackColor = p.Selection;
    }

    private static bool IsConsole(TextBox tb) =>
        ReferenceEquals(tb.Tag, ThemeTags.Console)
        || string.Equals(tb.Name, "textBoxLog", StringComparison.Ordinal);

    private static bool LooksMuted(Label lbl) =>
        string.Equals(lbl.Name, "labelFilterStatus", StringComparison.Ordinal)
        || string.Equals(lbl.Name, "labelProgress", StringComparison.Ordinal)
        || lbl.ForeColor == Color.DimGray
        || lbl.ForeColor == Color.DarkGray;

    private static bool IsPrimaryNamed(Button btn) =>
        string.Equals(btn.Name, "buttonProcess", StringComparison.Ordinal);

    // Меняем Font только при расхождении — иначе каждый Apply создаёт и Dispose'ит Font.
    private static void EnsureFont(Control control, string family, float size, FontStyle style)
    {
        Font current = control.Font;
        if (current != null
            && string.Equals(current.FontFamily.Name, family, StringComparison.OrdinalIgnoreCase)
            && Math.Abs(current.SizeInPoints - size) < 0.01f
            && current.Style == style)
            return;

        control.Font = new Font(family, size, style);
    }
}

public static class ThemeTags
{
    public static readonly object Primary = new();
    public static readonly object Muted = new();
    public static readonly object Brand = new();
    public static readonly object Surface = new();
    public static readonly object Elevated = new();
    public static readonly object Header = new();
    public static readonly object Console = new();
}
