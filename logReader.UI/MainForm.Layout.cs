namespace logReader.UI
{
    public partial class MainForm
    {
        private Panel _pagesHost = null!;
        private Panel[] _pages = [];
        private NavigationItem[] _navigation = [];
        private Label _pageTitle = null!, _pageDescription = null!, _readinessLabel = null!;
        private Label _devicesLibraryPath = null!, _compositesLibraryPath = null!, _outputSummary = null!;
        private InlineNotice _workspaceNotice = null!;
        private StatusBadge _workspaceStatus = null!;
        private ProgressBar _workspaceProgress = null!;
        private EmptyState _journalEmpty = null!;
        private TableLayoutPanel _workflowGrid = null!;
        private TableLayoutPanel _actionBar = null!, _actionStatus = null!;
        private FlowLayoutPanel _actionButtons = null!;
        private Control[] _workflowCards = [];
        private ModernButton _devicesShortcut = null!, _compositesShortcut = null!;
        private bool _compactWorkflow, _workspaceBusy, _runHadError, _hasOperationResult, _operationCancelled;
        private bool _workspaceLayoutReady, _reflowingWorkflow, _reflowingActionBar, _compactActionBar;

        private static ModernButton WorkspaceButton(string text, IconKind icon, ButtonVariant variant = ButtonVariant.Secondary)
            => new() { Text = text, Icon = icon, Variant = variant, AutoSize = true,
                MinimumSize = new Size(112, 36), Margin = new Padding(0, 0, 8, 0) };

        private static TableLayoutPanel WorkspaceStack(int padding = 0)
            => new() { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1, Padding = new Padding(padding), Margin = Padding.Empty };

        private static void StackAdd(TableLayoutPanel stack, Control child)
        {
            int row = stack.RowCount++;
            stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            child.Dock = DockStyle.Top;
            stack.Controls.Add(child, 0, row);
            if (child is Label label)
            {
                void Wrap()
                {
                    int available = stack.ClientSize.Width - stack.Padding.Horizontal - label.Margin.Horizontal;
                    if (available <= 0) return;
                    var maximum = new Size(available, 0);
                    if (label.MaximumSize != maximum) label.MaximumSize = maximum;
                }
                stack.SizeChanged += (_, _) => Wrap();
            }
        }

        private void BuildWorkspace()
        {
            var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 196));
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var sidebar = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, BackColor = AppTheme.Surface, Padding = new Padding(16, 24, 16, 16),
                ColumnCount = 1, RowCount = 5, Margin = Padding.Empty
            };
            sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
            sidebar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            sidebar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            sidebar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            sidebar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var brand = WorkspaceStack();
            StackAdd(brand, new Label { Text = "LOGER", AutoSize = true, Font = Typography.PageTitle, ForeColor = AppTheme.TextPrimary });
            StackAdd(brand, new Label { Text = "Анализ CAN-данных", AutoSize = true, Font = Typography.Caption,
                ForeColor = AppTheme.TextMuted, Margin = new Padding(0, 8, 0, 0) });
            sidebar.Controls.Add(brand, 0, 0);
            var navigation = WorkspaceStack();
            var labels = new[] { "Обработка", "Библиотеки", "Инструменты" };
            var icons = new[] { IconKind.Activity, IconKind.Devices, IconKind.Sliders };
            _navigation = new NavigationItem[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                int page = i;
                var item = new NavigationItem { Text = labels[i], Icon = icons[i], Height = 44,
                    Margin = new Padding(0, 0, 0, 8), Dock = DockStyle.Top, AccessibleName = labels[i] };
                item.Click += (_, _) => SelectWorkspacePage(page);
                _navigation[i] = item;
                StackAdd(navigation, item);
            }
            sidebar.Controls.Add(navigation, 0, 1);
            buttonHelp.Dock = DockStyle.Top;
            buttonHelp.Margin = new Padding(0, 0, 0, 12);
            sidebar.Controls.Add(buttonHelp, 0, 3);
            sidebar.Controls.Add(new Label { Text = "TRC · ASC · CSV · CANfox", AutoSize = true,
                Font = Typography.Caption, ForeColor = AppTheme.TextMuted }, 0, 4);
            shell.Controls.Add(sidebar, 0, 0);

            var workspace = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4,
                Padding = new Padding(24, 0, 24, 0), Margin = Padding.Empty };
            workspace.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            workspace.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            workspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            workspace.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var pageHeader = WorkspaceStack();
            pageHeader.Padding = new Padding(0, 24, 0, 20);
            _pageTitle = new Label { AutoSize = true, Font = Typography.PageTitle, ForeColor = AppTheme.TextPrimary, Margin = Padding.Empty };
            _pageDescription = new Label { AutoSize = true, Font = Typography.Secondary,
                ForeColor = AppTheme.TextSecondary, Margin = new Padding(0, 8, 0, 0) };
            StackAdd(pageHeader, _pageTitle);
            StackAdd(pageHeader, _pageDescription);
            workspace.Controls.Add(pageHeader, 0, 0);
            _workspaceNotice = new InlineNotice { Dock = DockStyle.Top, Visible = false, Margin = new Padding(0, 0, 0, 12) };
            workspace.Controls.Add(_workspaceNotice, 0, 1);
            contentSplit = new SplitContainer
            {
                Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, BackColor = AppTheme.Background,
                Panel1MinSize = 128, Panel2MinSize = 80, SplitterWidth = 8,
                Size = new Size(900, 610), SplitterDistance = 438, Margin = Padding.Empty, TabStop = false
            };
            _pagesHost = new Panel { Dock = DockStyle.Fill };
            _pages = [BuildProcessingPage(), BuildLibrariesPage(), BuildToolsPage()];
            foreach (var page in _pages)
            {
                page.Dock = DockStyle.Fill;
                page.Visible = false;
                _pagesHost.Controls.Add(page);
            }
            contentSplit.Panel1.Controls.Add(_pagesHost);
            contentSplit.Panel2.Controls.Add(BuildJournal());
            workspace.Controls.Add(contentSplit, 0, 2);
            workspace.Controls.Add(BuildActionBar(), 0, 3);
            shell.Controls.Add(workspace, 1, 0);
            Controls.Add(shell);
            SelectWorkspacePage(0);

            foreach (var input in new[] { textBoxCanLog, textBoxDevices, textBoxComposites, textBoxOutput })
                input.TextChanged += (_, _) =>
                {
                    if (input is ModernTextBox modern) { modern.HasError = false; modern.ErrorMessage = null; }
                    RefreshWorkspaceSummary(inputsChanged: true);
                };
            _workflowGrid.SizeChanged += (_, _) => ReflowWorkflow();
            _pagesHost.SizeChanged += (_, _) => RefreshWorkspaceSummary();
            textBoxLog.TextChanged += (_, _) => _journalEmpty.Visible = textBoxLog.TextLength == 0;
            Shown += (_, _) =>
            {
                // Initial WinForms autoscaling has completed; all metrics below are
                // now physical pixels, so no pre-scaled margins enter that pass.
                _workspaceLayoutReady = true;
                ReflowWorkflow();
                ReflowActionBar();
            };
            DpiChanged += (_, _) =>
            {
                if (!_workspaceLayoutReady || !IsHandleCreated || IsDisposed) return;
                BeginInvoke((Action)(() =>
                {
                    if (IsDisposed || Disposing) return;
                    ReflowWorkflow();
                    ReflowActionBar();
                }));
            };
            RefreshWorkspaceSummary();
        }

        private Panel BuildProcessingPage()
        {
            var page = new Panel { AutoScroll = true, BackColor = AppTheme.Background };
            _workflowGrid = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true,
                ColumnCount = 2, RowCount = 2, Margin = Padding.Empty };
            _workflowGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            _workflowGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            _workflowGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _workflowGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var source = BuildFileCard(labelCANlog, "01 · TRC, ASC, CSV, CANfox", textBoxCanLog, buttonCANlog, buttonViewLog);
            _devicesShortcut = WorkspaceButton("Создать файл", IconKind.Plus, ButtonVariant.Ghost);
            _devicesShortcut.Click += buttonDevicesCreateOrAdd_Click;
            var definitions = BuildFileCard(labelDevices, "02 · Посылки из XLSX, DBC или DBF", textBoxDevices, buttonDevices, buttonDevicesParams, _devicesShortcut);
            labelFilterStatus.Margin = new Padding(0, 12, 0, 0);
            StackAdd((TableLayoutPanel)definitions.Controls[0], labelFilterStatus);
            var result = BuildFileCard(labelResult, "03 · Файл для лога или каталог для папки", textBoxOutput, buttonOutput, buttonSaveOptions);
            _outputSummary = new Label { AutoSize = true, Font = Typography.Caption,
                ForeColor = AppTheme.TextSecondary, Margin = new Padding(0, 12, 0, 0) };
            StackAdd((TableLayoutPanel)result.Controls[0], _outputSummary);
            _compositesShortcut = WorkspaceButton("Создать файл", IconKind.Plus, ButtonVariant.Ghost);
            _compositesShortcut.Click += buttonCompositesCreateOrAdd_Click;
            var composites = BuildFileCard(labelComposites, "Необязательно · объединение сигналов", textBoxComposites, buttonComposites, _compositesShortcut);
            _workflowCards = [source, definitions, result, composites];
            _workflowGrid.Controls.Add(source, 0, 0);
            _workflowGrid.Controls.Add(definitions, 0, 1);
            _workflowGrid.Controls.Add(result, 1, 0);
            _workflowGrid.Controls.Add(composites, 1, 1);
            page.Controls.Add(_workflowGrid);
            return page;
        }

        private ModernCard BuildFileCard(Label title, string helper, TextBox input, Button browse, params Control[] actions)
        {
            var card = new ModernCard { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top, Margin = new Padding(0, 0, 12, 12) };
            var stack = WorkspaceStack();
            title.AutoSize = true;
            title.Font = Typography.CardTitle;
            title.ForeColor = AppTheme.TextPrimary;
            title.Margin = Padding.Empty;
            StackAdd(stack, title);
            StackAdd(stack, new Label { Text = helper, AutoSize = true, Font = Typography.Caption,
                ForeColor = AppTheme.TextSecondary, Margin = new Padding(0, 8, 0, 12) });
            var field = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            field.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            field.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            input.AccessibleName = title.Text;
            var inputHost = UiFactory.Field("", input);
            inputHost.Margin = new Padding(0, 0, 8, 0);
            inputHost.Dock = DockStyle.Fill;
            browse.Margin = Padding.Empty;
            browse.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            field.Controls.Add(inputHost, 0, 0);
            field.Controls.Add(browse, 1, 0);
            StackAdd(stack, field);
            if (actions.Length > 0)
            {
                var actionRow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top,
                    Margin = new Padding(0, 12, 0, 0), WrapContents = true };
                actionRow.Controls.AddRange(actions);
                StackAdd(stack, actionRow);
            }
            card.Controls.Add(stack);
            return card;
        }

        private Panel BuildLibrariesPage()
        {
            var page = new Panel { AutoScroll = true, BackColor = AppTheme.Background };
            var stack = WorkspaceStack();
            _devicesLibraryPath = new Label { AutoSize = true, Font = Typography.Secondary,
                ForeColor = AppTheme.TextSecondary, Margin = new Padding(0, 0, 0, 16) };
            _compositesLibraryPath = new Label { AutoSize = true, Font = Typography.Secondary,
                ForeColor = AppTheme.TextSecondary, Margin = new Padding(0, 0, 0, 16) };
            var selectDevices = WorkspaceButton("Выбрать файл", IconKind.File, ButtonVariant.Ghost);
            selectDevices.Click += buttonDevices_Click;
            var filters = WorkspaceButton("Устройства и параметры", IconKind.Filter);
            filters.Click += buttonDevicesParams_Click;
            StackAdd(stack, BuildToolCard("Файлы посылок", "Создавайте и редактируйте определения сообщений и сигналов.",
                _devicesLibraryPath, buttonDevicesCreateOrAdd, selectDevices, filters));
            var selectComposites = WorkspaceButton("Выбрать файл", IconKind.File, ButtonVariant.Ghost);
            selectComposites.Click += buttonComposites_Click;
            StackAdd(stack, BuildToolCard("Составные параметры", "Объединяйте данные нескольких посылок в один параметр.",
                _compositesLibraryPath, buttonCompositesCreateOrAdd, selectComposites));
            var back = WorkspaceButton("К обработке логов", IconKind.ArrowRight, ButtonVariant.Ghost);
            back.Click += (_, _) => SelectWorkspacePage(0);
            StackAdd(stack, back);
            page.Controls.Add(stack);
            return page;
        }

        private Panel BuildToolsPage()
        {
            var page = new Panel { AutoScroll = true, BackColor = AppTheme.Background };
            var stack = WorkspaceStack();
            var formats = new Label { Text = "TRC → ASC   /   CSV → ASC", AutoSize = true, Font = Typography.Mono,
                ForeColor = AppTheme.TextSecondary, Margin = new Padding(0, 0, 0, 16) };
            StackAdd(stack, BuildToolCard("Преобразование форматов", "Сохраните лог в формате ASC для дальнейшей работы с CAN-инструментами.", formats, buttonTrcToAsc));
            var settings = WorkspaceButton("Настроить сохранение", IconKind.Sliders);
            settings.Click += buttonSaveOptions_Click;
            StackAdd(stack, BuildToolCard("Формат результата и пакетная обработка", "XLSX, CSV, ДСТ Коннект; объединение логов, разбивка по датам и фильтр форматов папки.", null, settings));
            var help = WorkspaceButton("Открыть справку", IconKind.Help, ButtonVariant.Ghost);
            help.Click += buttonHelp_Click;
            StackAdd(stack, BuildToolCard("Начало работы", "Описание форматов, настройки сигналов и примеры работы с приложением.", null, help));
            page.Controls.Add(stack);
            return page;
        }

        private static ModernCard BuildToolCard(string title, string description, Control? details, params Control[] actions)
        {
            var card = new ModernCard { Dock = DockStyle.Top, AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(0, 0, 0, 16) };
            var stack = WorkspaceStack();
            StackAdd(stack, new Label { Text = title, AutoSize = true, Font = Typography.SectionTitle,
                ForeColor = AppTheme.TextPrimary, Margin = new Padding(0, 0, 0, 8) });
            StackAdd(stack, new Label { Text = description, AutoSize = true, Font = Typography.Secondary,
                ForeColor = AppTheme.TextSecondary, Margin = new Padding(0, 0, 0, 16) });
            if (details != null) StackAdd(stack, details);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Margin = Padding.Empty };
            buttons.Controls.AddRange(actions);
            StackAdd(stack, buttons);
            card.Controls.Add(stack);
            return card;
        }

        private Control BuildJournal()
        {
            var card = new ModernCard { Dock = DockStyle.Fill, Padding = new Padding(16, 8, 16, 12), Margin = Padding.Empty };
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var header = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Margin = new Padding(0, 0, 0, 8) };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header.Controls.Add(new Label { Text = "Журнал операции", AutoSize = true, Font = Typography.CardTitle, Anchor = AnchorStyles.Left }, 0, 0);
            var tools = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
            var copy = WorkspaceButton("Копировать", IconKind.Copy, ButtonVariant.Ghost);
            copy.MinimumSize = new Size(100, 32);
            copy.Click += (_, _) =>
            {
                if (textBoxLog.TextLength == 0) return;
                try { Clipboard.SetText(textBoxLog.Text); }
                catch (System.Runtime.InteropServices.ExternalException) { ShowWorkspaceNotice("Буфер обмена занят. Попробуйте ещё раз.", StatusTone.Warning); }
            };
            var clear = WorkspaceButton("Очистить", IconKind.Clear, ButtonVariant.Ghost);
            clear.MinimumSize = new Size(100, 32);
            clear.Click += (_, _) => textBoxLog.Clear();
            tools.Controls.AddRange([copy, clear]);
            header.Controls.Add(tools, 1, 0);
            layout.Controls.Add(header, 0, 0);
            var content = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.Surface };
            _journalEmpty = new EmptyState { Title = "Здесь появится ход обработки",
                Description = "Выберите входные файлы и запустите обработку.", Icon = IconKind.Activity, Dock = DockStyle.Fill };
            content.Controls.Add(textBoxLog);
            content.Controls.Add(_journalEmpty);
            _journalEmpty.BringToFront();
            layout.Controls.Add(content, 0, 1);
            card.Controls.Add(layout);
            return card;
        }

        private Control BuildActionBar()
        {
            _actionBar = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 1,
                Padding = new Padding(0, 16, 0, 16), Margin = Padding.Empty };
            _actionBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _actionBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _actionBar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _actionStatus = WorkspaceStack();
            _workspaceStatus = new StatusBadge { Text = "Выберите входные файлы", Tone = StatusTone.Neutral,
                Height = 28, Width = 200, Margin = Padding.Empty };
            _readinessLabel = new Label { AutoSize = true, Font = Typography.Caption,
                ForeColor = AppTheme.TextSecondary, Margin = new Padding(0, 8, 8, 0) };
            _workspaceProgress = progressBarProcess;
            StackAdd(_actionStatus, _workspaceStatus);
            StackAdd(_actionStatus, _readinessLabel);
            StackAdd(_actionStatus, labelProgress);
            StackAdd(_actionStatus, _workspaceProgress);
            _actionBar.Controls.Add(_actionStatus, 0, 0);
            _actionButtons = UiFactory.Footer(buttonProcess, buttonOpenOutput, buttonCancel);
            _actionButtons.Dock = DockStyle.Top;
            _actionButtons.Padding = Padding.Empty;
            _actionButtons.BackColor = AppTheme.Background;
            _actionButtons.WrapContents = true;
            _actionBar.Controls.Add(_actionButtons, 1, 0);
            _actionBar.SizeChanged += (_, _) => ReflowActionBar();
            foreach (var action in new[] { buttonProcess, buttonOpenOutput, buttonCancel })
            {
                action.VisibleChanged += (_, _) => ReflowActionBar();
                action.TextChanged += (_, _) => ReflowActionBar();
            }
            return _actionBar;
        }

        private void SelectWorkspacePage(int index)
        {
            for (int i = 0; i < _pages.Length; i++)
            {
                _pages[i].Visible = i == index;
                _navigation[i].Selected = i == index;
            }
            _pageTitle.Text = index switch { 1 => "Библиотеки посылок", 2 => "Инструменты", _ => "Обработка логов" };
            _pageDescription.Text = index switch
            {
                1 => "Определения устройств, сигналов и составных параметров.",
                2 => "Преобразование форматов и настройки результата.",
                _ => "От входного лога до декодированных данных — в одной рабочей области."
            };
            ReflowWorkflow();
        }

        private void ReflowWorkflow()
        {
            if (!_workspaceLayoutReady || _reflowingWorkflow || _workflowGrid == null ||
                _workflowCards.Length == 0 || _workflowGrid.Width <= 0) return;
            bool compact = _workflowGrid.Width < UiScale.Px(this, 760);
            bool changeMode = compact != _compactWorkflow || _workflowGrid.RowCount != (compact ? 4 : 2);
            int gap = UiScale.Px(this, 12);
            bool changeMargins = _workflowCards.Where((card, index) =>
                card.Margin != new Padding(0, 0, compact || index >= 2 ? 0 : gap, gap)).Any();
            if (!changeMode && !changeMargins) return;
            _reflowingWorkflow = true;
            _workflowGrid.SuspendLayout();
            try
            {
                _compactWorkflow = compact;
                if (changeMode)
                {
                    _workflowGrid.ColumnStyles.Clear();
                    _workflowGrid.RowStyles.Clear();
                    _workflowGrid.ColumnCount = compact ? 1 : 2;
                    _workflowGrid.RowCount = compact ? 4 : 2;
                    for (int c = 0; c < _workflowGrid.ColumnCount; c++) _workflowGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / _workflowGrid.ColumnCount));
                    for (int r = 0; r < _workflowGrid.RowCount; r++) _workflowGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                }
                for (int i = 0; i < _workflowCards.Length; i++)
                {
                    if (changeMode)
                        _workflowGrid.SetCellPosition(_workflowCards[i], compact ? new TableLayoutPanelCellPosition(0, i) : new TableLayoutPanelCellPosition(i / 2, i % 2));
                    _workflowCards[i].Margin = new Padding(0, 0, compact || i >= 2 ? 0 : gap, gap);
                }
            }
            finally
            {
                _workflowGrid.ResumeLayout(true);
                _reflowingWorkflow = false;
            }
        }

        private void ReflowActionBar()
        {
            if (!_workspaceLayoutReady || _reflowingActionBar || _actionBar == null || _actionBar.ClientSize.Width <= 0) return;
            int available = Math.Max(1, _actionBar.ClientSize.Width - _actionBar.Padding.Horizontal);
            int naturalActionsWidth = _actionButtons.Controls.Cast<Control>().Where(action => action.Visible)
                .Sum(action => action.GetPreferredSize(Size.Empty).Width + action.Margin.Horizontal);
            int gap = UiScale.Px(this, 16);
            bool compact = available < UiScale.Px(this, 620) || available < naturalActionsWidth + UiScale.Px(this, 240) + gap;
            int actionsWidth = compact ? available : Math.Min(available, naturalActionsWidth);
            int statusWidth = compact ? available : Math.Max(1, available - actionsWidth - gap);
            bool changeMode = compact != _compactActionBar || _actionBar.RowCount != (compact ? 2 : 1);
            _reflowingActionBar = true;
            _actionBar.SuspendLayout();
            try
            {
                _compactActionBar = compact;
                if (changeMode)
                {
                    _actionBar.ColumnStyles.Clear();
                    _actionBar.RowStyles.Clear();
                    _actionBar.ColumnCount = compact ? 1 : 2;
                    _actionBar.RowCount = compact ? 2 : 1;
                    _actionBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                    if (!compact) _actionBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, actionsWidth + gap));
                    for (int row = 0; row < _actionBar.RowCount; row++) _actionBar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                    _actionBar.SetCellPosition(_actionStatus, new TableLayoutPanelCellPosition(0, 0));
                    _actionBar.SetCellPosition(_actionButtons, compact ? new TableLayoutPanelCellPosition(0, 1) : new TableLayoutPanelCellPosition(1, 0));
                }
                else if (!compact)
                {
                    // The action text/visibility can change while the layout stays wide.
                    _actionBar.ColumnStyles[1].SizeType = SizeType.Absolute;
                    _actionBar.ColumnStyles[1].Width = actionsWidth + gap;
                }
                _actionButtons.Margin = compact ? new Padding(0, UiScale.Px(this, 12), 0, 0) : new Padding(gap, 0, 0, 0);
                var actionsMaximum = new Size(Math.Max(1, actionsWidth), 0);
                if (_actionButtons.MaximumSize != actionsMaximum) _actionButtons.MaximumSize = actionsMaximum;
                var statusMaximum = new Size(statusWidth, 0);
                if (_actionStatus.MaximumSize != statusMaximum) _actionStatus.MaximumSize = statusMaximum;
                int labelWidth = Math.Max(1, statusWidth - _readinessLabel.Margin.Horizontal);
                var labelMaximum = new Size(labelWidth, 0);
                if (_readinessLabel.MaximumSize != labelMaximum) _readinessLabel.MaximumSize = labelMaximum;
                if (labelProgress.MaximumSize != labelMaximum) labelProgress.MaximumSize = labelMaximum;
                if (_workspaceStatus.MaximumSize != statusMaximum) _workspaceStatus.MaximumSize = statusMaximum;
            }
            finally
            {
                _actionBar.ResumeLayout(true);
                _reflowingActionBar = false;
            }
        }

        private void RefreshWorkspaceSummary(bool inputsChanged = false)
        {
            if (_readinessLabel == null) return;
            if (inputsChanged && !_workspaceBusy)
            {
                _hasOperationResult = false;
                _workspaceNotice.Visible = false;
            }
            _devicesLibraryPath.Text = string.IsNullOrWhiteSpace(textBoxDevices.Text)
                ? "Файл пока не выбран. Создайте новый или откройте существующий." : textBoxDevices.Text;
            _compositesLibraryPath.Text = string.IsNullOrWhiteSpace(textBoxComposites.Text)
                ? "Файл пока не выбран. Составные параметры необязательны." : textBoxComposites.Text;
            bool hasDevices = IsDevicesFileSelectedAndExists();
            bool hasComposites = IsCompositesFileSelectedAndExists();
            _devicesShortcut.Text = hasDevices ? "Редактор" : "Создать файл";
            _devicesShortcut.Icon = hasDevices ? IconKind.Edit : IconKind.Plus;
            _compositesShortcut.Text = hasComposites ? "Редактор" : "Создать файл";
            _compositesShortcut.Icon = hasComposites ? IconKind.Edit : IconKind.Plus;
            string format = _saveOptions.OutputFormat switch { OutputFormat.Xlsx => "Excel · XLSX", OutputFormat.CsvDstConnect => "CSV · ДСТ Коннект", _ => "CSV" };
            string batch = _saveOptions.BatchMode switch { BatchOutputMode.MergeToSingleFile => "единый файл", BatchOutputMode.SplitTrcByDate => "разбивка по датам", _ => "отдельный файл на каждый лог" };
            _outputSummary.Text = $"{format} · {batch}";
            if (!_workspaceBusy && !_hasOperationResult)
            {
                bool ready = (File.Exists(textBoxCanLog.Text.Trim()) || Directory.Exists(textBoxCanLog.Text.Trim()))
                    && IsDevicesFileSelectedAndExists() && !string.IsNullOrWhiteSpace(textBoxOutput.Text);
                _workspaceStatus.Text = ready ? "Готово к обработке" : "Выберите входные файлы";
                _workspaceStatus.Tone = ready ? StatusTone.Info : StatusTone.Neutral;
                _readinessLabel.Text = string.IsNullOrWhiteSpace(textBoxCanLog.Text) ? "Шаг 1: выберите файл или папку с логами."
                    : !File.Exists(textBoxCanLog.Text.Trim()) && !Directory.Exists(textBoxCanLog.Text.Trim()) ? "Источник не найден. Проверьте путь к логу."
                    : !IsDevicesFileSelectedAndExists() ? "Шаг 2: выберите файл посылок для декодирования."
                    : string.IsNullOrWhiteSpace(textBoxOutput.Text) ? "Шаг 3: укажите путь к результату."
                    : "Входные файлы выбраны. Можно запускать обработку.";
            }
            buttonProcess.Enabled = !_workspaceBusy;
            int width = Math.Max(UiScale.Px(this, 180), _pagesHost.Width - UiScale.Px(this, 56));
            foreach (var label in new[] { _devicesLibraryPath, _compositesLibraryPath, _pageDescription })
                label.MaximumSize = new Size(width, 0);
            _readinessLabel.MaximumSize = new Size(Math.Max(UiScale.Px(this, 140), width - UiScale.Px(this, 380)), 0);
            labelProgress.MaximumSize = _readinessLabel.MaximumSize;
            ReflowActionBar();
        }

        private void ShowWorkspaceNotice(string text, StatusTone tone)
        {
            // Full details stay selectable in the journal; a notice must not consume the workspace.
            string summary = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? text;
            if (summary.Length > 160) summary = summary[..157] + "…";
            _workspaceNotice.Text = summary;
            _workspaceNotice.AccessibleDescription = text;
            _workspaceNotice.Tone = tone;
            _workspaceNotice.Visible = true;
        }

        private void ReportFieldError(TextBox field, string message)
        {
            if (field is ModernTextBox modern) { modern.HasError = true; modern.ErrorMessage = message; }
            field.Focus();
            Log(message);
        }

        private void SetWorkspaceBusy(bool busy, bool scan = false)
        {
            _workspaceBusy = busy;
            _pagesHost.Enabled = !busy;
            foreach (var navigation in _navigation) navigation.Enabled = !busy;
            buttonProcess.Enabled = !busy;
            buttonOpenOutput.Enabled = !busy;
            _workspaceProgress.Visible = busy;
            buttonProcess.Text = busy ? scan ? "Поиск посылок…" : "Обработка…" : "Обработать логи";
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
            if (busy)
            {
                _hasOperationResult = false;
                _workspaceStatus.Text = scan ? "Анализ устройств" : "Идёт обработка";
                _workspaceStatus.Tone = StatusTone.Info;
                _readinessLabel.Text = "Можно следить за ходом операции в журнале.";
            }
            else if (_operationCancelled)
            {
                _hasOperationResult = true;
                _workspaceStatus.Text = "Операция отменена";
                _workspaceStatus.Tone = StatusTone.Neutral;
                _readinessLabel.Text = "Можно изменить параметры и запустить операцию снова.";
            }
            else if (scan)
            {
                _workspaceStatus.Text = _runHadError ? "Не удалось выполнить поиск" : "Готово к работе";
                _workspaceStatus.Tone = _runHadError ? StatusTone.Error : StatusTone.Neutral;
                RefreshWorkspaceSummary();
            }
            else
            {
                _hasOperationResult = true;
                bool success = !_runHadError && buttonOpenOutput.Visible;
                _workspaceStatus.Text = _runHadError ? "Завершено с ошибкой" : success ? "Обработка завершена" : "Результаты не созданы";
                _workspaceStatus.Tone = _runHadError ? StatusTone.Error : success ? StatusTone.Success : StatusTone.Warning;
                _readinessLabel.Text = success ? "Результат доступен по кнопке «Открыть результат»." : "Подробности операции находятся в журнале.";
            }
        }

        private void PresentLogState(string message)
        {
            bool error = message.StartsWith("Ошибка", StringComparison.OrdinalIgnoreCase)
                || message.StartsWith("Критическая ошибка", StringComparison.OrdinalIgnoreCase)
                || message.StartsWith("Обработка завершилась с ошибкой", StringComparison.OrdinalIgnoreCase)
                || message.StartsWith("Пропуск: ошибка", StringComparison.OrdinalIgnoreCase)
                || message.StartsWith("Не удалось", StringComparison.OrdinalIgnoreCase);
            if (error)
            {
                _runHadError = true;
                _hasOperationResult = true;
                ShowWorkspaceNotice(message, StatusTone.Error);
                _workspaceStatus.Text = "Требуется внимание";
                _workspaceStatus.Tone = StatusTone.Error;
                _readinessLabel.Text = "Проверьте входные данные. Подробности находятся в журнале.";
            }
            else if (!_workspaceBusy && (message.Contains("успешно", StringComparison.OrdinalIgnoreCase)
                || message.Contains("обновлён", StringComparison.OrdinalIgnoreCase)))
                ShowWorkspaceNotice(message, StatusTone.Success);
        }
    }
}
