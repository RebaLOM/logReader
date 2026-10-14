namespace logReader.UI
{
    internal sealed class SaveOptionsForm : Form
    {
        private readonly ComboBox _comboOutputFormat;
        private readonly ComboBox _comboBatchMode;
        private readonly CheckedListBox _formatsList;
        private readonly Label _formatsHint;
        private readonly Panel _dstPanel;
        private readonly NumericUpDown _numBlockPeriod;
        private readonly NumericUpDown _numBlockStart;
        private readonly CheckBox _chkIncludeDeviceIdRow;
        private readonly CheckBox _chkIncludeDtc;
        private readonly NumericUpDown _numDtcTimeout;
        private bool _suppressFormatsEvents;

        internal OutputFormat SelectedOutputFormat { get; private set; }
        internal BatchOutputMode SelectedBatchMode { get; private set; }
        internal LogFormatKind SelectedFolderFormats { get; private set; } = LogFormatKind.All;
        internal DstConnectOptions SelectedDstConnectOptions { get; private set; } = new();
        internal bool IncludeDeviceIdHeaderRow { get; private set; }
        internal bool IncludeDtc { get; private set; }
        internal double DtcTimeoutMs { get; private set; } = 2000;

        internal SaveOptionsForm(
            OutputFormat currentFormat,
            BatchOutputMode currentBatchMode,
            DstConnectOptions currentDstOptions,
            string? logFolderPath,
            LogFormatKind currentFolderFormats,
            bool includeDeviceIdHeaderRow = false,
            bool includeDtc = false,
            double dtcTimeoutMs = 2000)
        {
            SelectedDstConnectOptions = CloneDstOptions(currentDstOptions);
            IncludeDeviceIdHeaderRow = includeDeviceIdHeaderRow;
            IncludeDtc = includeDtc;
            DtcTimeoutMs = double.IsFinite(dtcTimeoutMs) && dtcTimeoutMs > 0 ? dtcTimeoutMs : 2000;
            Text = "Параметры сохранения";
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = true;
            ShowInTaskbar = false;
            ClientSize = new Size(1000, 940);
            MinimumSize = new Size(780, 560);

            _comboOutputFormat = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Top };
            _comboOutputFormat.Items.AddRange(new object[] { "XLSX", "CSV", "CSV ДСТ Коннект" });
            _comboOutputFormat.SelectedIndexChanged += (_, _) => UpdateDstPanelVisibility();
            _chkIncludeDeviceIdRow = new CheckBox
            {
                Text = "Строка CAN ID над именами параметров", AutoSize = true,
                Dock = DockStyle.Top, Checked = includeDeviceIdHeaderRow, Padding = new Padding(0, 14, 0, 0)
            };
            var idHint = new Label
            {
                Text = "Выключено по умолчанию. «Шаг» и «Время» остаются в одной строке с именами параметров.",
                Dock = DockStyle.Top, Height = 48, Tag = "muted", Padding = new Padding(0, 8, 0, 0)
            };
            var formatContent = new Panel { Dock = DockStyle.Fill };
            formatContent.Controls.Add(idHint);
            formatContent.Controls.Add(_chkIncludeDeviceIdRow);
            formatContent.Controls.Add(_comboOutputFormat);

            _comboBatchMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Top };
            _comboBatchMode.Items.AddRange(new object[]
            {
                "Отдельный файл на каждый входной лог",
                "В единый файл (.trc / CSV)",
                "Разбить .trc на файлы по датам из содержимого"
            });
            var batchHint = new Label
            {
                Text = "При выборе папки этот режим определяет структуру результатов. Форматы входа выбираются ниже.",
                Dock = DockStyle.Top, Height = 62, Tag = "muted", Padding = new Padding(0, 14, 0, 0)
            };
            var batchContent = new Panel { Dock = DockStyle.Fill };
            batchContent.Controls.Add(batchHint);
            batchContent.Controls.Add(_comboBatchMode);
            var topCards = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Tag = "background" };
            topCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            topCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            topCards.Controls.Add(DialogLayout.Section("Формат результата", formatContent), 0, 0);
            topCards.Controls.Add(DialogLayout.Section("Пакетная обработка", batchContent), 1, 0);

            _numBlockPeriod = new NumericUpDown
            {
                Minimum = 1, Maximum = 3600000, Value = Math.Clamp(currentDstOptions.BlockPeriodMs, 1, 3600000),
                Width = 100, Margin = new Padding(8, 3, 18, 3)
            };
            _numBlockStart = new NumericUpDown
            {
                Minimum = 0, Maximum = 10000000, Value = Math.Max(0, currentDstOptions.BlockStartIndex),
                Width = 100, Margin = new Padding(8, 3, 18, 3)
            };
            var dstContent = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2 };
            dstContent.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
            dstContent.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            dstContent.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            dstContent.Controls.Add(MessageEditFormHelpers.MakeLabel("Период блока, мс"), 0, 0);
            dstContent.Controls.Add(_numBlockPeriod, 1, 0);
            dstContent.Controls.Add(new Label { Text = "Цикл шины; одна строка CSV на блок", Dock = DockStyle.Fill, Tag = "muted", TextAlign = ContentAlignment.MiddleLeft }, 2, 0);
            dstContent.Controls.Add(MessageEditFormHelpers.MakeLabel("Якорная посылка"), 0, 1);
            dstContent.Controls.Add(_numBlockStart, 1, 1);
            dstContent.Controls.Add(new Label { Text = "Message Number в TRC; 0 — автоматически", Dock = DockStyle.Fill, Tag = "muted", TextAlign = ContentAlignment.MiddleLeft }, 2, 1);
            _dstPanel = DialogLayout.Section("CSV ДСТ Коннект / границы блоков", dstContent);
            _dstPanel.Height = 126;
            _dstPanel.Dock = DockStyle.Top;
            _dstPanel.Margin = new Padding(0, 0, 12, 14);

            _chkIncludeDtc = new CheckBox
            {
                Name = "includeDtc", AccessibleName = "Добавлять DTC в результат обработки",
                Text = "Добавлять DTC в результат обработки", AutoSize = true,
                Dock = DockStyle.Top, Checked = includeDtc
            };
            _numDtcTimeout = new NumericUpDown
            {
                Name = "dtcTimeoutSeconds", AccessibleName = "Допустимый разрыв данных DTC, секунд",
                Minimum = 0.001M, Maximum = 3600, DecimalPlaces = 3, Increment = 0.1M,
                Value = (decimal)Math.Clamp(DtcTimeoutMs / 1000, 0.001, 3600),
                Width = 100, Enabled = includeDtc, Margin = new Padding(8, 3, 0, 3)
            };
            _chkIncludeDtc.CheckedChanged += (_, _) => _numDtcTimeout.Enabled = _chkIncludeDtc.Checked;
            var dtcTimeoutLine = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, AutoSize = true, WrapContents = true,
                Padding = new Padding(0, 10, 0, 0)
            };
            dtcTimeoutLine.Controls.Add(new Label
            {
                Text = "Допустимый разрыв данных, с:", AutoSize = true,
                Margin = new Padding(0, 6, 0, 3)
            });
            dtcTimeoutLine.Controls.Add(_numDtcTimeout);
            var dtcHint = new Label
            {
                Text = "DTC1–DTC6: SPN / FMI. Пусто — нет подтверждённого значения.",
                Dock = DockStyle.Top, Height = 40, Tag = "muted", Padding = new Padding(0, 8, 0, 0)
            };
            var dtcContent = new Panel { Dock = DockStyle.Fill };
            dtcContent.Controls.Add(dtcHint);
            dtcContent.Controls.Add(dtcTimeoutLine);
            dtcContent.Controls.Add(_chkIncludeDtc);
            var dtcSection = DialogLayout.Section("Диагностика DTC в результате", dtcContent);
            dtcSection.Height = 196;

            _formatsList = new ThemedCheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true, IntegralHeight = false };
            _formatsList.ItemCheck += formatsList_ItemCheck;
            _formatsHint = new Label { Dock = DockStyle.Bottom, Height = 24, Tag = "muted", TextAlign = ContentAlignment.MiddleLeft };
            var formatsContent = new Panel { Dock = DockStyle.Fill };
            formatsContent.Controls.Add(_formatsList);
            formatsContent.Controls.Add(_formatsHint);
            var body = new TableLayoutPanel
            {
                Name = "saveOptionsBody", Anchor = AnchorStyles.Top | AnchorStyles.Left,
                ColumnCount = 1, RowCount = 4, Margin = Padding.Empty,
                Padding = new Padding(24, 0, 12, 0), Tag = "background"
            };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 194));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 140));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 196));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 220));
            body.Controls.Add(topCards, 0, 0);
            body.Controls.Add(_dstPanel, 0, 1);
            body.Controls.Add(dtcSection, 0, 2);
            body.Controls.Add(DialogLayout.Section("Форматы в папке", formatsContent, compact: true), 0, 3);

            var buttonOk = new Button { Text = "Применить параметры", DialogResult = DialogResult.OK, Tag = "primary", AutoSize = true, MinimumSize = new Size(170, 34) };
            buttonOk.Click += buttonOk_Click;
            _formatsList.ItemCheck += (_, _) =>
            {
                if (IsHandleCreated) BeginInvoke(() => UpdateOkEnabledState(buttonOk));
            };
            var buttonCancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(100, 34) };
            var footer = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom, Height = 68, FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(24, 16, 24, 16), WrapContents = false, Tag = "background"
            };
            footer.Controls.Add(buttonOk);
            footer.Controls.Add(buttonCancel);
            var scroll = new Panel { Name = "saveOptionsScroll", Dock = DockStyle.Fill, AutoScroll = true, Tag = "background" };
            scroll.Controls.Add(body);
            bool layingOutBody = false;
            scroll.Layout += (_, _) =>
            {
                if (layingOutBody) return;
                layingOutBody = true;
                try
                {
                    body.RowStyles[1].Height = _comboOutputFormat.SelectedIndex == 2
                        ? LogicalToDeviceUnits(140) : 0;
                    if (_comboOutputFormat.SelectedIndex == 2) _dstPanel.Height = LogicalToDeviceUnits(126);
                    int height = body.Padding.Vertical + (int)Math.Ceiling(body.RowStyles.Cast<RowStyle>().Sum(row => row.Height));
                    // Let the scroll panel move this fixed-size child. AutoSize
                    // constraints and a right anchor can cancel native scrolling
                    // when nested controls receive focus or ScrollControlIntoView.
                    int width = scroll.Width - (height > scroll.Height ? SystemInformation.VerticalScrollBarWidth : 0);
                    body.Size = new Size(Math.Max(1, width), height);
                    scroll.AutoScrollMinSize = new Size(0, height);
                }
                finally { layingOutBody = false; }
            };
            _comboOutputFormat.SelectedIndexChanged += (_, _) => scroll.PerformLayout();
            Controls.Add(scroll);
            Controls.Add(DialogLayout.Header(this, "Экспорт / Настройки", "Параметры результата"));
            Controls.Add(footer);
            AcceptButton = buttonOk;
            CancelButton = buttonCancel;
            _comboOutputFormat.SelectedIndex = currentFormat switch
            {
                OutputFormat.Csv => 1,
                OutputFormat.CsvDstConnect => 2,
                _ => 0
            };
            _comboBatchMode.SelectedIndex = currentBatchMode switch
            {
                BatchOutputMode.MergeToSingleFile => 1,
                BatchOutputMode.SplitTrcByDate => 2,
                _ => 0
            };

            SelectedFolderFormats = currentFolderFormats == LogFormatKind.None ? LogFormatKind.All : currentFolderFormats;
            BuildFormatsList(logFolderPath);
            UpdateDstPanelVisibility();
            UpdateOkEnabledState(buttonOk);
            UiScaling.Apply(this);
            ThemeManager.Attach(this);
        }

        private void UpdateDstPanelVisibility()
        {
            _dstPanel.Visible = _comboOutputFormat.SelectedIndex == 2;
        }

        private static DstConnectOptions CloneDstOptions(DstConnectOptions src)
            => new()
            {
                BlockPeriodMs = src.BlockPeriodMs,
                BlockStartIndex = src.BlockStartIndex,
                JitterToleranceMs = src.JitterToleranceMs
            };

        private sealed record FormatItem(LogFormatKind Kind, string Label, bool IsAll = false)
        {
            public override string ToString() => Label;
        }

        private void BuildFormatsList(string? logFolderPath)
        {
            _suppressFormatsEvents = true;
            _formatsList.Items.Clear();

            string? folder = string.IsNullOrWhiteSpace(logFolderPath) ? null : logFolderPath.Trim();
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                _formatsList.Enabled = false;
                _formatsHint.Text = "Доступно при выборе папки с логами.";
                _suppressFormatsEvents = false;
                return;
            }

            var inv = LogFolderScanner.Scan(folder);
            int total = inv.Counts.Values.Sum();

            _formatsList.Enabled = true;
            _formatsHint.Text = $"Найдено файлов: {total}.";

            var items = new List<FormatItem>();
            items.Add(new FormatItem(LogFormatKind.All, $"Все форматы ({total})", IsAll: true));
            AddIfPresent(inv, items, LogFormatKind.Trc, ".trc");
            AddIfPresent(inv, items, LogFormatKind.Asc, ".asc");
            AddIfPresent(inv, items, LogFormatKind.MatrixCsv, LogFormatUiNames.Csv);
            AddIfPresent(inv, items, LogFormatKind.StepCsv, LogFormatUiNames.LegacyCsv);
            AddIfPresent(inv, items, LogFormatKind.CanfoxTxt, "CANfox .txt");

            foreach (var item in items)
                _formatsList.Items.Add(item, false);

            ApplyInitialChecks(inv, SelectedFolderFormats);

            _suppressFormatsEvents = false;
        }

        private static void AddIfPresent(LogFolderInventory inv, List<FormatItem> items, LogFormatKind kind, string label)
        {
            if (!inv.Counts.TryGetValue(kind, out int n) || n <= 0) return;
            items.Add(new FormatItem(kind, $"{label} ({n})"));
        }

        private void ApplyInitialChecks(LogFolderInventory inv, LogFormatKind selection)
        {
            bool anyChecked = false;
            for (int i = 0; i < _formatsList.Items.Count; i++)
            {
                var item = (FormatItem)_formatsList.Items[i]!;
                if (item.IsAll) continue;

                bool present = inv.Counts.ContainsKey(item.Kind);
                bool should = present && (selection & item.Kind) != 0;
                if (should)
                {
                    _formatsList.SetItemChecked(i, true);
                    anyChecked = true;
                }
            }

            if (!anyChecked || selection == LogFormatKind.All)
            {
                for (int i = 0; i < _formatsList.Items.Count; i++)
                {
                    var item = (FormatItem)_formatsList.Items[i]!;
                    if (item.IsAll) continue;
                    _formatsList.SetItemChecked(i, true);
                }
            }

            SyncAllCheckBoxState();
        }

        private void SyncAllCheckBoxState()
        {
            int allIndex = FindAllIndex();
            if (allIndex < 0) return;

            bool allChecked = true;
            for (int i = 0; i < _formatsList.Items.Count; i++)
            {
                if (i == allIndex) continue;
                if (!_formatsList.GetItemChecked(i))
                {
                    allChecked = false;
                    break;
                }
            }
            _formatsList.SetItemChecked(allIndex, allChecked);
        }

        private int FindAllIndex()
        {
            for (int i = 0; i < _formatsList.Items.Count; i++)
            {
                if (_formatsList.Items[i] is FormatItem { IsAll: true }) return i;
            }
            return -1;
        }

        private LogFormatKind GetFormatsSelectionFromList()
        {
            LogFormatKind kind = LogFormatKind.None;
            for (int i = 0; i < _formatsList.Items.Count; i++)
            {
                if (!_formatsList.GetItemChecked(i)) continue;
                var item = (FormatItem)_formatsList.Items[i]!;
                if (item.IsAll) continue;
                kind |= item.Kind;
            }
            return kind == LogFormatKind.None ? LogFormatKind.None : kind;
        }

        private void formatsList_ItemCheck(object? sender, ItemCheckEventArgs e)
        {
            if (_suppressFormatsEvents) return;

            var clicked = (FormatItem)_formatsList.Items[e.Index]!;
            if (clicked.IsAll)
            {
                _suppressFormatsEvents = true;
                bool newState = e.NewValue == CheckState.Checked;
                for (int i = 0; i < _formatsList.Items.Count; i++)
                {
                    if (_formatsList.Items[i] is not FormatItem item) continue;
                    if (item.IsAll) continue;
                    _formatsList.SetItemChecked(i, newState);
                }
                _suppressFormatsEvents = false;
                return;
            }

            BeginInvoke(() =>
            {
                if (_suppressFormatsEvents) return;
                _suppressFormatsEvents = true;
                SyncAllCheckBoxState();
                _suppressFormatsEvents = false;
            });
        }

        private void UpdateOkEnabledState(Button okButton)
        {
            if (!_formatsList.Enabled)
            {
                okButton.Enabled = true;
                return;
            }

            bool any = false;
            for (int i = 0; i < _formatsList.Items.Count; i++)
            {
                if (_formatsList.Items[i] is FormatItem { IsAll: true }) continue;
                if (_formatsList.GetItemChecked(i))
                {
                    any = true;
                    break;
                }
            }
            okButton.Enabled = any;
        }

        private void buttonOk_Click(object? sender, EventArgs e)
        {
            SelectedOutputFormat = _comboOutputFormat.SelectedIndex switch
            {
                1 => OutputFormat.Csv,
                2 => OutputFormat.CsvDstConnect,
                _ => OutputFormat.Xlsx
            };
            SelectedBatchMode = _comboBatchMode.SelectedIndex switch
            {
                1 => BatchOutputMode.MergeToSingleFile,
                2 => BatchOutputMode.SplitTrcByDate,
                _ => BatchOutputMode.PerInputFile
            };

            SelectedDstConnectOptions = new DstConnectOptions
            {
                BlockPeriodMs = (int)_numBlockPeriod.Value,
                BlockStartIndex = (int)_numBlockStart.Value
            };

            IncludeDeviceIdHeaderRow = _chkIncludeDeviceIdRow.Checked;
            IncludeDtc = _chkIncludeDtc.Checked;
            DtcTimeoutMs = (double)_numDtcTimeout.Value * 1000;
            SelectedFolderFormats = _formatsList.Enabled ? GetFormatsSelectionFromList() : LogFormatKind.All;
        }
    }
}
