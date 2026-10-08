using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace logReader.UI
{
    internal enum ThemeMode { Light, Dark }

    internal sealed record ThemePalette(
        Color Background, Color Surface, Color SurfaceAlt, Color Border,
        Color Text, Color Muted, Color Accent, Color AccentText, Color Danger, Color Success)
    {
        public Color AccentSoft => ThemeManager.Mode == ThemeMode.Dark
            ? Color.FromArgb(69, 60, 30) : Color.FromArgb(255, 245, 203);
    }

    // A semantic palette for every existing form. No timers, effects, external UI runtime,
    // or changes to processing models; event subscriptions are attached once per control.
    internal static class ThemeManager
    {
        private static readonly ThemePalette Light = new(
            Color.FromArgb(244, 245, 247), Color.White, Color.FromArgb(235, 238, 242),
            Color.FromArgb(221, 226, 232), Color.FromArgb(23, 32, 45), Color.FromArgb(99, 112, 131),
            Color.FromArgb(242, 201, 76), Color.FromArgb(33, 27, 9),
            Color.FromArgb(184, 58, 54), Color.FromArgb(36, 116, 81));
        private static readonly ThemePalette Dark = new(
            Color.FromArgb(17, 20, 25), Color.FromArgb(26, 30, 37), Color.FromArgb(36, 42, 51),
            Color.FromArgb(53, 61, 73), Color.FromArgb(239, 243, 248), Color.FromArgb(165, 175, 189),
            Color.FromArgb(242, 201, 76), Color.FromArgb(33, 27, 9),
            Color.FromArgb(255, 138, 132), Color.FromArgb(113, 205, 162));
        private static readonly ConditionalWeakTable<Control, ControlState> States = new();
        private static bool _initialized;
        private static string _preferencesPath = "";
        private sealed class ControlState { public bool Attached; public bool Hooked; public bool Captured; public bool Muted; }
        private sealed record Preferences(string Theme);

        public static ThemeMode Mode { get; private set; } = ThemeMode.Dark;
        public static ThemePalette Current => Mode == ThemeMode.Light ? Light : Dark;
        public static event EventHandler? ThemeChanged;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            _preferencesPath = Environment.GetEnvironmentVariable("LOGER_PREFERENCES_PATH")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LOGER", "ui-theme.json");
            try
            {
                if (File.Exists(_preferencesPath))
                {
                    var preference = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(_preferencesPath));
                    if (Enum.TryParse<ThemeMode>(preference?.Theme, true, out var mode) && Enum.IsDefined(mode)) Mode = mode;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            { System.Diagnostics.Debug.WriteLine("Theme preference could not be read: " + ex.Message); }
            if (Enum.TryParse<ThemeMode>(Environment.GetEnvironmentVariable("LOGER_THEME"), true, out var forced) && Enum.IsDefined(forced))
                Mode = forced;
        }

        public static void SetMode(ThemeMode mode, bool persist = true)
        {
            Initialize();
            if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            Mode = mode;
            if (persist)
            {
                try
                {
                    string? directory = Path.GetDirectoryName(_preferencesPath);
                    if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                    File.WriteAllText(_preferencesPath, JsonSerializer.Serialize(new Preferences(mode.ToString())));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                { System.Diagnostics.Debug.WriteLine("Theme preference could not be saved: " + ex.Message); }
            }
            foreach (Form form in Application.OpenForms.Cast<Form>().ToArray()) Apply(form);
            ThemeChanged?.Invoke(null, EventArgs.Empty);
        }

        public static void Attach(Form form)
        {
            Initialize();
            if (form.AcceptButton is Button accept && accept.Tag == null) accept.Tag = "primary";
            var state = States.GetOrCreateValue(form);
            if (state.Attached) return;
            state.Attached = true;
            form.HandleCreated += (_, _) => ApplyTitleBar(form);
            Apply(form);
        }

        public static void Apply(Control control)
        {
            Initialize();
            if (control.IsDisposed) return;
            control.SuspendLayout();
            try
            {
                ApplyControl(control);
                foreach (Control child in control.Controls) Apply(child);
            }
            finally { control.ResumeLayout(false); }
            if (control.IsHandleCreated && control.Visible) control.Invalidate();
        }

        private static void ApplyControl(Control c)
        {
            var p = Current;
            string tag = c.Tag as string ?? "";
            var state = States.GetOrCreateValue(c);
            if (!state.Captured)
            {
                state.Captured = true;
                state.Muted = c is Label && (c.ForeColor == Color.DimGray || c.ForeColor == Color.DarkGray);
            }
            c.ForeColor = tag == "brand" ? p.AccentText : tag == "muted" || state.Muted ? p.Muted : p.Text;
            c.BackColor = tag switch
            {
                "rail" or "background" => p.Background,
                "surface-alt" => p.SurfaceAlt,
                "brand" => p.Accent,
                "surface" => p.Surface,
                _ => c is TabPage ? p.Surface : c is Panel or Label or TabControl or GroupBox
                    ? c.Parent?.BackColor ?? p.Surface : p.Surface
            };
            switch (c)
            {
                case Form form:
                    form.BackColor = p.Background;
                    ApplyTitleBar(form);
                    break;
                case Button button: StyleButton(button, tag == "primary"); break;
                case TextBox text:
                    text.BackColor = text.ReadOnly ? p.SurfaceAlt : p.Surface;
                    text.BorderStyle = BorderStyle.FixedSingle;
                    break;
                case RichTextBox rich:
                    rich.BorderStyle = BorderStyle.None;
                    break;
                case ComboBox combo:
                    combo.FlatStyle = FlatStyle.Flat;
                    if (combo.DropDownStyle == ComboBoxStyle.DropDownList)
                    {
                        combo.DrawMode = DrawMode.OwnerDrawFixed;
                        HookCombo(combo);
                    }
                    break;
                case CheckBox check: check.FlatStyle = FlatStyle.Flat; break;
                case RadioButton radio: radio.FlatStyle = FlatStyle.Flat; break;
                case DataGridView grid: StyleGrid(grid); break;
                case ListView list:
                    list.BorderStyle = BorderStyle.None;
                    list.OwnerDraw = true;
                    HookListView(list);
                    break;
                case TreeView tree:
                    tree.BorderStyle = BorderStyle.None;
                    tree.LineColor = p.Border;
                    tree.DrawMode = TreeViewDrawMode.OwnerDrawText;
                    HookTree(tree);
                    break;
                case CheckedListBox checkedList:
                    checkedList.BorderStyle = BorderStyle.None;
                    break;
                case ListBox listBox:
                    listBox.BorderStyle = BorderStyle.None;
                    listBox.DrawMode = DrawMode.OwnerDrawFixed;
                    HookListBox(listBox);
                    break;
                case TabControl tabs:
                    tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
                    HookTabs(tabs);
                    break;
                case TabPage page:
                    page.UseVisualStyleBackColor = false;
                    page.BackColor = p.Surface;
                    break;
                case ProgressBar progress:
                    progress.ForeColor = p.Accent;
                    progress.BackColor = p.SurfaceAlt;
                    break;
            }
            if (c is not Button and not ListView and not TreeView and not ListBox and not TabControl && !state.Hooked)
            {
                state.Hooked = true;
                c.ControlAdded += (_, e) => { if (e.Control != null) Apply(e.Control); };
            }
        }

        public static void StyleButton(Button button, bool primary = false)
        {
            var p = Current;
            string tag = button.Tag as string ?? "";
            button.UseVisualStyleBackColor = false;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = tag.StartsWith("nav", StringComparison.Ordinal) ? 0 : 1;
            button.FlatAppearance.BorderColor = primary && button.Enabled ? p.Accent : p.Border;
            button.BackColor = !button.Enabled ? p.SurfaceAlt : primary ? p.Accent : tag == "nav-active" ? p.AccentSoft
                : tag == "nav" ? p.Background : p.SurfaceAlt;
            button.ForeColor = !button.Enabled ? p.Muted : primary ? p.AccentText : p.Text;
            button.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(255, 217, 98) : p.AccentSoft;
            button.FlatAppearance.MouseDownBackColor = primary ? Color.FromArgb(222, 177, 40) : p.SurfaceAlt;
            var state = States.GetOrCreateValue(button);
            if (!state.Hooked)
            {
                state.Hooked = true;
                button.EnabledChanged += (_, _) => StyleButton(button, button.Tag as string == "primary");
                button.Paint += (_, e) =>
                {
                    // Native ButtonRenderer uses a system disabled text color even when
                    // ForeColor is set. Keep the native button contract and paint this state.
                    if (!button.Enabled)
                    {
                        using var background = new SolidBrush(button.BackColor);
                        e.Graphics.FillRectangle(background, button.ClientRectangle);
                        if (button.FlatAppearance.BorderSize > 0)
                        {
                            using var border = new Pen(Current.Border);
                            e.Graphics.DrawRectangle(border, new Rectangle(0, 0, button.Width - 1, button.Height - 1));
                        }
                        var bounds = new Rectangle(button.Padding.Left + 3, button.Padding.Top + 2,
                            Math.Max(0, button.Width - button.Padding.Horizontal - 6),
                            Math.Max(0, button.Height - button.Padding.Vertical - 4));
                        var flags = TextFormatFlags.HidePrefix | TextFormatFlags.EndEllipsis;
                        flags |= button.TextAlign switch
                        {
                            ContentAlignment.TopLeft or ContentAlignment.MiddleLeft or ContentAlignment.BottomLeft => TextFormatFlags.Left,
                            ContentAlignment.TopRight or ContentAlignment.MiddleRight or ContentAlignment.BottomRight => TextFormatFlags.Right,
                            _ => TextFormatFlags.HorizontalCenter
                        };
                        flags |= button.TextAlign switch
                        {
                            ContentAlignment.TopLeft or ContentAlignment.TopCenter or ContentAlignment.TopRight => TextFormatFlags.Top,
                            ContentAlignment.BottomLeft or ContentAlignment.BottomCenter or ContentAlignment.BottomRight => TextFormatFlags.Bottom,
                            _ => TextFormatFlags.VerticalCenter
                        };
                        TextRenderer.DrawText(e.Graphics, button.Text, button.Font, bounds, Current.Muted, flags);
                        return;
                    }
                    if (!button.Focused) return;
                    using var pen = new Pen(Current.Accent, 2);
                    e.Graphics.DrawRectangle(pen, Rectangle.Inflate(button.ClientRectangle, -3, -3));
                };
            }
        }

        public static void StyleGrid(DataGridView grid)
        {
            var p = Current;
            grid.BackgroundColor = p.Surface;
            grid.BorderStyle = BorderStyle.None;
            grid.GridColor = p.Border;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = p.SurfaceAlt;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = p.Muted;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = p.SurfaceAlt;
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = p.Text;
            grid.DefaultCellStyle.BackColor = p.Surface;
            grid.DefaultCellStyle.ForeColor = p.Text;
            grid.DefaultCellStyle.SelectionBackColor = p.AccentSoft;
            grid.DefaultCellStyle.SelectionForeColor = p.Text;
            grid.AlternatingRowsDefaultCellStyle.BackColor = p.Background;
            grid.AlternatingRowsDefaultCellStyle.ForeColor = p.Text;
            grid.RowHeadersDefaultCellStyle.BackColor = p.SurfaceAlt;
            grid.RowHeadersDefaultCellStyle.ForeColor = p.Text;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        }

        private static void HookListView(ListView list)
        {
            var state = States.GetOrCreateValue(list);
            if (state.Hooked) return;
            state.Hooked = true;
            list.DrawColumnHeader += (_, e) =>
            {
                using var brush = new SolidBrush(Current.SurfaceAlt);
                e.Graphics.FillRectangle(brush, e.Bounds);
                TextRenderer.DrawText(e.Graphics, e.Header?.Text, list.Font, Rectangle.Inflate(e.Bounds, -8, 0),
                    Current.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            };
            list.DrawItem += (_, e) => { if (list.View != View.Details) e.DrawDefault = true; };
            list.DrawSubItem += (_, e) =>
            {
                bool selected = e.Item?.Selected == true;
                using var brush = new SolidBrush(selected ? Current.AccentSoft : e.ItemIndex % 2 == 0 ? Current.Surface : Current.Background);
                e.Graphics.FillRectangle(brush, e.Bounds);
                TextRenderer.DrawText(e.Graphics, e.SubItem?.Text, list.Font, Rectangle.Inflate(e.Bounds, -8, 0),
                    Current.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
                    | (e.Header?.TextAlign == HorizontalAlignment.Right ? TextFormatFlags.Right : TextFormatFlags.Left));
            };
        }

        private static void HookTree(TreeView tree)
        {
            var state = States.GetOrCreateValue(tree);
            if (state.Hooked) return;
            state.Hooked = true;
            tree.DrawNode += (_, e) =>
            {
                if (e.Node == null) return;
                bool selected = (e.State & TreeNodeStates.Selected) != 0;
                using var brush = new SolidBrush(selected ? Current.AccentSoft : tree.BackColor);
                e.Graphics.FillRectangle(brush, e.Bounds);
                TextRenderer.DrawText(e.Graphics, e.Node.Text, tree.Font, e.Bounds, Current.Text,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                if (selected && tree.Focused) ControlPaint.DrawFocusRectangle(e.Graphics, e.Bounds, Current.Accent, Current.AccentSoft);
            };
        }

        private static void HookListBox(ListBox list)
        {
            var state = States.GetOrCreateValue(list);
            if (state.Hooked) return;
            state.Hooked = true;
            list.DrawItem += (_, e) =>
            {
                if (e.Index < 0 || e.Index >= list.Items.Count) return;
                bool selected = (e.State & DrawItemState.Selected) != 0;
                using var brush = new SolidBrush(selected ? Current.AccentSoft : list.BackColor);
                e.Graphics.FillRectangle(brush, e.Bounds);
                TextRenderer.DrawText(e.Graphics, list.GetItemText(list.Items[e.Index]), list.Font, e.Bounds,
                    list.Enabled ? Current.Text : Current.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                e.DrawFocusRectangle();
            };
        }

        private static void HookCombo(ComboBox combo)
        {
            var state = States.GetOrCreateValue(combo);
            if (state.Hooked) return;
            state.Hooked = true;
            combo.DrawItem += (_, e) =>
            {
                bool selected = (e.State & DrawItemState.Selected) != 0;
                using var brush = new SolidBrush(selected ? Current.AccentSoft : Current.Surface);
                e.Graphics.FillRectangle(brush, e.Bounds);
                string text = e.Index < 0 ? combo.Text : combo.GetItemText(combo.Items[e.Index]) ?? "";
                TextRenderer.DrawText(e.Graphics, text, combo.Font, Rectangle.Inflate(e.Bounds, -4, 0),
                    combo.Enabled ? Current.Text : Current.Muted,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                if ((e.State & DrawItemState.Focus) != 0)
                    ControlPaint.DrawFocusRectangle(e.Graphics, e.Bounds, Current.Accent, Current.Surface);
            };
        }

        private static void HookTabs(TabControl tabs)
        {
            var state = States.GetOrCreateValue(tabs);
            if (state.Hooked) return;
            state.Hooked = true;
            tabs.DrawItem += (_, e) =>
            {
                bool selected = tabs.SelectedIndex == e.Index;
                using var brush = new SolidBrush(selected ? Current.AccentSoft : Current.SurfaceAlt);
                e.Graphics.FillRectangle(brush, e.Bounds);
                TextRenderer.DrawText(e.Graphics, tabs.TabPages[e.Index].Text, tabs.Font, e.Bounds,
                    Current.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                if (selected)
                {
                    using var pen = new Pen(Current.Accent, 3);
                    e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 2, e.Bounds.Right, e.Bounds.Bottom - 2);
                }
            };
        }

        private static void ApplyTitleBar(Form form)
        {
            if (!form.IsHandleCreated || !OperatingSystem.IsWindows()) return;
            int dark = Mode == ThemeMode.Dark ? 1 : 0;
            try { DwmSetWindowAttribute(form.Handle, 20, ref dark, sizeof(int)); }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
    }
}
