namespace logReader.UI
{
    internal sealed class FormatConversionDialog : Form
    {
        private readonly TextBox _inputPathTextBox;
        private readonly TextBox _outputPathTextBox;
        private readonly ComboBox _pairComboBox;
        private readonly Button _convertButton;
        private readonly Button _openButton;
        private readonly Button _browseInputButton;
        private readonly Button _browseOutputButton;
        private readonly Button _closeButton;
        private readonly InlineNotice _outcome;
        private readonly ProgressBar _progress;
        private readonly Label _progressLabel;
        private readonly TableLayoutPanel _progressPanel;
        private readonly List<FormatConversionPair> _pairs;
        private readonly Action<string> _log;
        private bool _suppressOutputTextChanged;
        private bool _outputEditedByUser;
        private string? _convertedOutputPath;
        private bool _busy;
        private string? _lastConversionError;

        internal FormatConversionDialog(
            IEnumerable<FormatConversionPair> pairs,
            string initialPath,
            Action<string> log)
        {
            _pairs = pairs.ToList();
            _log = log;
            if (_pairs.Count == 0)
                throw new ArgumentException("Должна быть доступна хотя бы одна пара конвертации.", nameof(pairs));

            SuspendLayout();
            Text = "Преобразование формата";
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(736, 650);
            MinimumSize = new Size(600, 530);

            _inputPathTextBox = new ModernTextBox
            {
                Dock = DockStyle.Fill,
                PlaceholderText = "Выберите исходный .trc или CSV файл",
                AccessibleName = "Исходный файл",
                Text = initialPath
            };
            _inputPathTextBox.TextChanged += (_, _) =>
            {
                ResetFieldError(_inputPathTextBox);
                ClearConversionResult();
                UpdateDefaultOutputPath();
                UpdateConvertButtonState();
            };

            _browseInputButton = new ModernButton
            {
                Text = "Выбрать",
                Icon = IconKind.Folder,
                Variant = ButtonVariant.Secondary,
                Dock = DockStyle.Fill,
                Width = 116
            };
            _browseInputButton.Click += (_, _) => BrowseInputFile();

            _pairComboBox = new ModernComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _pairComboBox.DisplayMember = nameof(FormatConversionPair.DisplayName);
            foreach (var pair in _pairs)
                _pairComboBox.Items.Add(pair);
            _pairComboBox.SelectedIndex = 0;
            _pairComboBox.SelectedIndexChanged += (_, _) =>
            {
                ClearConversionResult();
                UpdateDefaultOutputPath();
                UpdateConvertButtonState();
            };

            _outputPathTextBox = new ModernTextBox
            {
                Dock = DockStyle.Fill,
                PlaceholderText = "Путь для сохранения результата",
                AccessibleName = "Файл результата"
            };
            _outputPathTextBox.TextChanged += (_, _) =>
            {
                ResetFieldError(_outputPathTextBox);
                if (!_suppressOutputTextChanged)
                {
                    _outputEditedByUser = true;
                    ClearConversionResult();
                }
                UpdateConvertButtonState();
            };

            _browseOutputButton = new ModernButton
            {
                Text = "Выбрать",
                Icon = IconKind.Folder,
                Variant = ButtonVariant.Secondary,
                Dock = DockStyle.Fill,
                Width = 116
            };
            _browseOutputButton.Click += (_, _) => BrowseOutputFile();

            _convertButton = new ModernButton
            {
                Text = "Преобразовать",
                Icon = IconKind.Convert,
                Variant = ButtonVariant.Primary,
                AutoSize = true,
                MinimumSize = new Size(168, 40)
            };
            _convertButton.Click += convertButton_Click;

            _openButton = new ModernButton
            {
                Text = "Открыть результат",
                Icon = IconKind.ExternalLink,
                Variant = ButtonVariant.Secondary,
                AutoSize = true,
                MinimumSize = new Size(168, 40),
                Enabled = false
            };
            _openButton.Click += openButton_Click;

            _closeButton = new ModernButton
            {
                Text = "Закрыть",
                Variant = ButtonVariant.Ghost,
                DialogResult = DialogResult.Cancel,
                AutoSize = true,
                MinimumSize = new Size(100, 40)
            };

            _outcome = new InlineNotice
            {
                Dock = DockStyle.Fill,
                Tone = StatusTone.Info,
                Text = "Результат будет сохранён в отдельный файл. Исходный лог останется без изменений.",
                Margin = new Padding(0, 16, 0, 0),
                MinimumSize = new Size(0, 64)
            };
            _progress = new ProgressBar
            {
                Dock = DockStyle.Fill,
                Height = 8,
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 24,
                Margin = new Padding(0, 8, 0, 0)
            };
            _progressLabel = new Label
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                Font = Typography.Secondary,
                ForeColor = AppTheme.TextSecondary,
                Margin = Padding.Empty
            };
            _progressPanel = new TableLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Visible = false,
                Margin = new Padding(0, 16, 0, 0)
            };
            _progressPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _progressPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _progressPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 16));
            _progressPanel.Controls.Add(_progressLabel, 0, 0);
            _progressPanel.Controls.Add(_progress, 0, 1);

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
            var header = UiFactory.Header("Преобразование формата",
                "Переведите лог в формат для следующего этапа работы.", IconKind.Convert);
            header.Margin = new Padding(0, 0, 0, 20);
            root.Controls.Add(header, 0, 0);

            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Margin = Padding.Empty };
            var card = new ModernCard { AutoSize = true, Dock = DockStyle.Top, Margin = Padding.Empty };
            var fields = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 5,
                Margin = Padding.Empty
            };
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < fields.RowCount; i++)
                fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var inputField = UiFactory.Field("Исходный файл", BuildPathRow(_inputPathTextBox, _browseInputButton),
                "Формат файла должен соответствовать источнику выбранного преобразования.");
            inputField.Margin = new Padding(0, 0, 0, 20);
            fields.Controls.Add(inputField, 0, 0);
            var pairField = UiFactory.Field("Преобразование", _pairComboBox);
            pairField.Margin = new Padding(0, 0, 0, 20);
            fields.Controls.Add(pairField, 0, 1);
            fields.Controls.Add(UiFactory.Field("Сохранить результат", BuildPathRow(_outputPathTextBox, _browseOutputButton),
                "Имя предложено автоматически. Вы можете выбрать другую папку или изменить его."), 0, 2);
            fields.Controls.Add(_outcome, 0, 3);
            fields.Controls.Add(_progressPanel, 0, 4);
            card.Controls.Add(fields);
            scroll.Controls.Add(card);
            root.Controls.Add(scroll, 0, 1);
            var footer = UiFactory.Footer(_convertButton, _openButton, _closeButton);
            footer.Margin = new Padding(0, 20, 0, 0);
            root.Controls.Add(footer, 0, 2);
            Controls.Add(root);

            AcceptButton = _convertButton;
            CancelButton = _closeButton;

            UpdateDefaultOutputPath();
            UpdateConvertButtonState();
            ResumeLayout(true);
            AppTheme.Apply(this);
        }

        private static Control BuildPathRow(TextBox input, Button browse)
        {
            var row = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Height = 40,
                ColumnCount = 2,
                RowCount = 1,
                Margin = Padding.Empty
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var inputHost = UiFactory.Input(input);
            inputHost.Dock = DockStyle.Fill;
            inputHost.Margin = new Padding(0, 0, 8, 0);
            browse.Margin = Padding.Empty;
            browse.MinimumSize = new Size(116, 40);
            row.Controls.Add(inputHost, 0, 0);
            row.Controls.Add(browse, 1, 0);
            return row;
        }

        private async void convertButton_Click(object? sender, EventArgs e)
        {
            if (_busy)
                return;

            string inputPath;
            string outPath;
            FormatConversionPair pair;
            try
            {
                if (!TryPrepareConversion(out inputPath, out outPath, out pair))
                    return;
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                ReportValidation("Ошибка: проверьте путь файла. " + ex.Message);
                return;
            }

            ClearConversionResult();
            _lastConversionError = null;
            SetUiBusy(true);
            bool success;
            try
            {
                success = await Task.Run(() => RunConversion(inputPath, outPath, pair));
            }
            catch (Exception ex)
            {
                _log("Критическая ошибка: " + ex.Message);
                _lastConversionError = "Не удалось завершить преобразование: " + ex.Message;
                success = false;
            }
            finally
            {
                SetUiBusy(false);
            }

            if (success)
                SetConversionResult(outPath);
            else
                ShowOutcome(_lastConversionError ?? "Преобразование не завершено. Подробности доступны в журнале главного окна.", StatusTone.Error);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_busy)
            {
                e.Cancel = true;
                ShowOutcome("Дождитесь завершения преобразования. Файл ещё записывается.", StatusTone.Info);
                return;
            }
            base.OnFormClosing(e);
        }

        private void openButton_Click(object? sender, EventArgs e)
        {
            if (_busy)
                return;
            if (string.IsNullOrWhiteSpace(_convertedOutputPath))
            {
                _log("Нет файла для открытия. Сначала выполните преобразование.");
                ShowOutcome("Сначала выполните преобразование, чтобы открыть результат.", StatusTone.Info);
                return;
            }

            if (!File.Exists(_convertedOutputPath))
            {
                _log("Файл не найден: " + _convertedOutputPath);
                ClearConversionResult();
                ShowOutcome("Результирующий файл не найден. Выполните преобразование ещё раз.", StatusTone.Warning);
                return;
            }

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = _convertedOutputPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _log("Не удалось открыть: " + ex.Message);
                ShowOutcome("Не удалось открыть файл: " + ex.Message, StatusTone.Error);
            }
        }

        private void SetConversionResult(string outPath)
        {
            _convertedOutputPath = Path.GetFullPath(outPath);
            _openButton.Enabled = true;
            ShowOutcome($"Готово. Файл «{Path.GetFileName(outPath)}» сохранён. Вы можете открыть результат.", StatusTone.Success);
        }

        private void ClearConversionResult()
        {
            _convertedOutputPath = null;
            _openButton.Enabled = false;
            ShowOutcome("Результат будет сохранён в отдельный файл. Исходный лог останется без изменений.", StatusTone.Info);
        }

        private void ShowOutcome(string message, StatusTone tone)
        {
            _outcome.Tone = tone;
            _outcome.Text = message;
        }

        private void ReportValidation(string message, TextBox? invalidField = null)
        {
            _log(message);
            ShowOutcome(message, StatusTone.Error);
            if (invalidField is ModernTextBox field)
            {
                field.HasError = true;
                field.ErrorMessage = message;
            }
            invalidField?.Focus();
        }

        private static void ResetFieldError(TextBox input)
        {
            if (input is ModernTextBox field)
            {
                field.HasError = false;
                field.ErrorMessage = null;
            }
        }

        private bool TryPrepareConversion(
            out string inputPath,
            out string outPath,
            out FormatConversionPair pair)
        {
            inputPath = _inputPathTextBox.Text.Trim();
            outPath = _outputPathTextBox.Text.Trim();
            pair = SelectedPair;

            if (string.IsNullOrWhiteSpace(inputPath))
            {
                ReportValidation("Ошибка: файл лога не найден.", _inputPathTextBox);
                return false;
            }
            if (Directory.Exists(inputPath))
            {
                ReportValidation("Ошибка: для конвертации нужно выбрать файл, а не папку.", _inputPathTextBox);
                return false;
            }
            if (!File.Exists(inputPath))
            {
                ReportValidation("Ошибка: файл лога не найден.", _inputPathTextBox);
                return false;
            }

            string inputExt = Path.GetExtension(inputPath);
            if (!inputExt.Equals(pair.SourceExtension, StringComparison.OrdinalIgnoreCase))
            {
                ReportValidation($"Ошибка: выбранный файл не соответствует формату источника ({pair.SourceExtension}).", _inputPathTextBox);
                return false;
            }

            if (string.IsNullOrWhiteSpace(outPath))
            {
                ReportValidation("Ошибка: укажите путь выходного файла.", _outputPathTextBox);
                return false;
            }

            if (!Path.GetExtension(outPath).Equals(pair.TargetExtension, StringComparison.OrdinalIgnoreCase))
                outPath = Path.ChangeExtension(outPath, pair.TargetExtension);

            string? outDir = Path.GetDirectoryName(outPath);
            if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
            {
                ReportValidation($"Ошибка: директория для сохранения не существует: {outDir}", _outputPathTextBox);
                return false;
            }

            string outFull = Path.GetFullPath(outPath);
            string inFull = Path.GetFullPath(inputPath);
            if (outFull.Equals(inFull, StringComparison.OrdinalIgnoreCase))
            {
                ReportValidation("Ошибка: файл вывода совпадает с файлом лога. Укажите другой путь.", _outputPathTextBox);
                return false;
            }

            if (File.Exists(outPath))
            {
                try { using var fs = new FileStream(outPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None); }
                catch
                {
                    ReportValidation("Ошибка: выходной файл уже открыт в другой программе. Закройте его и попробуйте снова.", _outputPathTextBox);
                    return false;
                }
            }

            if (!string.Equals(_outputPathTextBox.Text.Trim(), outPath, StringComparison.OrdinalIgnoreCase))
            {
                _suppressOutputTextChanged = true;
                _outputPathTextBox.Text = outPath;
                _suppressOutputTextChanged = false;
            }

            outPath = outFull;
            return true;
        }

        private bool RunConversion(string inputPath, string outPath, FormatConversionPair pair)
        {
            bool hadError = false;
            void LogWrap(string message)
            {
                if (message.StartsWith("Ошибка:", StringComparison.Ordinal))
                {
                    hadError = true;
                    _lastConversionError = message;
                }
                _log(message);
            }

            if (pair.Id == "trc_to_asc")
            {
                new TrcToAscConverter().Convert(inputPath, outPath, LogWrap);
            }
            else if (pair.Id == "csv_to_asc")
            {
                if (!MatrixCsvLogParser.LooksLikeMatrixCsv(inputPath, LogFileEncoding.Detect(inputPath)))
                {
                    LogWrap($"Ошибка: для конвертации нужен {LogFormatUiNames.Csv}, не {LogFormatUiNames.LegacyCsv}.");
                    return false;
                }

                new MatrixCsvToAscConverter().Convert(inputPath, outPath, LogWrap);
            }
            else
            {
                LogWrap($"Ошибка: конвертация {pair.DisplayName} пока не поддерживается.");
                return false;
            }

            return File.Exists(outPath) && !hadError;
        }

        private void SetUiBusy(bool busy)
        {
            _busy = busy;
            _inputPathTextBox.Enabled = !busy;
            _outputPathTextBox.Enabled = !busy;
            _pairComboBox.Enabled = !busy;
            _browseInputButton.Enabled = !busy;
            _browseOutputButton.Enabled = !busy;
            _closeButton.Enabled = !busy;
            _progressPanel.Visible = busy;
            _progress.MarqueeAnimationSpeed = busy ? 24 : 0;
            UpdateConvertButtonState();
            _convertButton.Text = busy ? "Преобразование..." : "Преобразовать";

            if (busy)
            {
                _openButton.Enabled = false;
                _progressLabel.Text = $"{SelectedPair.DisplayName} · запись результата";
                ShowOutcome("Выполняется преобразование. Для больших логов это может занять некоторое время.", StatusTone.Info);
            }
            else if (!string.IsNullOrEmpty(_convertedOutputPath))
                _openButton.Enabled = true;

            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        }

        private void BrowseInputFile()
        {
            if (_busy) return;
            var pair = SelectedPair;
            using var ofd = new OpenFileDialog();
            string sourceExt = pair.SourceExtension.TrimStart('.');
            ofd.Filter = $"{sourceExt.ToUpperInvariant()} (*{pair.SourceExtension})|*{pair.SourceExtension}|Все файлы (*.*)|*.*";
            if (ofd.ShowDialog(this) == DialogResult.OK)
                _inputPathTextBox.Text = ofd.FileName;
        }

        private void BrowseOutputFile()
        {
            if (_busy) return;
            var pair = SelectedPair;
            using var sfd = new SaveFileDialog();
            string targetExt = pair.TargetExtension.TrimStart('.');
            sfd.Filter = $"{targetExt.ToUpperInvariant()} (*{pair.TargetExtension})|*{pair.TargetExtension}|Все файлы (*.*)|*.*";
            sfd.DefaultExt = targetExt;
            sfd.AddExtension = true;
            if (!string.IsNullOrWhiteSpace(_outputPathTextBox.Text))
                sfd.FileName = _outputPathTextBox.Text;
            else if (!string.IsNullOrWhiteSpace(_inputPathTextBox.Text))
                sfd.FileName = BuildDefaultOutputPath(_inputPathTextBox.Text, pair.TargetExtension);

            if (sfd.ShowDialog(this) == DialogResult.OK)
                _outputPathTextBox.Text = sfd.FileName;
        }

        private void UpdateDefaultOutputPath()
        {
            string input = _inputPathTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(input))
                return;

            if (_outputEditedByUser)
                return;

            string defaultOutput = BuildDefaultOutputPath(input, SelectedPair.TargetExtension);
            _suppressOutputTextChanged = true;
            _outputPathTextBox.Text = defaultOutput;
            _suppressOutputTextChanged = false;
        }

        private static string BuildDefaultOutputPath(string inputPath, string targetExtension)
        {
            string directory = Path.GetDirectoryName(inputPath) ?? "";
            string name = Path.GetFileNameWithoutExtension(inputPath);
            return Path.Combine(directory, name + targetExtension);
        }

        private FormatConversionPair SelectedPair => (FormatConversionPair)_pairComboBox.SelectedItem!;

        private void UpdateConvertButtonState()
        {
            _convertButton.Enabled = !_busy && _pairComboBox.SelectedItem != null
                && !string.IsNullOrWhiteSpace(_inputPathTextBox.Text)
                && !string.IsNullOrWhiteSpace(_outputPathTextBox.Text);
        }
    }

    internal sealed class FormatConversionPair
    {
        internal FormatConversionPair(string id, string displayName, string sourceExtension, string targetExtension)
        {
            Id = id;
            DisplayName = displayName;
            SourceExtension = sourceExtension;
            TargetExtension = targetExtension;
        }

        internal string Id { get; }
        internal string DisplayName { get; }
        internal string SourceExtension { get; }
        internal string TargetExtension { get; }

        public override string ToString() => DisplayName;
    }
}
