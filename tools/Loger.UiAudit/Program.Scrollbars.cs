using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

internal static partial class Program
{
    private static int RunScrollbarAudit(string assemblyPath)
    {
        using var probe = new Form
        {
            Text = "LOGER native scrollbar audit", Name = "scrollbarProbe",
            ClientSize = new Size(1120, 650), ShowInTaskbar = false
        };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2, Padding = new Padding(12) };
        for (int i = 0; i < 3; i++) layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
        for (int i = 0; i < 2; i++) layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        probe.Controls.Add(layout);
        var panel = new Panel { Name = "panel", Dock = DockStyle.Fill, AutoScroll = true };
        panel.Controls.Add(new Label { Text = Lines(), Width = 2200, Height = 2200, Location = new Point(4, 4) });
        var text = new RecreatingScrollbarTextBox
        {
            Name = "textBox", Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
            WordWrap = false, Dock = DockStyle.Fill, Text = Lines()
        };
        var grid = new DataGridView
        {
            Name = "grid", Dock = DockStyle.Fill, AllowUserToAddRows = false,
            RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            ScrollBars = ScrollBars.Vertical
        };
        grid.Columns.Add("number", "Строка"); grid.Columns.Add("value", "Значение");
        var list = new ListBox { Name = "listBox", Dock = DockStyle.Fill, IntegralHeight = false };
        var tree = new TreeView { Name = "treeView", Dock = DockStyle.Fill };
        var listView = new ListView { Name = "listView", Dock = DockStyle.Fill, View = View.Details };
        listView.Columns.Add("Строка", 250);
        for (int i = 0; i < 100; i++)
        {
            string item = $"Строка {i + 1}";
            grid.Rows.Add(i + 1, "DTC / CAN"); list.Items.Add(item);
            tree.Nodes.Add(item); listView.Items.Add(item);
        }
        Control[] controls = [panel, text, grid, list, tree, listView];
        for (int i = 0; i < controls.Length; i++)
        {
            var group = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4) };
            var heading = new Label { Dock = DockStyle.Top, Height = 24, Text = controls[i].Name };
            group.Controls.Add(controls[i]); group.Controls.Add(heading);
            layout.Controls.Add(group, i % 3, i / 3);
        }
        AttachAuditTheme(probe);
        Show(probe);
        var originalHandles = controls.Concat(Descendants(grid).OfType<ScrollBar>())
            .Where(control => control.IsHandleCreated).Distinct().ToDictionary(control => control, control => control.Handle);
        foreach (var stage in new[] { (Name: "dark-initial", Theme: "Dark"), (Name: "light-existing", Theme: "Light"), (Name: "dark-existing", Theme: "Dark") })
        {
            ApplyTheme(stage.Theme); Pump(); probe.Refresh(); Pump();
            Check(stage.Name + " preserves existing native handles", () =>
            {
                foreach (var pair in originalHandles)
                    Require(pair.Key.IsHandleCreated && pair.Key.Handle == pair.Value, pair.Key.Name + " handle was recreated during a theme switch.");
            });
            Check(stage.Name + " native window capture", () =>
            {
                using var bitmap = CaptureNativeScrollbarWindow(probe, stage.Name);
                foreach (Control control in controls)
                    Check(stage.Name + " " + control.Name + " scrollbar", () => MeasureScrollbar(probe, control, bitmap, stage.Name, stage.Theme == "Dark"));
                Check(stage.Name + " panel horizontal scrollbar", () => MeasureScrollbar(probe, panel, bitmap, stage.Name, stage.Theme == "Dark", vertical: false));
            });
        }
        Check("dark recreated text handle", () =>
        {
            int recreated = 0;
            text.HandleCreated += (_, _) => recreated++;
            text.RebuildNativeHandle(); Pump();
            Require(recreated == 1, "The existing text box did not recreate its native handle.");
            using var bitmap = CaptureNativeScrollbarWindow(probe, "dark-recreated");
            MeasureScrollbar(probe, text, bitmap, "dark-recreated", dark: true);
        });
        using (var late = new Form { Text = "Late-created scrollbar", Name = "lateScrollbar", ClientSize = new Size(400, 300) })
        {
            var lateText = new TextBox { Name = "lateTextBox", Multiline = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical, Text = Lines() };
            late.Controls.Add(lateText); AttachAuditTheme(late); Show(late);
            Check("dark late-created text handle", () =>
            {
                using var bitmap = CaptureNativeScrollbarWindow(late, "dark-late-created");
                MeasureScrollbar(late, lateText, bitmap, "dark-late-created", dark: true);
            });
            late.Close(); Pump();
        }
        probe.Close(); Pump();
        using (var main = Form("MainForm"))
        {
            Show(main);
            Check("main default native window capture", () => { using var capture = CaptureNativeScrollbarWindow(main, "main-default"); });
            Check("main default fits working area", () => Require(Screen.FromControl(main).WorkingArea.Contains(main.Bounds), "Default main window extends beyond the monitor work area."));
            Check("main full-HD initial session and export need no scrolling", () =>
            {
                var work = Screen.FromControl(main).WorkingArea;
                Require(work.Width >= 1280 && work.Height >= 1000, $"Physical work area {work} is too small to validate the full-HD default.");
                foreach (string name in new[] { "sessionPage", "exportSettings" })
                {
                    var scroll = (ScrollableControl)Descendants(main).Single(control => control.Name == name);
                    Require(!scroll.VerticalScroll.Visible && !scroll.HorizontalScroll.Visible, name + " has an initial scrollbar.");
                }
            });
            main.Size = main.MinimumSize; Pump();
            Check("main minimum native window capture", () => { using var capture = CaptureNativeScrollbarWindow(main, "main-minimum"); });
            main.Close(); Pump();
        }
        var report = new
        {
            assemblyPath, assemblySha256 = AssemblyHash(_ui), coreAssemblyPath = _core.Location,
            coreAssemblySha256 = AssemblyHash(_core), runtime = Environment.Version.ToString(), os = RuntimeInformation.OSDescription,
            colorMode = Application.ColorMode.ToString(), checks = Checks, measurements = Measurements,
            screenshots = Screens, failures = _failures,
            method = "Shown native HWND PrintWindow captures; GetScrollBarInfo identifies native scrollbar tracks; median pixel luminance validates Dark/Light/Dark on unchanged controls plus recreated and late-created handles."
        };
        File.WriteAllText(Path.Combine(_output, "scrollbar-report.json"), JsonSerializer.Serialize(report, Json));
        Console.WriteLine($"Native scrollbar audit: {_failures} failures, {Measurements.Count} measurements, {Screens.Count} captures.");
        return _failures == 0 ? 0 : 1;
    }

    private static string Lines() => string.Join(Environment.NewLine, Enumerable.Range(1, 100).Select(i => $"Строка {i} — CAN / DTC"));

    private static void AttachAuditTheme(Form form)
    {
        Type manager = _ui.GetType("logReader.UI.ThemeManager", throwOnError: true)!;
        manager.GetMethod("Attach", BindingFlags.Static | BindingFlags.Public)!.Invoke(null, [form]);
    }

    private static Bitmap CaptureNativeScrollbarWindow(Form form, string name)
    {
        Pump(); form.Update(); Pump();
        var bitmap = new Bitmap(form.Width, form.Height);
        try
        {
            bool captured;
            using (var graphics = Graphics.FromImage(bitmap))
            {
                IntPtr hdc = graphics.GetHdc();
                try { captured = Native.PrintWindow(form.Handle, hdc, 2); }
                finally { graphics.ReleaseHdc(hdc); }
            }
            Require(captured, "PrintWindow failed; no native pixel evidence.");
            var values = new List<double>();
            // Exclude the native caption/border: they alone must not make an empty client capture pass.
            for (int y = 60; y < bitmap.Height - 20; y += 17)
                for (int x = 20; x < bitmap.Width - 20; x += 17) values.Add(Luminance(bitmap.GetPixel(x, y)));
            Require(values.Count > 20 && values.Max() - values.Min() > 70, "Native capture is blank or lacks real rendered content.");
            string path = Path.Combine(_output, name + "-window.png");
            bitmap.Save(path, ImageFormat.Png);
            Screens.Add(new { name, path, bitmap.Width, bitmap.Height, dpi = form.DeviceDpi, method = "Native HWND PrintWindow" });
            return bitmap;
        }
        catch { bitmap.Dispose(); throw; }
    }

    private static void MeasureScrollbar(Form form, Control control, Bitmap bitmap, string stage, bool dark, bool vertical = true)
    {
        Control nativeControl = control;
        int objectId = vertical ? -5 : -6; // OBJID_VSCROLL / OBJID_HSCROLL: non-client scrollbar.
        if (control is DataGridView)
        {
            nativeControl = Descendants(control).OfType<ScrollBar>().SingleOrDefault(bar => bar.Visible && (vertical ? bar is VScrollBar : bar is HScrollBar))
                ?? throw new InvalidOperationException("Grid's native scrollbar is not visible.");
            objectId = -4; // OBJID_CLIENT: an independent SCROLLBAR child window.
        }
        Require(nativeControl.Visible && nativeControl.IsHandleCreated, control.Name + " has no visible native handle.");
        var info = new ScrollbarNative.ScrollbarInfo { Size = Marshal.SizeOf<ScrollbarNative.ScrollbarInfo>(), State = new uint[6] };
        Require(ScrollbarNative.GetScrollBarInfo(nativeControl.Handle, objectId, ref info), $"GetScrollBarInfo failed for {control.Name}: {Marshal.GetLastWin32Error()}.");
        Require((info.State[0] & 0x00018001) == 0, control.Name + " scrollbar is unavailable, invisible or offscreen.");
        int height = info.Bounds.Bottom - info.Bounds.Top;
        int width = info.Bounds.Right - info.Bounds.Left;
        int length = vertical ? height : width;
        int thickness = vertical ? width : height;
        Require(thickness >= 5 && length >= 50, control.Name + " scrollbar bounds are empty.");
        Require(info.ThumbTop >= 0 && info.ThumbBottom >= info.ThumbTop && info.ThumbBottom <= length, control.Name + " thumb coordinates are invalid.");
        int trackStart = info.LineButton + 2;
        int trackEnd = length - info.LineButton - 2;
        (int Start, int End) before = (trackStart, info.ThumbTop - 2);
        (int Start, int End) after = (info.ThumbBottom + 2, trackEnd);
        var segment = before.End - before.Start > after.End - after.Start ? before : after;
        Require(segment.End - segment.Start >= 12, control.Name + " has no measurable native track outside its thumb.");
        Require(ScrollbarNative.GetWindowRect(form.Handle, out var window), "Cannot locate capture origin.");
        int middle = (segment.Start + segment.End) / 2;
        int x = info.Bounds.Left - window.Left + (vertical ? width / 2 : middle);
        int y = info.Bounds.Top - window.Top + (vertical ? middle : height / 2);
        var sample = vertical ? new Rectangle(x - 1, y - 4, 3, 9) : new Rectangle(x - 4, y - 1, 9, 3);
        Require(new Rectangle(Point.Empty, bitmap.Size).Contains(sample), $"Scrollbar pixel sample outside native image: {control.Name} {sample}.");
        var colors = new List<Color>();
        for (int sy = sample.Top; sy < sample.Bottom; sy++)
            for (int sx = sample.Left; sx < sample.Right; sx++) colors.Add(bitmap.GetPixel(sx, sy));
        double median = colors.Select(Luminance).Order().ElementAt(colors.Count / 2);
        string rgb = $"#{colors.Select(c => (int)c.R).Order().ElementAt(colors.Count / 2):X2}{colors.Select(c => (int)c.G).Order().ElementAt(colors.Count / 2):X2}{colors.Select(c => (int)c.B).Order().ElementAt(colors.Count / 2):X2}";
        Measurements.Add(new { stage, control = control.Name, axis = vertical ? "vertical" : "horizontal", dark, rgb, medianLuminance = median,
            sample = new { sample.X, sample.Y, sample.Width, sample.Height },
            scrollbar = new { width, height, info.LineButton, info.ThumbTop, info.ThumbBottom } });
        Require(dark ? median < 150 : median > 170, $"{stage} {control.Name}: native track {rgb}, luminance {median:F1}; expected {(dark ? "dark <150" : "light >170")}.");
    }

    private static double Luminance(Color color) => .2126 * color.R + .7152 * color.G + .0722 * color.B;

    private sealed class RecreatingScrollbarTextBox : TextBox
    {
        internal void RebuildNativeHandle() => RecreateHandle();
    }

    private static class ScrollbarNative
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct Rect { internal int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        internal struct ScrollbarInfo
        {
            internal int Size;
            internal Rect Bounds;
            internal int LineButton, ThumbTop, ThumbBottom, Reserved;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)] internal uint[] State;
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetScrollBarInfo(IntPtr window, int objectId, ref ScrollbarInfo info);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetWindowRect(IntPtr window, out Rect rect);
    }
}
