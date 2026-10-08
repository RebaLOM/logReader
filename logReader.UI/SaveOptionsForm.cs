namespace logReader.UI
{
    internal sealed class SaveOptionsForm : Form
    {
        private readonly ComboBox _comboOutputFormat;
        private readonly ComboBox _comboBatchMode;
        private readonly CheckedListBox _formatsList;
        private readonly EmptyState _formatsEmptyState;
        private readonly Label _formatsHint;
        private readonly Panel _dstPanel;
        private readonly NumericUpDown _numBlockPeriod;
        private readonly NumericUpDown _numBlockStart;
        private readonly CheckBox _chkIncludeDeviceIdRow;
        private readonly Button _okButton;
        private bool _suppressFormatsEvents;
        private bool _formatsRefreshPending;

        internal OutputFormat SelectedOutputFormat { get; private set; }
        internal BatchOutputMode SelectedBatchMode { get; private set; }
        internal LogFormatKind SelectedFolderFormats { get; private set; } = LogFormatKind.All;
        internal DstConnectOptions SelectedDstConnectOptions { get; private set; } = new();
        internal bool IncludeDeviceIdHeaderRow { get; private set; }

        internal SaveOptionsForm(
            OutputFormat currentFormat,
            BatchOutputMode currentBatchMode,
            DstConnectOptions currentDstOptions,
            string? logFolderPath,
            LogFormatKind currentFolderFormats,
            bool includeDeviceIdHeaderRow = false)
        {
            SuspendLayout();
            SelectedDstConnectOptions = CloneDstOptions(currentDstOptions);
            IncludeDeviceIdHeaderRow = includeDeviceIdHeaderRow;
            Text = "Параметры сохранения";
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(744, 800);
            MinimumSize = new Size(620, 560);

            _comboOutputFormat = new ModernComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Fill
            };
            _comboOutputFormat.Items.Add("XLSX");
            _comboOutputFormat.Items.Add("CSV");
            _comboOutputFormat.Items.Add("CSV ДСТ Коннект");
            _comboOutputFormat.SelectedIndexChanged += (_, _) => UpdateDstPanelVisibility();

            var dstFields = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0, 12, 0, 0),
                Padding = new Padding(0)
            };
            dstFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            dstFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            _dstPanel = dstFields;

            _numBlockPeriod = new ModernNumericUpDown
            {
                Minimum = 1,
                Maximum = 3600000,
                Value = Math.Clamp(currentDstOptions.BlockPeriodMs, 1, 3600000),
                Dock = DockStyle.Fill,
                ThousandsSeparator = true
            };
            _numBlockStart = new ModernNumericUpDown
            {
                Minimum = 0,
                Maximum = 10000000,
                Value = Math.Clamp(currentDstOptions.BlockStartIndex, 0, 10000000),
                Dock = DockStyle.Fill,
                ThousandsSeparator = true
            };
            var periodField = UiFactory.Field("Период блока, мс", _numBlockPeriod,
                "1–3 600 000 мс. Одна строка CSV на каждый цикл шины.");
            periodField.Margin = new Padding(0, 0, 12, 0);
            var anchorField = UiFactory.Field("Номер якорной посылки", _numBlockStart,
                "Message Number из .trc. 0 — определить автоматически.");
            anchorField.Margin = Padding.Empty;
            dstFields.Controls.Add(periodField, 0, 0);
            dstFields.Controls.Add(anchorField, 1, 0);

            _chkIncludeDeviceIdRow = new CheckBox
            {
                Text = "Добавлять строку с ID посылок",
                AutoSize = true,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 16, 0, 4),
                Checked = includeDeviceIdHeaderRow
            };
            var labelIdRowHint = new Label
            {
                Text = "ID будут расположены над именами параметров. «Шаг» и «Время» остаются в строке с именами.",
                AutoSize = true,
                Dock = DockStyle.Fill,
                Font = Typography.Secondary,
                ForeColor = AppTheme.TextSecondary,
                Margin = new Padding(24, 0, 0, 0)
            };

            _comboBatchMode = new ModernComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Fill
            };
            _comboBatchMode.Items.Add("Отдельный файл на каждый входной лог");
            _comboBatchMode.Items.Add("В единый файл (.trc / CSV)");
            _comboBatchMode.Items.Add("Разбить .trc на отдельные файлы по датам (из содержимого)");

            _formatsList = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                Height = 164,
                IntegralHeight = false,
                CheckOnClick = true,
                BorderStyle = BorderStyle.None,
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.TextPrimary,
                Font = Typography.Body,
                Margin = new Padding(0, 8, 0, 8)
            };
            _formatsList.ItemCheck += formatsList_ItemCheck;

            _formatsEmptyState = new EmptyState
            {
                Dock = DockStyle.Fill,
                Visible = false,
                Icon = IconKind.Folder,
                Title = "Папка не выбрана",
                Description = "Выберите папку с логами в основном окне, чтобы настроить доступные форматы.",
                MinimumSize = new Size(0, 140),
                Margin = Padding.Empty
            };

            _formatsHint = new Label
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                Font = Typography.Secondary,
                ForeColor = AppTheme.TextSecondary,
                Margin = new Padding(0, 4, 0, 0),
                Text = ""
            };
            _okButton = new ModernButton
            {
                Text = "Применить",
                Icon = IconKind.Check,
                Variant = ButtonVariant.Primary,
                DialogResult = DialogResult.OK,
                AutoSize = true,
                MinimumSize = new Size(140, 40)
            };
            _okButton.Click += buttonOk_Click;

            var buttonCancel = new ModernButton
            {
                Text = "Отмена",
                Variant = ButtonVariant.Ghost,
                DialogResult = DialogResult.Cancel,
                AutoSize = true,
                MinimumSize = new Size(100, 40)
            };

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(24),
                Margin = Padding.Empty
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var header = UiFactory.Header("Параметры сохранения",
                "Выберите результат обработки и правила для папки с логами.", IconKind.Save);
            header.Margin = new Padding(0, 0, 0, 20);
            root.Controls.Add(header, 0, 0);

            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Margin = Padding.Empty };
            var sections = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(0, 0, 8, 0),
                Margin = Padding.Empty
            };
            sections.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            sections.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            sections.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            sections.Controls.Add(CreateSection("Выходной файл", "Формат и структура сохранённых данных.",
                UiFactory.Field("Формат файла", _comboOutputFormat), _dstPanel,
                _chkIncludeDeviceIdRow, labelIdRowHint), 0, 0);
            sections.Controls.Add(CreateSection("Обработка папки", "Настройки применяются, когда источником выбрана папка.",
                UiFactory.Field("Как сохранять результаты", _comboBatchMode),
                new Label { Text = "Включать форматы", Font = Typography.CardTitle, AutoSize = true,
                    Margin = new Padding(0, 16, 0, 4) }, _formatsList, _formatsEmptyState, _formatsHint), 0, 1);
            scroll.Controls.Add(sections);
            root.Controls.Add(scroll, 0, 1);
            var footer = UiFactory.Footer(_okButton, buttonCancel);
            footer.Margin = new Padding(0, 20, 0, 0);
            root.Controls.Add(footer, 0, 2);
            Controls.Add(root);

            AcceptButton = _okButton;
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
            UpdateOkEnabledState(_okButton);
            ResumeLayout(true);
            AppTheme.Apply(this);
        }

        private static ModernCard CreateSection(string title, string subtitle, params Control[] content)
        {
            var card = new ModernCard
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 16)
            };
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = content.Length + 2,
                Margin = Padding.Empty
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.Controls.Add(new Label { Text = title, AutoSize = true, Font = Typography.SectionTitle,
                ForeColor = AppTheme.TextPrimary, Margin = new Padding(0, 0, 0, 4) }, 0, 0);
            layout.Controls.Add(new Label { Text = subtitle, AutoSize = true, Dock = DockStyle.Fill,
                Font = Typography.Secondary, ForeColor = AppTheme.TextSecondary,
                Margin = new Padding(0, 0, 0, 16) }, 0, 1);
            for (int i = 0; i < content.Length; i++)
            {
                content[i].Dock = DockStyle.Fill;
                layout.Controls.Add(content[i], 0, i + 2);
            }
            for (int i = 0; i < layout.RowCount; i++)
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            card.Controls.Add(layout);
            return card;
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
                _formatsList.Visible = false;
                _formatsEmptyState.Visible = true;
                _formatsEmptyState.Title = "Папка не выбрана";
                _formatsEmptyState.Description = "Выберите папку с логами в основном окне, чтобы настроить доступные форматы.";
                _formatsHint.Text = "Доступно при выборе папки с логами.";
                _suppressFormatsEvents = false;
                return;
            }

            var inv = LogFolderScanner.Scan(folder);
            int total = inv.Counts.Values.Sum();

            _formatsList.Enabled = true;
            _formatsList.Visible = total > 0;
            _formatsEmptyState.Visible = total == 0;
            if (total == 0)
            {
                _formatsEmptyState.Title = "В папке нет поддерживаемых логов";
                _formatsEmptyState.Description = "Поддерживаются TRC, ASC, CSV и текстовые логи CANfox. Выберите другую папку в основном окне.";
            }
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
                QueueFormatsStateRefresh();
                return;
            }

            QueueFormatsStateRefresh();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            UpdateOkEnabledState(_okButton);
        }

        private void QueueFormatsStateRefresh()
        {
            // ItemCheck is raised before CheckedListBox commits e.NewValue.
            // Read the final states on the next UI turn, once the form has a handle.
            if (!IsHandleCreated || IsDisposed || Disposing || _formatsRefreshPending)
                return;
            _formatsRefreshPending = true;
            BeginInvoke((Action)(() =>
            {
                _formatsRefreshPending = false;
                if (IsDisposed || Disposing || _suppressFormatsEvents) return;
                _suppressFormatsEvents = true;
                try
                {
                    SyncAllCheckBoxState();
                }
                finally
                {
                    _suppressFormatsEvents = false;
                }
                UpdateOkEnabledState(_okButton);
            }));
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
            SelectedFolderFormats = _formatsList.Enabled ? GetFormatsSelectionFromList() : LogFormatKind.All;
        }
    }
}
