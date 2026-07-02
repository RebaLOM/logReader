namespace logReader.UI
{
    internal sealed class SaveOptionsForm : Form
    {
        private readonly ComboBox _comboOutputFormat;
        private readonly ComboBox _comboBatchMode;
        private readonly CheckedListBox _formatsList;
        private readonly Label _formatsHint;
        private bool _suppressFormatsEvents;

        internal OutputFormat SelectedOutputFormat { get; private set; }
        internal BatchOutputMode SelectedBatchMode { get; private set; }
        internal LogFormatKind SelectedFolderFormats { get; private set; } = LogFormatKind.All;

        internal SaveOptionsForm(
            OutputFormat currentFormat,
            BatchOutputMode currentBatchMode,
            string? logFolderPath,
            LogFormatKind currentFolderFormats)
        {
            Text = "Параметры сохранения";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(680, 370);
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

            var labelBatch = new Label
            {
                Text = "Режим сохранения при обработке папки:",
                AutoSize = true,
                Location = new Point(16, 84)
            };

            _comboBatchMode = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(16, 109),
                Size = new Size(645, 23)
            };
            _comboBatchMode.Items.Add("Отдельный файл на каждый входной лог");
            _comboBatchMode.Items.Add("В единый файл (.trc / CSV)");
            _comboBatchMode.Items.Add("Разбить .trc на отдельные файлы по датам (из содержимого)");

            var labelFormats = new Label
            {
                Text = "Форматы в папке:",
                AutoSize = true,
                Location = new Point(16, 148)
            };

            _formatsList = new CheckedListBox
            {
                Location = new Point(16, 172),
                Size = new Size(645, 120),
                CheckOnClick = true
            };
            _formatsList.ItemCheck += formatsList_ItemCheck;

            _formatsHint = new Label
            {
                AutoSize = false,
                Location = new Point(16, 296),
                Size = new Size(645, 18),
                ForeColor = Color.DimGray,
                Text = ""
            };

            var buttonOk = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Location = new Point(505, 330),
                Size = new Size(75, 26)
            };
            buttonOk.Click += buttonOk_Click;
            _formatsList.ItemCheck += (_, _) => UpdateOkEnabledState(buttonOk);

            var buttonCancel = new Button
            {
                Text = "Отмена",
                DialogResult = DialogResult.Cancel,
                Location = new Point(586, 330),
                Size = new Size(75, 26)
            };

            Controls.Add(labelFormat);
            Controls.Add(_comboOutputFormat);
            Controls.Add(labelBatch);
            Controls.Add(_comboBatchMode);
            Controls.Add(labelFormats);
            Controls.Add(_formatsList);
            Controls.Add(_formatsHint);
            Controls.Add(buttonOk);
            Controls.Add(buttonCancel);

            AcceptButton = buttonOk;
            CancelButton = buttonCancel;

            _comboOutputFormat.SelectedIndex = currentFormat == OutputFormat.Csv ? 1 : 0;
            _comboBatchMode.SelectedIndex = currentBatchMode switch
            {
                BatchOutputMode.MergeToSingleFile => 1,
                BatchOutputMode.SplitTrcByDate => 2,
                _ => 0
            };

            SelectedFolderFormats = currentFolderFormats == LogFormatKind.None ? LogFormatKind.All : currentFolderFormats;
            BuildFormatsList(logFolderPath);
            UpdateOkEnabledState(buttonOk);
        }

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
            // Если selection содержит форматы, которых нет в папке — просто игнорируем их.
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

            // Если ничего не выбрано (или selection=All) — включаем всё, что присутствует.
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

            // Обрабатываем клики по "Все".
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

            // После изменения любого элемента синхронизируем "Все" и состояние OK.
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
            SelectedOutputFormat = _comboOutputFormat.SelectedIndex == 1 ? OutputFormat.Csv : OutputFormat.Xlsx;
            SelectedBatchMode = _comboBatchMode.SelectedIndex switch
            {
                1 => BatchOutputMode.MergeToSingleFile,
                2 => BatchOutputMode.SplitTrcByDate,
                _ => BatchOutputMode.PerInputFile
            };

            SelectedFolderFormats = _formatsList.Enabled ? GetFormatsSelectionFromList() : LogFormatKind.All;
        }
    }
}

