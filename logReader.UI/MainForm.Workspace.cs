namespace logReader.UI;

// Composition and presentation only. Processing handlers live in MainForm.cs.
public partial class MainForm
{
    private readonly List<Font> _workspaceFonts = new();
    private readonly List<Control> _deferredWorkspaceLayouts = new();
    private TableLayoutPanel _workspaceColumns = null!;
    private Panel _sessionPage = null!;
    private Panel _decoderPage = null!;
    private Label _workspaceTitle = null!;
    private Label _workspaceSubtitle = null!;
    private Label _workspaceState = null!;
    private Label _sourceName = null!;
    private Label _sourceInfo = null!;
    private Label _decoderName = null!;
    private Label _decoderInfo = null!;
    private Label _exportFormat = null!;
    private Label _exportMode = null!;
    private Label _readinessSource = null!;
    private Label _readinessDecoder = null!;
    private Label _readinessOutput = null!;
    private Button _navSession = null!;
    private Button _navDecoder = null!;
    private Button _buttonTheme = null!;
    private Label _operationNotice = null!;
    private bool _workspaceReady;
    private RowStyle _executionFooterRow = null!;

    private Font WorkspaceFont(float size, FontStyle style = FontStyle.Regular, string family = "Segoe UI")
    {
        var font = new Font(family, size, style);
        _workspaceFonts.Add(font);
        return font;
    }

    private void DeferWorkspaceLayout(Control control)
    {
        control.SuspendLayout();
        _deferredWorkspaceLayouts.Add(control);
    }

    private static TextBox WorkspacePath(string name, int tabIndex) => new()
    {
        Name = name, Dock = DockStyle.Fill, TabIndex = tabIndex,
        BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(0, 4, 0, 4),
        AccessibleName = name switch
        {
            "textBoxCanLog" => "Путь к логу или папке",
            "textBoxDevices" => "Путь к описанию CAN-посылок",
            "textBoxComposites" => "Путь к составным параметрам",
            _ => "Путь результата"
        }
    };

    private static Label WorkspaceLabel(string text, string name = "", string? tag = null) => new()
    {
        Text = text, Name = name, Tag = tag, AutoSize = true,
        Margin = new Padding(0, 0, 0, 8), BackColor = Color.Transparent
    };

    private static Button WorkspaceButton(string text, string name, EventHandler action, string? tag = null)
    {
        var button = new Button
        {
            Text = text, Name = name, Tag = tag, AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink, MinimumSize = new Size(100, 34),
            Padding = new Padding(12, 4, 12, 4), Margin = new Padding(0, 0, 8, 0),
            FlatStyle = FlatStyle.Flat, AccessibleName = text.Replace("…", ""), Cursor = Cursors.Hand
        };
        button.Click += action;
        button.TextChanged += (_, _) => button.AccessibleName = button.Text.Replace("…", "");
        return button;
    }

    private Font? _cardTitleFont;
    private Font? _eyebrowFont;
    private Panel WorkspaceCard(string title, string eyebrow, Control body)
    {
        var panel = new WorkspaceCardPanel(title, eyebrow,
            _cardTitleFont ??= WorkspaceFont(14F, FontStyle.Bold),
            _eyebrowFont ??= WorkspaceFont(8F, FontStyle.Bold), body);
        DeferWorkspaceLayout(panel);
        return panel;
    }

    private VerticalStackPanel WorkspaceStack()
    {
        var stack = new VerticalStackPanel
        {
            AutoSize = true, Dock = DockStyle.Top,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = Padding.Empty, Padding = Padding.Empty, BackColor = Color.Transparent
        };
        DeferWorkspaceLayout(stack);
        return stack;
    }

    private FlowLayoutPanel WorkspaceActions(params Control[] controls)
    {
        var actions = new FlowLayoutPanel
        {
            AutoSize = true, Dock = DockStyle.Fill, WrapContents = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 8, 0, 0), BackColor = Color.Transparent
        };
        DeferWorkspaceLayout(actions);
        actions.Controls.AddRange(controls);
        return actions;
    }

    private void BuildWorkspace()
    {
        var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, Tag = "background" };
        DeferWorkspaceLayout(shell);
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 188));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(shell);
        shell.Controls.Add(BuildWorkspaceNavigation(), 0, 0);
        var workspace = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2,
            Padding = new Padding(24, 0, 24, 20), Margin = Padding.Empty, Tag = "background"
        };
        DeferWorkspaceLayout(workspace);
        workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, 108));
        workspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        shell.Controls.Add(workspace, 1, 0);
        workspace.Controls.Add(BuildWorkspaceHeader(), 0, 0);
        workspace.Controls.Add(contentSplit, 0, 1);
        _workspaceColumns = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, Tag = "background" };
        DeferWorkspaceLayout(_workspaceColumns);
        _workspaceColumns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _workspaceColumns.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 306));
        _workspaceColumns.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        contentSplit.Panel1.Controls.Add(_workspaceColumns);
        var pages = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 18, 0), Tag = "background" };
        DeferWorkspaceLayout(pages);
        _sessionPage = BuildSessionPage();
        _decoderPage = BuildDecoderPage();
        pages.Controls.Add(_decoderPage);
        pages.Controls.Add(_sessionPage);
        _workspaceColumns.Controls.Add(pages, 0, 0);
        _workspaceColumns.Controls.Add(BuildExportInspector(), 1, 0);
        BuildWorkspaceJournal();
        foreach (var path in new[] { textBoxCanLog, textBoxDevices, textBoxComposites, textBoxOutput })
            path.TextChanged += (_, _) => { buttonOpenOutput.Visible = false; RefreshWorkspaceSummary(); };
        labelFilterStatus.TextChanged += (_, _) => RefreshWorkspaceSummary();
        buttonOpenOutput.VisibleChanged += (_, _) => { ResizeExecutionFooter(); RefreshWorkspaceSummary(); };
        textBoxCanLog.AllowDrop = true;
        _sessionPage.AllowDrop = true;
        textBoxCanLog.DragEnter += SourceDragEnter;
        _sessionPage.DragEnter += SourceDragEnter;
        textBoxCanLog.DragDrop += SourceDragDrop;
        _sessionPage.DragDrop += SourceDragDrop;
        Resize += (_, _) => ResizeWorkspaceInspector();
        KeyDown += WorkspaceKeyDown;
        _workspaceReady = true;
        ThemeManager.Attach(this);
        ThemeManager.ThemeChanged += WorkspaceThemeChanged;
        Shown += (_, _) => RefreshWorkspaceSummary();
        SelectWorkspacePage(false);
        RefreshWorkspaceSummary();
        // Release batched containers; the root layout arranges their final widths once.
        // Without batching, every Add recalculates the full AutoSize ancestor chain.
        for (int i = _deferredWorkspaceLayouts.Count - 1; i >= 0; i--)
            _deferredWorkspaceLayouts[i].ResumeLayout(false);
        _deferredWorkspaceLayouts.Clear();
    }

    private Control BuildWorkspaceNavigation()
    {
        var rail = new TableLayoutPanel
        {
            Name = "workspaceNavigation", Tag = "rail", Dock = DockStyle.Fill,
            ColumnCount = 1, RowCount = 6, Padding = new Padding(14, 26, 14, 18), Margin = Padding.Empty
        };
        DeferWorkspaceLayout(rail);
        foreach (var height in new[] { 104, 28, 100 }) rail.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        rail.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        rail.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
        rail.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        rail.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var brand = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Size = new Size(150, 74), Margin = Padding.Empty };
        var mark = new Label
        {
            Text = "L", Size = new Size(34, 34), Location = new Point(0, 0),
            Font = WorkspaceFont(16F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter,
            Name = "brandMark", Tag = "brand"
        };
        var wordmark = WorkspaceLabel("LOGER");
        wordmark.Font = WorkspaceFont(20F, FontStyle.Bold);
        wordmark.Location = new Point(43, -1);
        var version = WorkspaceLabel("2.0  /  CAN WORKSPACE", tag: "muted");
        version.Font = WorkspaceFont(7.5F, FontStyle.Bold);
        version.Location = new Point(0, 49);
        brand.Controls.AddRange(new Control[] { mark, wordmark, version });
        rail.Controls.Add(brand, 0, 0);
        rail.Controls.Add(WorkspaceLabel("РАБОЧИЙ СЕАНС", tag: "muted"), 0, 1);
        var nav = WorkspaceStack();
        _navSession = WorkspaceButton("01   Обработка", "navSession", (_, _) => SelectWorkspacePage(false), "nav");
        _navDecoder = WorkspaceButton("02   Декодирование", "navDecoder", (_, _) => SelectWorkspacePage(true), "nav");
        foreach (var button in new[] { _navSession, _navDecoder })
        {
            button.AutoSize = false; button.Dock = DockStyle.Top; button.Width = 160; button.Height = 42;
            button.TextAlign = ContentAlignment.MiddleLeft; button.Margin = new Padding(0, 0, 0, 6);
            nav.Controls.Add(button);
        }
        rail.Controls.Add(nav, 0, 2);
        var tools = WorkspaceStack();
        foreach (var button in new[] { buttonTrcToAsc, buttonHelp })
        {
            button.AutoSize = false; button.Dock = DockStyle.Top; button.Width = 160; button.Height = 40;
            button.TextAlign = ContentAlignment.MiddleLeft; button.Margin = new Padding(0, 0, 0, 6);
            tools.Controls.Add(button);
        }
        rail.Controls.Add(tools, 0, 4);
        _buttonTheme = WorkspaceButton("Светлая тема", "buttonTheme", (_, _) =>
            ThemeManager.SetMode(ThemeManager.Mode == ThemeMode.Dark ? ThemeMode.Light : ThemeMode.Dark));
        _buttonTheme.AutoSize = false; _buttonTheme.Dock = DockStyle.Top; _buttonTheme.Width = 160;
        rail.Controls.Add(_buttonTheme, 0, 5);
        return rail;
    }

    private Control BuildWorkspaceHeader()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3,
            Padding = new Padding(0, 18, 0, 16), Margin = Padding.Empty, Tag = "background"
        };
        DeferWorkspaceLayout(header);
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        var kicker = WorkspaceLabel("LOGER  /  РАБОЧЕЕ ПРОСТРАНСТВО", tag: "muted");
        kicker.Font = WorkspaceFont(8F, FontStyle.Bold);
        _workspaceTitle = WorkspaceLabel("Обработка логов");
        _workspaceTitle.Font = WorkspaceFont(23F, FontStyle.Bold);
        _workspaceSubtitle = WorkspaceLabel("От CAN-пакетов к готовому набору данных.", tag: "muted");
        _workspaceState = WorkspaceLabel("Новый сеанс", "workspaceState", "muted");
        _workspaceState.Dock = DockStyle.Fill; _workspaceState.TextAlign = ContentAlignment.MiddleRight;
        header.Controls.Add(kicker, 0, 0); header.Controls.Add(_workspaceTitle, 0, 1);
        header.Controls.Add(_workspaceSubtitle, 0, 2); header.Controls.Add(_workspaceState, 1, 1);
        return header;
    }

    private Panel BuildSessionPage()
    {
        var page = new Panel { Name = "sessionPage", Dock = DockStyle.Fill, AutoScroll = true, Tag = "background" };
        DeferWorkspaceLayout(page);
        var stack = WorkspaceStack();
        stack.Tag = "background";
        var source = WorkspaceStack();
        _sourceName = WorkspaceLabel("Добавьте CAN-лог", "sourceName");
        _sourceName.Font = WorkspaceFont(12F, FontStyle.Bold);
        _sourceName.AutoSize = false; _sourceName.AutoEllipsis = true; _sourceName.Dock = DockStyle.Top; _sourceName.Height = 24;
        _sourceName.Margin = new Padding(0, 0, 0, 4);
        _sourceInfo = WorkspaceLabel("CSV · TRC · ASC · TXT CANfox / PCAN-View", "sourceInfo", "muted");
        _sourceInfo.Dock = DockStyle.Top; _sourceInfo.AutoSize = false; _sourceInfo.Height = 20;
        _sourceInfo.Margin = new Padding(0, 0, 0, 4);
        source.Controls.Add(_sourceName); source.Controls.Add(_sourceInfo);
        source.Controls.Add(textBoxCanLog); source.Controls.Add(WorkspaceActions(buttonCANlog, buttonViewLog));
        var hint = WorkspaceLabel("Перетащите файл или папку в поле источника.", tag: "muted");
        hint.Margin = new Padding(0, 8, 0, 0); source.Controls.Add(hint);
        stack.Controls.Add(WorkspaceCard("Источник данных", "01  /  ВХОДНЫЕ ДАННЫЕ", source));
        var decoder = WorkspaceStack();
        _decoderName = WorkspaceLabel("Описание посылок не выбрано", "decoderName");
        _decoderName.AutoSize = false; _decoderName.Dock = DockStyle.Top; _decoderName.Height = 24; _decoderName.AutoEllipsis = true;
        _decoderName.Margin = new Padding(0, 0, 0, 4);
        _decoderInfo = WorkspaceLabel("Подключите XLSX, DBC или DBF в разделе декодирования.", "decoderInfo", "muted");
        _decoderInfo.AutoSize = false; _decoderInfo.Dock = DockStyle.Top; _decoderInfo.Height = 32;
        _decoderInfo.Margin = new Padding(0, 0, 0, 4);
        var setup = WorkspaceButton("Настроить декодирование →", "buttonDecoderSetup", (_, _) => SelectWorkspacePage(true));
        decoder.Controls.Add(_decoderName); decoder.Controls.Add(_decoderInfo); decoder.Controls.Add(WorkspaceActions(setup));
        stack.Controls.Add(WorkspaceCard("Декодирование", "02  /  КОНФИГУРАЦИЯ", decoder));
        page.Controls.Add(stack);
        return page;
    }

    private Panel BuildDecoderPage()
    {
        var page = new Panel { Name = "decoderPage", Dock = DockStyle.Fill, AutoScroll = true, Visible = false, Tag = "background" };
        DeferWorkspaceLayout(page);
        var stack = WorkspaceStack();
        stack.Tag = "background";
        var devices = WorkspaceStack();
        devices.Controls.Add(WorkspaceLabel("XLSX · DBC · DBF  /  файлы описания посылок", tag: "muted"));
        devices.Controls.Add(textBoxDevices); devices.Controls.Add(WorkspaceActions(buttonDevices, buttonDevicesCreateOrAdd));
        devices.Controls.Add(WorkspaceActions(buttonDevicesParams));
        labelFilterStatus.Margin = new Padding(0, 12, 0, 0); devices.Controls.Add(labelFilterStatus);
        stack.Controls.Add(WorkspaceCard("Описание CAN-посылок", "01  /  УСТРОЙСТВА И СИГНАЛЫ", devices));
        var composites = WorkspaceStack();
        var explanation = WorkspaceLabel("Опциональный XLSX: объединение сигналов в составные параметры.", tag: "muted");
        explanation.AutoSize = false; explanation.Dock = DockStyle.Top; explanation.Height = 40;
        composites.Controls.Add(explanation); composites.Controls.Add(textBoxComposites);
        composites.Controls.Add(WorkspaceActions(buttonComposites, buttonCompositesCreateOrAdd));
        stack.Controls.Add(WorkspaceCard("Составные параметры", "02  /  РАСШИРЕННОЕ ДЕКОДИРОВАНИЕ", composites));
        stack.Controls.Add(WorkspaceActions(WorkspaceButton("← Вернуться к обработке", "buttonBackToSession", (_, _) => SelectWorkspacePage(false))));
        page.Controls.Add(stack);
        return page;
    }

    private Control BuildExportInspector()
    {
        var inspector = new Panel
        {
            Name = "exportInspector", Dock = DockStyle.Fill,
            Tag = "surface", Padding = new Padding(20), Margin = Padding.Empty
        };
        DeferWorkspaceLayout(inspector);
        var stack = WorkspaceStack();
        var heading = WorkspaceLabel("Экспорт"); heading.Font = WorkspaceFont(18F, FontStyle.Bold);
        var kicker = WorkspaceLabel("03  /  РЕЗУЛЬТАТ", tag: "muted"); kicker.Font = WorkspaceFont(8F, FontStyle.Bold);
        stack.Controls.Add(kicker); stack.Controls.Add(heading);
        _exportFormat = WorkspaceLabel("CSV", "exportFormat");
        _exportFormat.Font = WorkspaceFont(12F, FontStyle.Bold); _exportFormat.Margin = new Padding(0, 12, 0, 4);
        _exportMode = WorkspaceLabel("Отдельный файл для каждого лога", "exportMode", "muted");
        _exportMode.AutoSize = false; _exportMode.Dock = DockStyle.Top; _exportMode.Height = 30;
        stack.Controls.Add(_exportFormat); stack.Controls.Add(_exportMode);
        labelResult.Margin = new Padding(0, 12, 0, 4); stack.Controls.Add(labelResult);
        stack.Controls.Add(textBoxOutput); stack.Controls.Add(WorkspaceActions(buttonOutput));
        stack.Controls.Add(WorkspaceActions(buttonSaveOptions));
        var readyTitle = WorkspaceLabel("ГОТОВНОСТЬ СЕАНСА", tag: "muted");
        readyTitle.Font = WorkspaceFont(8F, FontStyle.Bold); readyTitle.Margin = new Padding(0, 16, 0, 10);
        stack.Controls.Add(readyTitle);
        _readinessSource = AddReadiness(stack, "Источник логов");
        _readinessDecoder = AddReadiness(stack, "Описание посылок");
        _readinessOutput = AddReadiness(stack, "Путь результата");
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
        DeferWorkspaceLayout(layout);
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _executionFooterRow = new RowStyle(SizeType.Absolute, 96);
        layout.RowStyles.Add(_executionFooterRow);
        var settings = new Panel { Name = "exportSettings", Dock = DockStyle.Fill, AutoScroll = true, Margin = Padding.Empty };
        DeferWorkspaceLayout(settings);
        settings.Controls.Add(stack);
        layout.Controls.Add(settings, 0, 0);
        var execution = WorkspaceStack();
        execution.Name = "executionActions";
        execution.IncludeChild = child => child != progressBarProcess && child != labelProgress && child != buttonCancel || IsBusy;
        execution.Padding = new Padding(0, 12, 0, 0);
        buttonProcess.Margin = new Padding(0, 0, 0, 8);
        buttonProcess.MinimumSize = new Size(100, 46);
        execution.Controls.Add(buttonProcess); execution.Controls.Add(progressBarProcess); execution.Controls.Add(labelProgress);
        execution.Controls.Add(buttonCancel); execution.Controls.Add(WorkspaceActions(buttonOpenOutput));
        var shortcut = WorkspaceLabel("Ctrl + Enter — обработать", tag: "muted");
        shortcut.Font = WorkspaceFont(8F); shortcut.Margin = new Padding(0, 10, 0, 0); execution.Controls.Add(shortcut);
        layout.Controls.Add(execution, 0, 1);
        inspector.Controls.Add(layout);
        return inspector;
    }

    private static Label AddReadiness(Control stack, string text)
    {
        var label = WorkspaceLabel("○  " + text, tag: "muted");
        label.Dock = DockStyle.Top; label.AutoSize = false; label.AutoEllipsis = true; label.Height = 25; label.Margin = Padding.Empty;
        stack.Controls.Add(label);
        return label;
    }

    private void BuildWorkspaceJournal()
    {
        var journal = new TableLayoutPanel
        {
            Name = "operationJournal", Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2,
            Padding = new Padding(14, 10, 14, 8), Margin = Padding.Empty, Tag = "surface"
        };
        DeferWorkspaceLayout(journal);
        journal.RowStyles.Add(new RowStyle(SizeType.Absolute, 26)); journal.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty, BackColor = Color.Transparent };
        DeferWorkspaceLayout(header);
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 156)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var title = WorkspaceLabel("ЖУРНАЛ ОПЕРАЦИЙ", tag: "muted"); title.Font = WorkspaceFont(8F, FontStyle.Bold);
        _operationNotice = WorkspaceLabel("События обработки появятся здесь.", "operationNotice", "muted");
        _operationNotice.AutoSize = false; _operationNotice.AutoEllipsis = true; _operationNotice.Dock = DockStyle.Fill;
        _operationNotice.TextAlign = ContentAlignment.TopRight;
        header.Controls.Add(title, 0, 0); header.Controls.Add(_operationNotice, 1, 0);
        journal.Controls.Add(header, 0, 0); journal.Controls.Add(textBoxLog, 0, 1); contentSplit.Panel2.Controls.Add(journal);
    }

    private void SelectWorkspacePage(bool decoder)
    {
        if (!_workspaceReady) return;
        _sessionPage.Visible = !decoder; _decoderPage.Visible = decoder;
        (decoder ? _decoderPage : _sessionPage).BringToFront();
        _workspaceTitle.Text = decoder ? "Декодирование" : "Обработка логов";
        _workspaceSubtitle.Text = decoder ? "Устройства, сигналы и составные параметры." : "От CAN-пакетов к готовому набору данных.";
        _navSession.Tag = decoder ? "nav" : "nav-active"; _navDecoder.Tag = decoder ? "nav-active" : "nav";
        ThemeManager.Apply(_navSession); ThemeManager.Apply(_navDecoder);
    }

    private void WorkspaceThemeChanged(object? sender, EventArgs e)
    {
        RefreshWorkspaceSummary();
        SelectWorkspacePage(_decoderPage.Visible);
        ShowWorkspaceNotice(_operationNotice.Text);
    }

    private void RefreshWorkspaceSummary()
    {
        if (!_workspaceReady || IsDisposed) return;
        var palette = ThemeManager.Current;
        var input = textBoxCanLog.Text.Trim();
        bool hasSource = !string.IsNullOrWhiteSpace(input) && (File.Exists(input) || Directory.Exists(input));
        bool hasDecoder = _cachedDevices is { Count: > 0 };
        bool hasOutput = !string.IsNullOrWhiteSpace(textBoxOutput.Text);
        _sourceName.Text = hasSource ? Path.GetFileName(input.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) : "Добавьте CAN-лог";
        _sourceInfo.Text = hasSource ? Directory.Exists(input) ? "Пакетная обработка папки · CSV / TRC / ASC / TXT" : $"{Path.GetExtension(input).TrimStart('.').ToUpperInvariant()}  /  один файл" : "CSV · TRC · ASC · TXT CANfox / PCAN-View";
        _decoderName.Text = hasDecoder ? Path.GetFileName(textBoxDevices.Text) : "Описание посылок не выбрано";
        _decoderInfo.Text = hasDecoder ? labelFilterStatus.Text + (string.IsNullOrWhiteSpace(textBoxComposites.Text) ? "" : "\nУказан путь составных параметров") : "Подключите XLSX, DBC или DBF в разделе декодирования.";
        _exportFormat.Text = _saveOptions.OutputFormat switch
        {
            OutputFormat.Xlsx => "Excel / XLSX", OutputFormat.Csv => "Табличные данные / CSV", _ => "DST Connect / CSV"
        };
        _exportMode.Text = Directory.Exists(input) ? _saveOptions.BatchMode switch
        {
            BatchOutputMode.MergeToSingleFile => "Объединение логов в один файл",
            BatchOutputMode.SplitTrcByDate => "TRC: отдельные результаты по датам",
            _ => "Отдельный файл для каждого лога"
        } : "Один лог → один результат";
        SetReadiness(_readinessSource, hasSource, "Источник логов", palette.Success, palette.Muted);
        SetReadiness(_readinessDecoder, hasDecoder, "Описание посылок", palette.Success, palette.Muted);
        SetReadiness(_readinessOutput, hasOutput, "Путь результата", palette.Success, palette.Muted);
        _buttonTheme.Text = ThemeManager.Mode == ThemeMode.Dark ? "☀   Светлая тема" : "◐   Тёмная тема";
        if (!IsBusy)
        {
            _workspaceState.Text = buttonOpenOutput.Visible ? "●  Результат готов" : hasSource && hasDecoder && hasOutput ? "●  Готов к обработке" : "○  Настройка сеанса";
            _workspaceState.ForeColor = buttonOpenOutput.Visible ? palette.Success : palette.Muted;
        }
        labelFilterStatus.ForeColor = palette.Muted;
        var brand = Controls.Find("brandMark", true).FirstOrDefault();
        if (brand != null) { brand.BackColor = palette.Accent; brand.ForeColor = palette.AccentText; }
    }

    private static void SetReadiness(Label label, bool ready, string text, Color success, Color muted)
    {
        label.Text = (ready ? "●  " : "○  ") + text; label.ForeColor = ready ? success : muted;
    }

    private void ShowWorkspaceNotice(string message)
    {
        if (!_workspaceReady) return;
        _operationNotice.Text = message;
        _operationNotice.ForeColor = message.Contains("ошиб", StringComparison.OrdinalIgnoreCase) ? ThemeManager.Current.Danger : ThemeManager.Current.Muted;
    }

    private void UpdateWorkspaceBusy(bool busy, string stage)
    {
        foreach (Control control in new Control[]
        {
            textBoxCanLog, textBoxDevices, textBoxComposites, textBoxOutput,
            buttonCANlog, buttonViewLog, buttonDevices, buttonDevicesCreateOrAdd,
            buttonDevicesParams, buttonComposites, buttonCompositesCreateOrAdd,
            buttonOutput, buttonSaveOptions, buttonProcess, buttonOpenOutput, buttonTrcToAsc
        }) control.Enabled = !busy;
        _workspaceState.Text = busy ? "●  Выполняется операция" : "○  Настройка сеанса";
        _workspaceState.ForeColor = busy ? ThemeManager.Current.Text : ThemeManager.Current.Muted;
        ResizeExecutionFooter(busy);
        if (busy) _operationNotice.Text = stage; else RefreshWorkspaceSummary();
    }

    private void ResizeWorkspaceInspector()
    {
        if (_workspaceReady)
        {
            _workspaceColumns.ColumnStyles[1].Width = LogicalToDeviceUnits(_workspaceColumns.Width < LogicalToDeviceUnits(850) ? 276 : 306);
            ResizeExecutionFooter();
        }
    }

    private void ResizeExecutionFooter(bool? busy = null)
    {
        if (_executionFooterRow != null)
            _executionFooterRow.Height = LogicalToDeviceUnits((busy ?? IsBusy) ? 188 : buttonOpenOutput.Visible ? 144 : 96);
    }

    private void WorkspaceKeyDown(object? sender, KeyEventArgs e)
    {
        bool handled = true;
        if (e.KeyCode == Keys.F1) buttonHelp.PerformClick();
        else if (e.Control && e.KeyCode == Keys.D1) SelectWorkspacePage(false);
        else if (e.Control && e.KeyCode == Keys.D2) SelectWorkspacePage(true);
        else if (e.Control && e.KeyCode == Keys.L) textBoxLog.Focus();
        else if (e.KeyCode == Keys.Escape && IsBusy) buttonCancel.PerformClick();
        else if (e.Control && e.KeyCode == Keys.Enter && !IsBusy) buttonProcess.PerformClick();
        else if (e.Control && e.KeyCode == Keys.O && !IsBusy) { SelectWorkspacePage(false); buttonCANlog.PerformClick(); }
        else handled = false;
        if (handled) { e.Handled = true; e.SuppressKeyPress = true; }
    }

    private void SourceDragEnter(object? sender, DragEventArgs e)
    {
        if (!IsBusy && e.Data?.GetData(DataFormats.FileDrop) is string[] { Length: 1 }) e.Effect = DragDropEffects.Copy;
    }

    private void SourceDragDrop(object? sender, DragEventArgs e)
    {
        if (IsBusy || e.Data?.GetData(DataFormats.FileDrop) is not string[] { Length: 1 } paths) return;
        var path = paths[0];
        if (!Directory.Exists(path) && !new[] { ".csv", ".trc", ".asc", ".txt" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
        {
            Log("Ошибка: поддерживаются CSV, TRC, ASC, TXT CANfox или папка логов."); return;
        }
        textBoxCanLog.Text = path;
        textBoxOutput.Text = Directory.Exists(path) ? Path.Combine(path, "result") : Path.Combine(Path.GetDirectoryName(path) ?? "", Path.GetFileNameWithoutExtension(path) + "_result" + LogProcessingService.GetOutputExtension(_saveOptions.OutputFormat));
        SelectWorkspacePage(false);
    }

    private void ReleaseWorkspace()
    {
        ThemeManager.ThemeChanged -= WorkspaceThemeChanged;
        foreach (var font in _workspaceFonts) font.Dispose();
        _workspaceFonts.Clear();
    }
}
