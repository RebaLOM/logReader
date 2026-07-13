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
        private bool _suppressFormatsEvents;

        internal OutputFormat SelectedOutputFormat { get; private set; }
        internal BatchOutputMode SelectedBatchMode { get; private set; }
        internal LogFormatKind SelectedFolderFormats { get; private set; } = LogFormatKind.All;
        internal DstConnectOptions SelectedDstConnectOptions { get; private set; } = new();

        internal SaveOptionsForm(
            OutputFormat currentFormat,
            BatchOutputMode currentBatchMode,
            DstConnectOptions currentDstOptions,
            string? logFolderPath,
            LogFormatKind currentFolderFormats)
        {
            SelectedDstConnectOptions = CloneDstOptions(currentDstOptions);
            Text = "Параметры сохранения";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(680, 442);
            MinimumSize = Size;
            MaximumSize = Size;

            var labelFormat = new Label
            {
                Text = "Формат выходного файла:",
                AutoSize = true,
                Location = new Point(16, 20)
            };

            _comboOutputFormat = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(16, 45),
                Size = new Size(645, 23)
            };
            _comboOutputFormat.Items.Add("XLSX");
            _comboOutputFormat.Items.Add("CSV");
            _comboOutputFormat.Items.Add("CSV ДСТ Коннект");
            _comboOutputFormat.SelectedIndexChanged += (_, _) => UpdateDstPanelVisibility();

            _dstPanel = new Panel
            {
                Location = new Point(16, 78),
                Size = new Size(645, 68),
                BorderStyle = BorderStyle.FixedSingle
            };

            var labelBlockPeriod = new Label { Text = "Период блока, мс:", AutoSize = true, Location = new Point(8, 10) };
            _numBlockPeriod = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 3600000,
                Value = Math.Clamp(currentDstOptions.BlockPeriodMs, 1, 3600000),
                Location = new Point(180, 8),
                Width = 80
            };
            var labelBlockPeriodHint = new Label
            {
                Text = "цикл шины; одна строка CSV на каждый блок",
                AutoSize = true,
                Location = new Point(270, 10),
                ForeColor = Color.DimGray
            };

            var labelBlockStart = new Label { Text = "Номер якорной посылки:", AutoSize = true, Location = new Point(8, 38) };
            _numBlockStart = new NumericUpDown
            {
                Minimum = 0,
                Maximum = 10000000,
                Value = Math.Max(0, currentDstOptions.BlockStartIndex),
                Location = new Point(180, 36),
                Width = 80
            };
            var labelBlockHint = new Label
            {
                Text = "Message Number из .trc; 0 — авто; иначе с этой посылки",
                AutoSize = true,
                Location = new Point(270, 38),
                ForeColor = Color.DimGray
            };

            _dstPanel.Controls.Add(labelBlockPeriod);
            _dstPanel.Controls.Add(_numBlockPeriod);
            _dstPanel.Controls.Add(labelBlockPeriodHint);
            _dstPanel.Controls.Add(labelBlockStart);
            _dstPanel.Controls.Add(_numBlockStart);
            _dstPanel.Controls.Add(labelBlockHint);

            var labelBatch = new Label
            {
                Text = "Режим сохранения при обработке папки:",
                AutoSize = true,
                Location = new Point(16, 156)
            };

            _comboBatchMode = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(16, 181),
                Size = new Size(645, 23)
            };
            _comboBatchMode.Items.Add("Отдельный файл на каждый входной лог");
            _comboBatchMode.Items.Add("В единый файл (.trc / CSV)");
            _comboBatchMode.Items.Add("Разбить .trc на отдельные файлы по датам (из содержимого)");

            var labelFormats = new Label
            {
                Text = "Форматы в папке:",
                AutoSize = true,
                Location = new Point(16, 220)
            };

            _formatsList = new CheckedListBox
            {
                Location = new Point(16, 244),
                Size = new Size(645, 120),
                CheckOnClick = true
            };
            _formatsList.ItemCheck += formatsList_ItemCheck;

            _formatsHint = new Label
            {
                AutoSize = false,
                Location = new Point(16, 368),
                Size = new Size(645, 18),
                ForeColor = Color.DimGray,
                Text = ""
            };

            var buttonOk = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Location = new Point(505, 402),
                Size = new Size(75, 26)
            };
            buttonOk.Click += buttonOk_Click;
            _formatsList.ItemCheck += (_, _) => UpdateOkEnabledState(buttonOk);

            var buttonCancel = new Button
            {
                Text = "Отмена",
                DialogResult = DialogResult.Cancel,
                Location = new Point(586, 402),
                Size = new Size(75, 26)
            };

            Controls.Add(labelFormat);
            Controls.Add(_comboOutputFormat);
            Controls.Add(_dstPanel);
            Controls.Add(labelBatch);
            Controls.Add(_comboBatchMode);
            Controls.Add(labelFormats);
            Controls.Add(_formatsList);
            Controls.Add(_formatsHint);
            Controls.Add(buttonOk);
            Controls.Add(buttonCancel);

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

            SelectedFolderFormats = _formatsList.Enabled ? GetFormatsSelectionFromList() : LogFormatKind.All;
        }
    }
}
