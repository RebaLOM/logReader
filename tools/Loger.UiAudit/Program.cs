using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;

// Loads either the preserved baseline or the current application. There is deliberately no
// project reference: captures and checks cannot accidentally rebuild or change the target.
internal static class Program
{
    private static Assembly _ui = null!;
    private static Assembly _core = null!;
    private static string _output = "";
    private static readonly List<object> Checks = new();
    private static readonly List<object> Screens = new();
    private static readonly List<object> Measurements = new();
    private static int _failures;
    private static WindowsFormsSynchronizationContext? _context;
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    [STAThread]
    private static int Main(string[] args)
    {
        var startup = Stopwatch.StartNew();
        string assemblyPath = Path.GetFullPath(Option(args, "--assembly", "artifacts/baseline/app/LOGER.dll"));
        _output = Path.GetFullPath(Option(args, "--output", "artifacts/baseline/ui"));
        string theme = Option(args, "--theme", "existing");
        int iterations = int.Parse(Option(args, "--iterations", "7"));
        Directory.CreateDirectory(_output);
        // Theme preference IO is confined to this run, even when the target has persisted themes.
        Environment.SetEnvironmentVariable("LOGER_SETTINGS_PATH", Path.Combine(_output, "preferences.json"));
        Environment.SetEnvironmentVariable("LOGER_PREFERENCES_PATH", Path.Combine(_output, "preferences.json"));
        Environment.SetEnvironmentVariable("LOGER_THEME", theme);
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        _context = new WindowsFormsSynchronizationContext();
        SynchronizationContext.SetSynchronizationContext(_context);
        string bin = Path.GetDirectoryName(assemblyPath)!;
        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            string path = Path.Combine(bin, name.Name + ".dll");
            return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
        };
        try
        {
            _core = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(bin, "logReader.dll"));
            _ui = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
            ApplyTheme(theme);
            if (args.Contains("--profile-only"))
            {
                var watch = Stopwatch.StartNew(); using var profile = Form("MainForm");
                double construction = watch.Elapsed.TotalMilliseconds; Show(profile);
                double shown = watch.Elapsed.TotalMilliseconds;
                object? trace = profile.GetType().GetField("StartupTrace", Members)?.GetValue(profile)
                    ?? profile.GetType().GetProperty("StartupTrace", Members)?.GetValue(profile);
                var report = new { assemblyPath, theme, constructionMs = construction, shownMs = shown, trace };
                File.WriteAllText(Path.Combine(_output, "profile-report.json"), JsonSerializer.Serialize(report, Json));
                Console.WriteLine(JsonSerializer.Serialize(report, Json)); profile.Close(); return 0;
            }
            if (args.Contains("--extras-only"))
            {
                PrepareFixtures(out string extraTrc, out string extraDbc, out _, out _);
                using var owner = Form("MainForm"); Show(owner);
                CaptureExtras(owner, extraTrc, extraDbc); owner.Close(); Pump();
                File.WriteAllText(Path.Combine(_output, "extra-report.json"), JsonSerializer.Serialize(new
                { assemblyPath, theme, checks = Checks, screenshots = Screens, captureMethod = "Shown native WinForms HWND PrintWindow plus DrawToBitmap" }, Json));
                Console.WriteLine($"Extra UI audit complete: {_failures} failures, {Screens.Count} captures.");
                return _failures == 0 ? 0 : 1;
            }
            var timings = new List<double>();
            var shownTimings = new List<double>();
            for (int i = 0; i < iterations; i++)
            {
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                var watch = Stopwatch.StartNew();
                using var measured = Form("MainForm");
                double created = watch.Elapsed.TotalMilliseconds;
                Show(measured);
                double shown = watch.Elapsed.TotalMilliseconds;
                if (i == 0)
                    Measurements.Add(new { name = "cold_harness_entry_to_main_shown", milliseconds = startup.Elapsed.TotalMilliseconds,
                        note = "From harness Main entry, assembly loading, constructor, Show, layout and message pump. Excludes OS process launch and runtime bootstrap." });
                timings.Add(created); shownTimings.Add(shown);
                Measurements.Add(new { name = "main_form", sample = i + 1, constructionMs = created, shownMs = shown,
                    managedBytes = GC.GetTotalMemory(false), workingSetBytes = Process.GetCurrentProcess().WorkingSet64,
                    gdiHandles = Native.GetGuiResources(Process.GetCurrentProcess().Handle, 0),
                    userHandles = Native.GetGuiResources(Process.GetCurrentProcess().Handle, 1) });
                measured.Close(); Pump();
            }
            Measurements.Add(new { name = "main_form_medians", iterations, constructionMs = Median(timings), shownMs = Median(shownTimings),
                note = "Warm in-process median; first sample included. Same harness/runtime/DPI on baseline and redesigned assembly." });
            PrepareFixtures(out string trc, out string dbc, out string xlsx, out string composites);
            using var main = Form("MainForm");
            Show(main);
            Capture(main, "main-default");
            Check("default processing and source actions fit viewport", () =>
            {
                Require(InViewport(Field<Button>(main, "buttonProcess")), "Process is clipped in default viewport.");
                Require(InViewport(Field<Button>(main, "buttonCANlog")), "Source picker is clipped in default viewport.");
                Require(InViewport(Field<Button>(main, "buttonViewLog")), "Packet viewer is clipped in default viewport.");
            });
            main.ClientSize = new Size(1024, 720); Pump(); Capture(main, "main-1024x720");
            Check("minimum processing action is reachable", () =>
            {
                var process = Field<Button>(main, "buttonProcess");
                ScrollIntoView(process); Require(InViewport(process), "Process cannot be reached at minimum size.");
            });
            main.ClientSize = new Size(1440, 900); Pump(); Capture(main, "main-1440x900");
            AuditMainRoutes(main);
            Check("empty input error reaches journal", () =>
            {
                Field<TextBox>(main, "textBoxCanLog").Text = "";
                Field<Button>(main, "buttonProcess").PerformClick(); Pump();
                Require(Field<TextBox>(main, "textBoxLog").Text.Contains("не указан"), "Missing-input journal error was not shown.");
            });
            Check("UI processes fixture and restores idle controls", () =>
            {
                SetInput(main, trc, dbc, Path.Combine(_output, "fixture-result.csv"));
                Field<Button>(main, "buttonProcess").PerformClick();
                Until(() => Get(main, "_operation") == null && Field<TextBox>(main, "textBoxLog").Text.Contains("успешно"), "processing", 15000);
                Pump();
                Require(File.Exists(Path.Combine(_output, "fixture-result.csv")), "Output was not produced.");
                Require(Field<TextBox>(main, "textBoxLog").Text.Contains("успешно"), "Success missing from journal.");
                Require(Field<Button>(main, "buttonProcess").Enabled, "Process did not return to enabled.");
                Require(!Field<Button>(main, "buttonCancel").Visible, "Cancel stayed visible after completion.");
            });
            Check("UI repeated processing preserves output bytes", () =>
            {
                string path = Path.Combine(_output, "fixture-result.csv");
                byte[] first = File.ReadAllBytes(path);
                Field<Button>(main, "buttonProcess").PerformClick();
                Until(() => Get(main, "_operation") == null && Field<TextBox>(main, "textBoxLog").Text.Contains("успешно"), "repeated processing", 15000); Pump();
                Require(first.SequenceEqual(File.ReadAllBytes(path)), "Repeated output differed.");
            });
            Capture(main, "main-complete");
            Check("UI invalid save path reports actionable error", () =>
            {
                Field<TextBox>(main, "textBoxOutput").Text = Path.Combine(_output, "absent-parent", "result.csv");
                Field<Button>(main, "buttonProcess").PerformClick(); Pump();
                Require(Field<TextBox>(main, "textBoxLog").Text.Contains("не существует"), "Save-directory error missing.");
            });
            Check("UI cancellation removes partial output and restores idle", () =>
            {
                string large = Path.Combine(_output, "fixtures", "large.trc");
                using (var writer = new StreamWriter(large, false, new UTF8Encoding(false)))
                    for (int i = 1; i <= 2000000; i++) writer.WriteLine(FormattableString.Invariant($"{i,7}) {i * 10.0,12:F1}  Rx     0CFF0008  8  11 00 00 00 00 00 00 00"));
                string path = Path.Combine(_output, "cancelled.csv");
                // Only this generated audit output is reset; failed earlier audit runs may
                // have legitimately completed it before cancellation was exercised.
                if (File.Exists(path)) File.Delete(path);
                SetInput(main, large, dbc, path);
                main.Controls.Find("navDecoder", true).OfType<Button>().FirstOrDefault()?.PerformClick(); Pump();
                Field<Button>(main, "buttonProcess").PerformClick();
                Require(Get(main, "_operation") != null, "No cancellable operation was created.");
                Require(Field<Button>(main, "buttonCancel").Visible, "Cancel inaccessible while busy.");
                ScrollIntoView(Field<Button>(main, "buttonCancel"));
                Capture(main, "main-busy-decoder");
                bool cancelInViewport = InViewport(Field<Button>(main, "buttonCancel"));
                Field<Button>(main, "buttonCancel").PerformClick();
                Until(() => Get(main, "_operation") == null && Field<Button>(main, "buttonProcess").Enabled, "cancellation and idle restoration", 15000); Pump();
                Require(Field<TextBox>(main, "textBoxLog").Text.Contains("отменена"), "Cancelled status absent.");
                Require(!File.Exists(path), "Cancelled processing left an output file.");
                Require(!Directory.EnumerateFiles(_output, "*cancelled*.tmp*").Any(), "Cancelled processing left temporary files.");
                Require(Field<Button>(main, "buttonProcess").Enabled, "UI not restored after cancellation.");
                Require(cancelInViewport, "Cancel cannot be reached while decoder page is active.");
            });
            SetInput(main, trc, dbc, Path.Combine(_output, "fixture-result.csv"));
            CheckBatchRoutes(main, dbc);
            SetInput(main, trc, dbc, Path.Combine(_output, "fixture-result.csv"));
            CaptureOtherForms(trc, dbc, xlsx, composites);
            CheckFilters(dbc);
            CheckSaveOptions(Path.GetDirectoryName(trc)!);
            Check("main navigation and theme actions", () =>
            {
                foreach (var button in Descendants(main).OfType<Button>().Where(b => b.Name.StartsWith("nav", StringComparison.OrdinalIgnoreCase)).ToList())
                { button.PerformClick(); Pump(); Capture(main, "main-" + button.Name); }
            });
            CheckThemeAndKeyboard(main, theme);
            CheckMessages(main);
            CheckEditorConfirmations(dbc);
            main.Close(); Pump();
            File.WriteAllText(Path.Combine(_output, "report.json"), JsonSerializer.Serialize(new
            {
                assemblyPath, assemblySha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(assemblyPath))),
                theme, captureMethod = "Actual WinForms instantiated and shown on STA thread; Control.DrawToBitmap plus native HWND capture using PrintWindow. Neither is a visual mockup; no unrelated desktop content captured.",
                runtime = Environment.Version.ToString(), dpi = 96, checks = Checks, screenshots = Screens, measurements = Measurements,
                limitations = new[] { "No physical keyboard/mouse or Windows UIA interaction.", "Native file/folder dialogs and baseline system MessageBox interaction NOT TESTED. Redesigned custom message dialogs use actual modal button interactions.", "Process startup/long-session leaks/UI scroll FPS/CPU load NOT TESTED.", "DrawToBitmap can omit native visual states such as expanded combobox popups." }
            }, Json));
            Console.WriteLine($"UI audit complete: {_failures} failures, {Screens.Count} captures. {_output}");
            return _failures == 0 ? 0 : 1;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 2; }
    }

    private static void PrepareFixtures(out string trc, out string dbc, out string xlsx, out string composites)
    {
        string dir = Path.Combine(_output, "fixtures"); Directory.CreateDirectory(dir);
        trc = Path.Combine(dir, "small.trc"); dbc = Path.Combine(dir, "devices.dbc");
        xlsx = Path.Combine(dir, "devices.xlsx"); composites = Path.Combine(dir, "composites.xlsx");
        File.WriteAllText(trc, "     1)        10.0  Rx     0CFF0008  8  11 00 00 00 00 00 00 00\n     2)        20.0  Rx     0CFF0009  8  22 00 00 00 00 00 00 00\n");
        File.WriteAllText(dbc, "BO_ 2365521928 A: 8 X\n SG_ A1 : 0|8@1+ (1,0) [0|255] \"\" X\nBO_ 2365521929 B: 8 X\n SG_ B1 : 0|8@1+ (1,0) [0|255] \"\" X\n");
        Static("DeviceExcelFile", "CreateDevicesExcelTemplate", xlsx);
        Static("CompositeExcelFile", "CreateTemplate", composites);
    }

    private static void CaptureExtras(Form main, string trc, string description)
    {
        Check("source chooser modal can be cancelled without input changes", () =>
        {
            string before = Field<TextBox>(main, "textBoxCanLog").Text;
            WithDialogChoice(main, DialogResult.Cancel, () => { Field<Button>(main, "buttonCANlog").PerformClick(); return 0; }, "source-chooser");
            Require(Field<TextBox>(main, "textBoxCanLog").Text == before, "Cancelled picker changed input.");
        });
        Check("DBF editor loads and renders both message rows", () =>
        {
            string path = Path.Combine(_output, "fixtures", "devices.dbf");
            Static("DbfFile", "Write", path, Static("DbcFile", "Read", description)!);
            using var editor = Form("DevicesEditorForm", path); Show(editor);
            Require(Field<DataGridView>(editor, "_grid").Rows.Count == 2, "DBF editor message count differs.");
            Capture(editor, "devices-editor-dbf"); editor.Close();
        });
        Check("all five folder formats plus All fit DST export selector", () =>
        {
            string folder = Path.Combine(_output, "fixtures", "formats"); Directory.CreateDirectory(folder);
            File.Copy(trc, Path.Combine(folder, "sample.trc"), true);
            File.WriteAllText(Path.Combine(folder, "sample.asc"), "date Wed Apr 26 10:00:00.000 2023\nbase hex timestamps absolute\n   1.234600 1  0CFF0008x       Rx   d 8 10 00 00 00 00 00 00 00\n");
            File.WriteAllText(Path.Combine(folder, "matrix.csv"), ";0CFF0008;0CFF0009\n00:00:00.020;1100000000000000;2200000000000000\n");
            File.WriteAllText(Path.Combine(folder, "legacy.csv"), "1;10:15:30;CFF0008;1;17;0;0;0;0;0;0;0\n");
            File.WriteAllText(Path.Combine(folder, "canfox.txt"), "2023.04.26 10:15:30.010 - 0CFF0008 8 11 00 00 00 00 00 00 00\n");
            using var options = SaveForm(folder); Show(options);
            var list = Field<CheckedListBox>(options, "_formatsList");
            Require(list.Items.Count == 6, "Expected All + all five supported input format options.");
            Require(list.CheckedItems.Count == 6, "Initial format-selection semantics changed.");
            Capture(options, "save-options-all-formats");
            Field<ComboBox>(options, "_comboOutputFormat").SelectedIndex = 2; Pump();
            Capture(options, "save-options-dst-all-formats");
            // In a redesigned dialog every existing format fits without scrolling at 96DPI.
            if (_ui.GetType("logReader.UI.ThemeManager") != null)
                Require(list.ClientSize.Height >= list.ItemHeight * list.Items.Count, "DST format list does not display all six choices.");
            list.SetItemChecked(0, false); Pump();
            Require(list.CheckedItems.Count == 0 && !((Button)options.AcceptButton!).Enabled, "Unchecked All did not disable invalid folder selection.");
            list.SetItemChecked(0, true); Pump();
            Require(list.CheckedItems.Count == 6 && ((Button)options.AcceptButton!).Enabled, "Checked All did not restore all folder formats.");
            options.Close();
        });
    }

    private static void CaptureOtherForms(string trc, string dbc, string xlsx, string composites)
    {
        object devices = Static("DeviceFiles", "LoadDevices", dbc)!;
        var deviceEnabled = new Dictionary<string, bool>(); var paramEnabled = new Dictionary<string, bool[]>();
        var pairs = GetStatic(_ui.GetType("logReader.UI.MainForm")!, "_conversionPairs")!;
        var cases = new (string Name, Func<Form> Create)[]
        {
            ("save-options", () => SaveForm(Path.GetDirectoryName(trc)!)),
            ("log-inspector", () => Form("CanLogViewForm", trc)),
            ("device-filter", () => Form("Devices_ParametrsForm", devices, deviceEnabled, paramEnabled, new List<string>{"00000123"}, new List<string>{"0CFF0008", "0CFF0009"})),
            ("devices-editor-dbc", () => Form("DevicesEditorForm", dbc)),
            ("devices-editor-xlsx", () => Form("DevicesEditorForm", xlsx)),
            ("composites-editor", () => Form("CompositeEditorForm", composites)),
            ("composite-signal", () => Form("CompositeParamEditForm", (object?)null)),
            ("dbc-message", () => Form("DbcMessageEditForm", (object?)null)),
            ("xlsx-message", () => Form("XlsxMessageEditForm", null, false)),
            ("dbc-signal", () => Form("DbcSignalEditForm", null, 8, null, null, null)),
            ("xlsx-field", () => Form("DeviceFieldRowEditForm", null, 8, 0, null, null)),
            ("file-kind", () => Form("FileKindPromptForm")),
            ("conversion", () => Form("FormatConversionDialog", pairs, trc, new Action<string>(_ => { }))),
            ("help", () => Form("HelpForm")),
        };
        foreach (var scenario in cases)
            Check("construct/render " + scenario.Name, () =>
            {
                var watch = Stopwatch.StartNew(); using var form = scenario.Create(); Show(form);
                if (scenario.Name == "log-inspector") Until(() => Field<TextBox>(form, "textBoxSearch").Enabled, "log-inspector load", 15000);
                Measurements.Add(new { name = scenario.Name, shownMs = watch.Elapsed.TotalMilliseconds });
                Capture(form, scenario.Name);
                if (scenario.Name == "log-inspector")
                {
                    Field<TextBox>(form, "textBoxSearch").Text = "0008"; Pump();
                    Require(Field<ListView>(form, "_list").VirtualListSize == 1, "Log ID filter failed.");
                }
                if (scenario.Name == "help")
                {
                    Field<TextBox>(form, "textBoxSearch").Text = "DBC"; Pump();
                    Require(Field<TreeView>(form, "treeViewTopics").Nodes.Count > 0, "Help search failed.");
                    Capture(form, "help-search");
                }
                if (scenario.Name == "save-options")
                { Field<ComboBox>(form, "_comboOutputFormat").SelectedIndex = 2; Pump(); Capture(form, "save-options-dst"); }
                form.Close(); Pump();
            });
    }

    private static void CheckFilters(string dbc)
    {
        Check("parameter filter cancel keeps original maps and OK commits", () =>
        {
            object devices = Static("DeviceFiles", "LoadDevices", dbc)!;
            var enabled = new Dictionary<string, bool>(); var parameters = new Dictionary<string, bool[]>();
            using (var form = Form("Devices_ParametrsForm", devices, enabled, parameters, null, null))
            { Show(form); Field<Button>(form, "buttonDisableAll").PerformClick(); Pump(); form.Close(); }
            Require(enabled.Count == 0 && parameters.Count == 0, "Cancel altered target maps.");
            using var commit = Form("Devices_ParametrsForm", devices, enabled, parameters, null, null); Show(commit);
            Field<Button>(commit, "buttonDisableAll").PerformClick(); Pump();
            ((Button)commit.AcceptButton!).PerformClick(); Pump();
            Require(enabled.Count == 2 && enabled.Values.All(v => !v), "Disable-all commit failed.");
            Require(parameters.Count == 2 && parameters.Values.All(a => a.All(v => !v)), "Parameter commit failed.");
            using var all = Form("Devices_ParametrsForm", devices, enabled, parameters, null, null); Show(all);
            Field<TextBox>(all, "textBoxSearch").Text = "A1"; Pump();
            Field<Button>(all, "buttonEnableAll").PerformClick(); ((Button)all.AcceptButton!).PerformClick(); Pump();
            Require(enabled.Values.All(v => v) && parameters.Values.All(a => a.All(v => v)), "Enable-all applied only visible filtered rows.");
        });
    }

    private static void CheckBatchRoutes(Form main, string description)
    {
        string folder = Path.Combine(_output, "fixtures", "batch"); Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "a.trc"), ";   Start time: 26.04.2023 23:59:59.990.0\n     1)         5.0  Rx     0CFF0008  8  01 00 00 00 00 00 00 00\n     2)        20.0  Rx     0CFF0008  8  02 00 00 00 00 00 00 00\n");
        File.WriteAllText(Path.Combine(folder, "b.trc"), ";   Start time: 27.04.2023 01:00:00.000.0\n     1)        10.0  Rx     0CFF0008  8  03 00 00 00 00 00 00 00\n");
        foreach (string mode in new[] { "PerInputFile", "MergeToSingleFile", "SplitTrcByDate" })
            Check("UI batch route " + mode, () =>
            {
                string output = Path.Combine(_output, "batch-" + mode); Directory.CreateDirectory(output);
                SetInput(main, folder, description, output);
                object options = Get(main, "_saveOptions")!;
                options.GetType().GetProperty("OutputFormat")!.SetValue(options, EnumValue("OutputFormat", "Csv"));
                options.GetType().GetProperty("BatchMode")!.SetValue(options, EnumValue("BatchOutputMode", mode));
                Field<Button>(main, "buttonProcess").PerformClick();
                try
                {
                    Until(() => Get(main, "_operation") == null && Field<TextBox>(main, "textBoxLog").Text.Contains("Готово:")
                        && Field<Button>(main, "buttonOpenOutput").Visible && Field<Button>(main, "buttonProcess").Enabled, "batch " + mode, 15000);
                }
                catch (TimeoutException ex)
                {
                    Capture(main, "batch-failure-" + mode);
                    throw new InvalidOperationException($"{ex.Message}; idle={Get(main, "_operation") == null}; processEnabled={Field<Button>(main, "buttonProcess").Enabled}; openVisible={Field<Button>(main, "buttonOpenOutput").Visible}; journal={Field<TextBox>(main, "textBoxLog").Text}", ex);
                }
                string[] files = Directory.GetFiles(output, "*.csv");
                Require(files.Length == (mode == "MergeToSingleFile" ? 1 : 2), "Batch output file count mismatch.");
                Require(Field<Button>(main, "buttonOpenOutput").Visible && Field<Button>(main, "buttonProcess").Enabled, "Batch did not return successful idle UI.");
                if (mode == "MergeToSingleFile")
                    Require(File.ReadAllLines(files[0]).Contains("2023-04-26 23:59:59.995;1"), "Merged batch output values changed.");
                if (mode == "SplitTrcByDate")
                {
                    Require(File.ReadAllLines(Path.Combine(output, "result_2023-04-26.csv"))[1] == "2023-04-26 23:59:59.995;1", "First-day output changed.");
                    Require(File.ReadAllLines(Path.Combine(output, "result_2023-04-27.csv")).Contains("2023-04-27 00:00:00.010;2"), "Midnight split output changed.");
                }
            });
    }

    private static void CheckSaveOptions(string folder)
    {
        for (int i = 0; i < 3; i++)
        {
            int selection = i;
            Check("save options output and batch mode " + selection, () =>
            {
                using var form = SaveForm(folder); Show(form);
                Field<ComboBox>(form, "_comboOutputFormat").SelectedIndex = selection;
                Field<ComboBox>(form, "_comboBatchMode").SelectedIndex = selection;
                Field<CheckBox>(form, "_chkIncludeDeviceIdRow").Checked = true;
                Field<NumericUpDown>(form, "_numBlockPeriod").Value = 250;
                Field<NumericUpDown>(form, "_numBlockStart").Value = 17;
                ((Button)form.AcceptButton!).PerformClick(); Pump();
                string expectedFormat = new[] { "Xlsx", "Csv", "CsvDstConnect" }[selection];
                string expectedBatch = new[] { "PerInputFile", "MergeToSingleFile", "SplitTrcByDate" }[selection];
                Require(GetProperty(form, "SelectedOutputFormat")!.ToString() == expectedFormat, "Output mode selection mismatch.");
                Require(GetProperty(form, "SelectedBatchMode")!.ToString() == expectedBatch, "Batch mode selection mismatch.");
                Require((bool)GetProperty(form, "IncludeDeviceIdHeaderRow")!, "ID header setting not committed.");
                object dst = GetProperty(form, "SelectedDstConnectOptions")!;
                Require((int)GetProperty(dst, "BlockPeriodMs")! == 250 && (int)GetProperty(dst, "BlockStartIndex")! == 17, "DST settings not committed.");
            });
        }
    }

    private static Form SaveForm(string folder) => Form("SaveOptionsForm", EnumValue("OutputFormat", "Csv"), EnumValue("BatchOutputMode", "PerInputFile"),
        Activator.CreateInstance(_core.GetType("logReader.DstConnectOptions")!)!, folder, EnumValue("LogFormatKind", "All"), false);

    private static void CheckThemeAndKeyboard(Form main, string requestedTheme)
    {
        Type? manager = _ui.GetTypes().FirstOrDefault(t => t.Name == "ThemeManager");
        if (manager == null) return;
        Check("actual theme button switches and persists isolated choice", () =>
        {
            var button = main.Controls.Find("buttonTheme", true).OfType<Button>().Single();
            string before = manager.GetProperty("Mode", BindingFlags.Static | BindingFlags.Public)!.GetValue(null)!.ToString()!;
            Color color = main.BackColor; var timings = new List<double>();
            for (int i = 0; i < 6; i++) { var watch = Stopwatch.StartNew(); button.PerformClick(); Pump(); timings.Add(watch.Elapsed.TotalMilliseconds); }
            button.PerformClick(); Pump();
            string after = manager.GetProperty("Mode", BindingFlags.Static | BindingFlags.Public)!.GetValue(null)!.ToString()!;
            Require(before != after && color != main.BackColor, "Theme button did not change mode and window palette.");
            string preferences = Path.Combine(_output, "preferences.json");
            Require(File.Exists(preferences) && File.ReadAllText(preferences).Contains(after), "Theme preference was not saved in isolated file.");
            Capture(main, "theme-button-switched");
            Measurements.Add(new { name = "theme_switch_median", iterations = timings.Count, milliseconds = Median(timings) });
            button.PerformClick(); Pump();
            Require(manager.GetProperty("Mode", BindingFlags.Static | BindingFlags.Public)!.GetValue(null)!.ToString() == before, "Theme failed to switch back.");
        });
        MethodInfo? keyboard = main.GetType().GetMethod("WorkspaceKeyDown", Members);
        if (keyboard == null) return;
        Check("keyboard event routes switch pages and focus journal", () =>
        {
            keyboard.Invoke(main, [main, new KeyEventArgs(Keys.Control | Keys.D2)]); Pump();
            Require(main.Controls.Find("decoderPage", true).Single().Visible, "Ctrl+2 route failed.");
            keyboard.Invoke(main, [main, new KeyEventArgs(Keys.Control | Keys.D1)]); Pump();
            Require(main.Controls.Find("sessionPage", true).Single().Visible, "Ctrl+1 route failed.");
            keyboard.Invoke(main, [main, new KeyEventArgs(Keys.Control | Keys.L)]); Pump();
            Require(Field<TextBox>(main, "textBoxLog").Focused, "Ctrl+L focus route failed.");
        });
    }

    private static void CheckMessages(Form owner)
    {
        Type? box = _ui.GetType("logReader.UI.ThemedMessageBox");
        if (box == null) return;
        foreach (var choice in new[] { DialogResult.Yes, DialogResult.No, DialogResult.Cancel })
            Check("custom modal message choice " + choice, () =>
            {
                var show = box.GetMethod("Show", BindingFlags.Static | BindingFlags.Public)!;
                var result = WithDialogChoice(owner, choice, () => (DialogResult)show.Invoke(null,
                    [owner, "Сохранить изменения?\nПроверка реального модального подтверждения LOGER.", "UI audit confirmation", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question])!,
                    "message-" + choice.ToString().ToLowerInvariant());
                Require(result == choice, "Custom dialog result contract mismatch.");
            });
    }

    private static void CheckEditorConfirmations(string description)
    {
        if (_ui.GetType("logReader.UI.ThemedMessageBox") == null) return;
        Check("editor delete and dirty-close Yes/No/Cancel contracts preserve files", () =>
        {
            string path = Path.Combine(_output, "fixtures", "confirmation-test.dbc"); File.Copy(description, path, true);
            byte[] original = File.ReadAllBytes(path);
            using (var editor = Form("DevicesEditorForm", path))
            {
                Show(editor);
                var grid = Field<DataGridView>(editor, "_grid"); grid.CurrentCell = grid.Rows[0].Cells[0];
                WithDialogChoice(editor, DialogResult.No, () => { Field<Button>(editor, "_btnDelete").PerformClick(); return 0; });
                Require(grid.Rows.Count == 2, "No deleted a device.");
                WithDialogChoice(editor, DialogResult.Yes, () => { Field<Button>(editor, "_btnDelete").PerformClick(); return 0; });
                Require(grid.Rows.Count == 1 && original.SequenceEqual(File.ReadAllBytes(path)), "In-memory delete changed disk prematurely.");
                WithDialogChoice(editor, DialogResult.Cancel, () => { editor.Close(); return 0; });
                Require(editor.Visible && !editor.IsDisposed && original.SequenceEqual(File.ReadAllBytes(path)), "Cancel failed to retain editor and original file.");
                WithDialogChoice(editor, DialogResult.No, () => { editor.Close(); return 0; });
                Require(original.SequenceEqual(File.ReadAllBytes(path)), "Discard changed disk.");
            }
            using var saved = Form("DevicesEditorForm", path); Show(saved);
            var savedGrid = Field<DataGridView>(saved, "_grid"); savedGrid.CurrentCell = savedGrid.Rows[0].Cells[0];
            WithDialogChoice(saved, DialogResult.Yes, () => { Field<Button>(saved, "_btnDelete").PerformClick(); return 0; });
            WithDialogChoice(saved, DialogResult.Yes, () => { saved.Close(); return 0; });
            Require(!original.SequenceEqual(File.ReadAllBytes(path)), "Save confirmation did not write changes.");
            var loaded = (IList)Static("DbcFile", "Read", path)!;
            Require(loaded.Count == 1, "Saved deletion has incorrect device count.");
        });
    }

    private static T WithDialogChoice<T>(Form owner, DialogResult choice, Func<T> action, string? captureName = null)
    {
        bool clicked = false; Exception? timerError = null;
        using var timer = new System.Windows.Forms.Timer { Interval = 25 };
        timer.Tick += (_, _) =>
        {
            Form? dialog = Application.OpenForms.Cast<Form>().LastOrDefault(f => f != owner && f.Modal);
            if (dialog == null) return;
            timer.Stop();
            try
            {
                if (captureName != null) Capture(dialog, captureName);
                var button = Descendants(dialog).OfType<Button>().Single(b => b.DialogResult == choice);
                Require(InViewport(button), "Confirmation button is clipped."); button.PerformClick(); clicked = true;
            }
            catch (Exception ex) { timerError = ex; dialog.DialogResult = DialogResult.Cancel; dialog.Close(); }
        };
        timer.Start(); T result = action(); timer.Stop();
        if (timerError != null) throw timerError;
        Require(clicked, "Expected modal confirmation was not shown."); return result;
    }

    private static bool InViewport(Control control)
    {
        if (!control.Visible || control.Width <= 0 || control.Height <= 0) return false;
        Rectangle rectangle = control.RectangleToScreen(control.ClientRectangle);
        for (Control? parent = control.Parent; parent != null; parent = parent.Parent)
            if (!parent.RectangleToScreen(parent.ClientRectangle).Contains(rectangle)) return false;
        return true;
    }
    private static void ScrollIntoView(Control control)
    {
        for (Control? parent = control.Parent; parent != null; parent = parent.Parent)
            if (parent is ScrollableControl scroll && scroll.AutoScroll) scroll.ScrollControlIntoView(control);
        Pump();
    }

    private static void AuditMainRoutes(Form main)
    {
        var routes = new Dictionary<string, string>
        {
            ["buttonCANlog"] = "buttonCANlog_Click", ["buttonViewLog"] = "buttonViewLog_Click",
            ["buttonDevices"] = "buttonDevices_Click", ["buttonDevicesCreateOrAdd"] = "buttonDevicesCreateOrAdd_Click",
            ["buttonDevicesParams"] = "buttonDevicesParams_Click", ["buttonComposites"] = "buttonComposites_Click",
            ["buttonCompositesCreateOrAdd"] = "buttonCompositesCreateOrAdd_Click", ["buttonOutput"] = "buttonOutput_Click",
            ["buttonSaveOptions"] = "buttonSaveOptions_Click", ["buttonProcess"] = "buttonProcess_Click",
            ["buttonOpenOutput"] = "buttonOpenOutput_Click", ["buttonHelp"] = "buttonHelp_Click",
            ["buttonTrcToAsc"] = "buttonFormatConvert_Click", ["buttonCancel"] = "buttonCancel_Click"
        };
        object clickKey = typeof(Control).GetFields(BindingFlags.Static | BindingFlags.NonPublic).First(f => f.Name.Equals("s_clickEvent", StringComparison.OrdinalIgnoreCase) || f.Name == "EventClick").GetValue(null)!;
        foreach (var pair in routes)
            Check("event route " + pair.Key, () =>
            {
                var button = Field<Button>(main, pair.Key);
                var events = (EventHandlerList)typeof(Component).GetProperty("Events", Members)!.GetValue(button)!;
                var methods = events[clickKey]?.GetInvocationList().Select(d => d.Method.Name).ToArray() ?? [];
                Require(methods.Contains(pair.Value), "Missing wired handler " + pair.Value);
                Require(Descendants(main).Contains(button), "Control is not present in main form tree.");
            });
    }

    private static void Capture(Form form, string name)
    {
        Pump(); form.Update();
        string path = Path.Combine(_output, name + ".png");
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        string windowPath = Path.Combine(_output, name + "-window.png");
        bool windowCaptureSucceeded;
        using (var windowBitmap = new Bitmap(form.Width, form.Height))
        {
            using var graphics = Graphics.FromImage(windowBitmap);
            IntPtr hdc = graphics.GetHdc();
            try { windowCaptureSucceeded = Native.PrintWindow(form.Handle, hdc, 2); }
            finally { graphics.ReleaseHdc(hdc); }
            if (windowCaptureSucceeded) windowBitmap.Save(windowPath, System.Drawing.Imaging.ImageFormat.Png);
        }
        var controls = Descendants(form).Select(c => new
        {
            name = c.Name, type = c.GetType().Name, text = c.Text, visible = c.Visible, enabled = c.Enabled,
            x = c.Left, y = c.Top, width = c.Width, height = c.Height,
            parent = c.Parent?.Name, tabIndex = c.TabIndex, accessibleName = c.AccessibleName,
            background = c.BackColor.ToArgb(), foreground = c.ForeColor.ToArgb()
        }).ToArray();
        Screens.Add(new { name, path, windowPath = windowCaptureSucceeded ? windowPath : null, width = form.Width, height = form.Height, dpi = form.DeviceDpi, controls });
        File.WriteAllText(Path.Combine(_output, name + ".controls.json"), JsonSerializer.Serialize(controls, Json));
    }

    private static void ApplyTheme(string theme)
    {
        Type? manager = _ui.GetTypes().FirstOrDefault(t => t.Name == "ThemeManager");
        if (manager == null || theme == "existing") return;
        var method = manager.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(m => (m.Name == "SetTheme" || m.Name == "ApplyTheme" || m.Name == "SetMode") && m.GetParameters().Length >= 1 && m.GetParameters()[0].ParameterType.IsEnum);
        if (method == null) throw new InvalidOperationException("ThemeManager has no compatible theme selection API.");
        object kind = Enum.Parse(method.GetParameters()[0].ParameterType, theme, true);
        method.Invoke(null, method.GetParameters().Length == 2 ? [kind, false] : [kind]);
    }

    private static void SetInput(Form main, string log, string description, string output)
    {
        Field<TextBox>(main, "textBoxCanLog").Text = log;
        Field<TextBox>(main, "textBoxDevices").Text = description;
        Field<TextBox>(main, "textBoxOutput").Text = output; Pump();
    }
    private static Form Form(string name, params object?[] args) => (Form)Activator.CreateInstance(_ui.GetType("logReader.UI." + name, true)!, Members, null, args, null)!;
    private static object? Static(string type, string method, params object?[] args)
    {
        var member = _core.GetType("logReader." + type, true)!.GetMethods(BindingFlags.Static | BindingFlags.Public)
            .First(m => m.Name == method && m.GetParameters().Length >= args.Length && m.GetParameters().Skip(args.Length).All(p => p.HasDefaultValue));
        object?[] padded = args.Concat(member.GetParameters().Skip(args.Length).Select(p => p.DefaultValue)).ToArray();
        return member.Invoke(null, padded);
    }
    private static object EnumValue(string name, string value) => Enum.Parse(_core.GetTypes().First(t => t.Name == name), value);
    private static object? Get(object target, string name) => target.GetType().GetField(name, Members)!.GetValue(target);
    private static T Field<T>(object target, string name) => (T)Get(target, name)!;
    private static object? GetProperty(object target, string name) => target.GetType().GetProperty(name, Members)!.GetValue(target);
    private static object? GetStatic(Type type, string name) => type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!.GetValue(null);
    private static string Option(string[] args, string name, string fallback) { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback; }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Check(string name, Action action)
    {
        try { action(); Checks.Add(new { name, status = "PASS" }); Console.WriteLine("PASS " + name); }
        catch (Exception ex) { _failures++; string error = (ex is TargetInvocationException ? ex.InnerException : ex)?.ToString() ?? ex.ToString(); Checks.Add(new { name, status = "FAIL", error }); Console.WriteLine("FAIL " + name + ": " + error); }
    }
    private static void Show(Form form) { form.StartPosition = FormStartPosition.Manual; form.Location = new Point(30, 30); form.ShowInTaskbar = false; form.Show(); Pump(); form.PerformLayout(); form.Update(); Pump(); }
    private static void Pump()
    {
        // DoEvents starts and ends a temporary WinForms message loop. Ending the last loop
        // can uninstall its context; restore the persistent STA dispatcher so async form
        // handlers never resume on a thread-pool thread in this embedding harness.
        SynchronizationContext.SetSynchronizationContext(_context); Application.DoEvents();
        SynchronizationContext.SetSynchronizationContext(_context); Application.DoEvents();
        SynchronizationContext.SetSynchronizationContext(_context);
    }
    private static void Until(Func<bool> condition, string operation, int timeout)
    {
        var watch = Stopwatch.StartNew();
        while (!condition()) { Pump(); if (watch.ElapsedMilliseconds > timeout) throw new TimeoutException(operation); Thread.Sleep(5); }
        Pump();
    }
    private static IEnumerable<Control> Descendants(Control control)
    {
        foreach (Control child in control.Controls) { yield return child; foreach (var descendant in Descendants(child)) yield return descendant; }
    }
    private static double Median(List<double> data) { var sorted = data.Order().ToArray(); return sorted[sorted.Length / 2]; }
    private static class Native
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        internal static extern int GetGuiResources(IntPtr process, int flags);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        internal static extern bool PrintWindow(IntPtr handle, IntPtr hdc, uint flags);
    }
}
