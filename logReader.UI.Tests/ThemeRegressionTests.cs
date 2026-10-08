using System.Runtime.ExceptionServices;
using logReader.UI.Controls;
using logReader.UI.Theme;

namespace logReader.UI.Tests;

public class ThemeRegressionTests
{
    [Theory]
    [InlineData("main")]
    [InlineData("devices")]
    [InlineData("composites")]
    [InlineData("xlsx-message")]
    [InlineData("dbc-message")]
    [InlineData("dbc-signal")]
    [InlineData("xlsx-field")]
    [InlineData("composite-param")]
    [InlineData("save")]
    [InlineData("conversion")]
    [InlineData("file-kind")]
    [InlineData("device-selection")]
    [InlineData("log-view")]
    [InlineData("help")]
    public void Switching_open_forms_preserves_edited_values_and_native_data(string scenario) => UiThread.Run(() =>
    {
        using var theme = new IsolatedTheme();
        using var fixture = new UiFixtures();
        using Form form = CreateForm(scenario, fixture);
        UiThread.Show(form);
        if (form is CanLogViewForm)
            UiThread.Until(() => UiThread.Field<TextBox>(form, "textBoxSearch").Enabled);
        EditNativeValue(form, scenario, fixture);
        UiThread.Pump();
        var assertState = CaptureState(form);

        foreach (var mode in new[] { ThemeMode.Dark, ThemeMode.Light })
        {
            Assert.True(AppTheme.SetMode(mode, persist: false));
            UiThread.Pump();
            Assert.Equal(mode, AppTheme.CurrentMode);
            Assert.Equal(AppTheme.CurrentPalette.Background.ToArgb(), form.BackColor.ToArgb());
            assertState();
            foreach (var grid in UiThread.Descendants(form).OfType<DataGridView>())
            {
                Assert.Equal(AppTheme.Surface.ToArgb(), grid.BackgroundColor.ToArgb());
                Assert.Equal(AppTheme.TextPrimary.ToArgb(), grid.DefaultCellStyle.ForeColor.ToArgb());
                Assert.Equal(AppTheme.PrimarySoft.ToArgb(), grid.DefaultCellStyle.SelectionBackColor.ToArgb());
                Assert.Equal(AppTheme.SurfaceSecondary.ToArgb(), grid.ColumnHeadersDefaultCellStyle.BackColor.ToArgb());
                Assert.Equal(AppTheme.TextSecondary.ToArgb(), grid.ColumnHeadersDefaultCellStyle.ForeColor.ToArgb());
            }
        }
        if (form is FileKindPromptForm chooser)
        {
            chooser.AcceptButton!.PerformClick();
            Assert.Equal(FileKindPromptForm.FileKind.Dbf, chooser.SelectedKind);
        }
        else if (form is SaveOptionsForm options)
        {
            options.AcceptButton!.PerformClick();
            Assert.Equal(OutputFormat.CsvDstConnect, options.SelectedOutputFormat);
            Assert.Equal(123, options.SelectedDstConnectOptions.BlockPeriodMs);
        }
        else if (form is Devices_ParametrsForm selection)
        {
            selection.AcceptButton!.PerformClick();
            Assert.False(UiThread.Field<Dictionary<string, bool[]>>(selection, "_targetParamEnabled")["123"][0]);
        }
        Assert.False(File.Exists(theme.Path));
    });

    [Fact]
    public void Switching_keeps_existing_validation_and_operation_status() => UiThread.Run(() =>
    {
        using var theme = new IsolatedTheme();
        using var main = new MainForm();
        using var signal = new DbcSignalEditForm(UiFixtures.Signal, 8);
        UiThread.Show(main);
        UiThread.Show(signal);
        UiThread.Invoke(main, "PresentLogState", "Ошибка: проверка занятости файла.");
        var name = UiThread.Field<ModernTextBox>(signal, "_txtName");
        name.Text = "invalid signal name";
        signal.AcceptButton!.PerformClick();
        var notice = UiThread.Field<InlineNotice>(signal, "_validation");
        Assert.True(name.HasError);
        Assert.True(notice.Visible);
        string validation = notice.Text;
        var status = UiThread.Field<StatusBadge>(main, "_workspaceStatus");
        string statusText = status.Text;
        name.Select(2, 4);

        foreach (var mode in new[] { ThemeMode.Dark, ThemeMode.Light })
        {
            Assert.True(AppTheme.SetMode(mode, persist: false));
            UiThread.Pump();
            Assert.Equal("invalid signal name", name.Text);
            Assert.Equal(2, name.SelectionStart);
            Assert.Equal(4, name.SelectionLength);
            Assert.True(name.HasError);
            Assert.Equal(AppTheme.ErrorSoft.ToArgb(), name.BackColor.ToArgb());
            Assert.Equal(validation, notice.Text);
            Assert.True(notice.Visible);
            Assert.Equal(StatusTone.Error, notice.Tone);
            Assert.Equal(AppTheme.ErrorSoft.ToArgb(), notice.BackColor.ToArgb());
            Assert.Equal(AppTheme.Error.ToArgb(), UiThread.Descendants(notice).OfType<Label>().Single().ForeColor.ToArgb());
            Assert.Equal(StatusTone.Error, status.Tone);
            Assert.Equal(statusText, status.Text);
            Assert.True(UiThread.Field<bool>(main, "_hasOperationResult"));
        }
    });

    [Fact]
    public void Help_document_and_a_later_modal_use_the_active_dark_palette() => UiThread.Run(() =>
    {
        using var theme = new IsolatedTheme();
        using var help = new HelpForm();
        UiThread.Show(help);
        var document = UiThread.Field<RichTextBox>(help, "richTextBoxHelp");
        var topics = UiThread.Field<TreeView>(help, "treeViewTopics");
        string text = document.Text;
        string selectedTopic = topics.SelectedNode!.FullPath;
        Assert.NotEmpty(text);
        document.Select(0, 1);
        Assert.Equal(AppTheme.TextPrimary.ToArgb(), document.SelectionColor.ToArgb());

        Assert.True(AppTheme.SetMode(ThemeMode.Dark, persist: false));
        UiThread.Pump();
        Assert.Equal(text, document.Text);
        Assert.Equal(selectedTopic, topics.SelectedNode!.FullPath);
        Assert.Equal(0, document.SelectionStart);
        Assert.Equal(1, document.SelectionLength);
        document.Select(0, 1);
        Assert.Equal(AppTheme.TextPrimary.ToArgb(), document.SelectionColor.ToArgb());
        Assert.Equal(AppTheme.Surface.ToArgb(), document.BackColor.ToArgb());

        ExceptionDispatchInfo? failure = null;
        bool inspected = false;
        const string caption = "Проверка тёмного подтверждения";
        using var responder = new System.Windows.Forms.Timer { Interval = 20 };
        responder.Tick += (_, _) =>
        {
            var dialog = Application.OpenForms.Cast<Form>()
                .FirstOrDefault(candidate => candidate != help && candidate.Modal && candidate.Text == caption);
            if (dialog == null) return;
            responder.Stop();
            try
            {
                Assert.Equal(AppTheme.Background.ToArgb(), dialog.BackColor.ToArgb());
                var message = UiThread.Descendants(dialog).OfType<RichTextBox>().Single();
                Assert.Equal(AppTheme.TextPrimary.ToArgb(), message.ForeColor.ToArgb());
                Assert.Equal(AppTheme.Background.ToArgb(), message.BackColor.ToArgb());
                inspected = true;
                UiThread.Descendants(dialog).OfType<Button>()
                    .Single(button => button.DialogResult == DialogResult.No).PerformClick();
            }
            catch (Exception ex)
            {
                failure = ExceptionDispatchInfo.Capture(ex);
                dialog.DialogResult = DialogResult.Cancel;
                dialog.Close();
            }
        };
        responder.Start();
        var answer = AppDialog.Show(help, "Продолжить с выбранными настройками?", caption,
            MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        failure?.Throw();
        Assert.True(inspected);
        Assert.Equal(DialogResult.No, answer);
    });

    [Theory]
    [InlineData(ThemeMode.Light)]
    [InlineData(ThemeMode.Dark)]
    public void Semantic_palettes_keep_text_and_statuses_readable(ThemeMode mode)
    {
        var palette = ThemePalette.For(mode);
        foreach (var background in new[] { palette.Background, palette.Surface, palette.SurfaceSecondary })
        {
            AssertContrast(palette.TextPrimary, background, 4.5);
            AssertContrast(palette.TextSecondary, background, 4.5);
            AssertContrast(palette.TextMuted, background, 3);
        }
        AssertContrast(palette.TextOnPrimary, palette.Primary, 4.5);
        AssertContrast(palette.TextOnError, palette.Error, 4.5);
        AssertContrast(palette.Success, palette.SuccessSoft, 4.5);
        AssertContrast(palette.Warning, palette.WarningSoft, 4.5);
        AssertContrast(palette.Error, palette.ErrorSoft, 4.5);
        AssertContrast(palette.Info, palette.PrimarySoft, 4.5);
        Assert.Equal(ThemePalette.Light.PayloadColors, ThemePalette.Dark.PayloadColors);
    }

    [Fact]
    public void Preferences_round_trip_in_an_isolated_file_and_corruption_falls_back() => UiThread.Run(() =>
    {
        using var theme = new IsolatedTheme();
        int changes = 0;
        EventHandler handler = (_, _) => changes++;
        AppTheme.Changed += handler;
        try
        {
            Assert.True(AppTheme.SetMode(ThemeMode.Dark, persist: false));
            Assert.Equal(1, changes);
            Assert.False(File.Exists(theme.Path));
            Assert.True(AppTheme.SetMode(ThemeMode.Dark));
            Assert.Equal(1, changes);
            Assert.Equal(ThemeMode.Dark, ThemePreferences.Load(theme.Path));
            Assert.Null(AppTheme.LastPersistenceError);
            Assert.True(AppTheme.SetMode(ThemeMode.Light, persist: false));
            AppTheme.Initialize(theme.Path);
            Assert.Equal(ThemeMode.Dark, AppTheme.CurrentMode);
            Assert.Equal(System.IO.Path.GetFullPath(theme.Path), AppTheme.PreferenceFilePath);

            using var main = new MainForm();
            UiThread.Show(main);
            var input = UiThread.Field<TextBox>(main, "textBoxCanLog");
            const string typedPath = @"C:\missing-fixture\typed-input.trc";
            input.Text = typedPath;
            var toggle = UiThread.Field<Button>(main, "_themeButton");
            foreach (var mode in new[] { ThemeMode.Light, ThemeMode.Dark })
            {
                toggle.PerformClick();
                UiThread.Pump();
                Assert.Equal(mode, AppTheme.CurrentMode);
                Assert.Equal(mode, ThemePreferences.Load(theme.Path));
                Assert.Equal(typedPath, input.Text);
                Assert.Null(AppTheme.LastPersistenceError);
            }

            File.WriteAllText(theme.Path, "{\"theme\":");
            AppTheme.Initialize(theme.Path);
            Assert.Equal(ThemeMode.Light, AppTheme.CurrentMode);
            Assert.False(string.IsNullOrWhiteSpace(AppTheme.LastPersistenceError));
        }
        finally { AppTheme.Changed -= handler; }
    });

    [Fact]
    public void Preference_write_failure_reports_error_without_preventing_switching() => UiThread.Run(() =>
    {
        using var theme = new IsolatedTheme();
        string blocked = System.IO.Path.Combine(theme.Root, "ordinary-file");
        File.WriteAllText(blocked, "This is a file, not a preference directory.");
        AppTheme.Initialize(System.IO.Path.Combine(blocked, "theme.json"));
        Assert.False(AppTheme.SetMode(ThemeMode.Dark));
        Assert.Equal(ThemeMode.Dark, AppTheme.CurrentMode);
        Assert.False(string.IsNullOrWhiteSpace(AppTheme.LastPersistenceError));
        Assert.True(AppTheme.SetMode(ThemeMode.Light, persist: false));
        Assert.Equal(ThemeMode.Light, AppTheme.CurrentMode);
    });

    private static Form CreateForm(string scenario, UiFixtures fixture) => scenario switch
    {
        "main" => new MainForm(),
        "devices" => new DevicesEditorForm(fixture.Xlsx),
        "composites" => new CompositeEditorForm(fixture.Composites),
        "xlsx-message" => new XlsxMessageEditForm(UiFixtures.Definition),
        "dbc-message" => new DbcMessageEditForm(UiFixtures.Message),
        "dbc-signal" => new DbcSignalEditForm(UiFixtures.Signal, 8),
        "xlsx-field" => new DeviceFieldRowEditForm(UiFixtures.FieldRow, 8, 0),
        "composite-param" => new CompositeParamEditForm(UiFixtures.Composite("Speed")),
        "save" => new SaveOptionsForm(OutputFormat.Csv, BatchOutputMode.PerInputFile, new(), fixture.LogFolder, LogFormatKind.All),
        "conversion" => new FormatConversionDialog([new("trc_to_asc", "TRC → ASC", ".trc", ".asc")], fixture.Trc, _ => { }),
        "file-kind" => new FileKindPromptForm(),
        "device-selection" => new Devices_ParametrsForm([UiFixtures.Device], new(), new()),
        "log-view" => new CanLogViewForm(fixture.Trc),
        "help" => new HelpForm(),
        _ => throw new ArgumentOutOfRangeException(nameof(scenario))
    };

    private static void EditNativeValue(Form form, string scenario, UiFixtures fixture)
    {
        if (scenario == "save")
        {
            UiThread.Field<NumericUpDown>(form, "_numBlockPeriod").Value = 123;
            UiThread.Field<ComboBox>(form, "_comboOutputFormat").SelectedIndex = 2;
        }
        else if (scenario == "file-kind")
            UiThread.Descendants(form).OfType<Button>().Single(button => button.AccessibleName == "Выбрать DBF .dbf").PerformClick();
        else if (scenario == "device-selection")
            UiThread.Field<TreeView>(form, "_tree").Nodes[0].Nodes[0].Checked = false;
        else
        {
            var (field, value) = scenario switch
            {
                "main" => ("textBoxCanLog", "edited-input.trc"),
                "devices" => ("_txtSearch", "Engine"),
                "composites" => ("_txtSearch", "Temperature"),
                "xlsx-message" or "dbc-message" => ("_txtName", "EditedEngine"),
                "dbc-signal" or "xlsx-field" => ("_txtName", "EditedSignal"),
                "composite-param" => ("_txtParam", "EditedComposite"),
                "conversion" => ("_outputPathTextBox", System.IO.Path.Combine(fixture.Root, "edited.asc")),
                "log-view" => ("textBoxSearch", "123"),
                "help" => ("textBoxSearch", "формат"),
                _ => throw new ArgumentOutOfRangeException(nameof(scenario))
            };
            UiThread.Field<TextBox>(form, field).Text = value;
        }
    }

    private static Action CaptureState(Form form)
    {
        var checks = new List<Action>();
        foreach (var control in UiThread.Descendants(form))
        {
            if (control is TextBoxBase text)
            {
                string value = text.Text;
                checks.Add(() => Assert.Equal(value, text.Text));
            }
            else if (control is ComboBox combo)
            {
                int selected = combo.SelectedIndex;
                checks.Add(() => Assert.Equal(selected, combo.SelectedIndex));
            }
            else if (control is NumericUpDown number)
            {
                decimal value = number.Value;
                checks.Add(() => Assert.Equal(value, number.Value));
            }
            else if (control is CheckBox check)
            {
                bool value = check.Checked;
                checks.Add(() => Assert.Equal(value, check.Checked));
            }
            else if (control is RadioButton radio)
            {
                bool value = radio.Checked;
                checks.Add(() => Assert.Equal(value, radio.Checked));
            }
            else if (control is CheckedListBox list)
            {
                var values = Enumerable.Range(0, list.Items.Count).Select(list.GetItemChecked).ToArray();
                checks.Add(() => Assert.Equal(values, Enumerable.Range(0, list.Items.Count).Select(list.GetItemChecked).ToArray()));
            }
            else if (control is TreeView tree)
            {
                var values = TreeState(tree.Nodes).ToArray();
                checks.Add(() => Assert.Equal(values, TreeState(tree.Nodes).ToArray()));
            }
            else if (control is DataGridView grid)
            {
                int rows = grid.RowCount;
                var values = GridValues(grid);
                checks.Add(() => { Assert.Equal(rows, grid.RowCount); Assert.Equal(values, GridValues(grid)); });
            }
            else if (control is InlineNotice notice)
            {
                string textValue = notice.Text;
                var tone = notice.Tone;
                checks.Add(() => { Assert.Equal(textValue, notice.Text); Assert.Equal(tone, notice.Tone); });
            }
            else if (control is StatusBadge status)
            {
                string textValue = status.Text;
                var tone = status.Tone;
                checks.Add(() => { Assert.Equal(textValue, status.Text); Assert.Equal(tone, status.Tone); });
            }
        }
        return () => { foreach (var check in checks) check(); };
    }

    private static IEnumerable<(string Path, bool Checked)> TreeState(TreeNodeCollection nodes)
    {
        foreach (TreeNode node in nodes)
        {
            yield return (node.FullPath, node.Checked);
            foreach (var child in TreeState(node.Nodes)) yield return child;
        }
    }

    private static string?[] GridValues(DataGridView grid) => grid.Rows.Cast<DataGridViewRow>()
        .SelectMany(row => row.Cells.Cast<DataGridViewCell>()).Select(cell => cell.Value?.ToString()).ToArray();

    private static void AssertContrast(Color ink, Color background, double minimum)
    {
        static double Luminance(Color color)
        {
            static double Linear(byte channel)
            {
                double value = channel / 255d;
                return value <= .04045 ? value / 12.92 : Math.Pow((value + .055) / 1.055, 2.4);
            }
            return .2126 * Linear(color.R) + .7152 * Linear(color.G) + .0722 * Linear(color.B);
        }
        double first = Luminance(ink), second = Luminance(background);
        double ratio = (Math.Max(first, second) + .05) / (Math.Min(first, second) + .05);
        Assert.True(ratio >= minimum, $"Text {ink} on {background} has contrast {ratio:F2}; expected at least {minimum:F1}.");
    }

    private sealed class IsolatedTheme : IDisposable
    {
        private readonly ThemeMode previousMode = AppTheme.CurrentMode;
        private readonly string previousPath = AppTheme.PreferenceFilePath;
        internal string Root { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "logReader-theme-tests-" + Guid.NewGuid().ToString("N"));
        internal string Path => System.IO.Path.Combine(Root, "theme.json");

        internal IsolatedTheme()
        {
            Directory.CreateDirectory(Root);
            AppTheme.Initialize(Path);
            Assert.True(AppTheme.SetMode(ThemeMode.Light, persist: false));
        }

        public void Dispose()
        {
            AppTheme.Initialize(previousPath);
            AppTheme.SetMode(previousMode, persist: false);
            Directory.Delete(Root, recursive: true);
        }
    }
}
