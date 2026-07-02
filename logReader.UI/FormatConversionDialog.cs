namespace logReader.UI
{
    internal sealed class FormatConversionDialog : Form
    {
        private readonly TextBox _inputPathTextBox;
        private readonly TextBox _outputPathTextBox;
        private readonly ComboBox _pairComboBox;
        private readonly Button _convertButton;
        private readonly Button _openButton;
        private readonly List<FormatConversionPair> _pairs;
        private readonly Action<string> _log;
        private bool _suppressOutputTextChanged;
        private bool _outputEditedByUser;
        private string? _convertedOutputPath;

        internal FormatConversionDialog(
            IEnumerable<FormatConversionPair> pairs,
            string initialPath,
            Action<string> log)
        {
            _pairs = pairs.ToList();
            _log = log;
            if (_pairs.Count == 0)
                throw new ArgumentException("Должна быть доступна хотя бы одна пара конвертации.", nameof(pairs));

            Text = "Смена формата";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(560, 230);
            MinimumSize = Size;
            MaximumSize = Size;

            var inputPathLabel = new Label
            {
                AutoSize = true,
                Location = new Point(12, 12),
                Text = "Файл для конвертации:"
            };

            _inputPathTextBox = new TextBox
            {
                Location = new Point(12, 32),
                Size = new Size(456, 23),
                Text = initialPath
            };
            _inputPathTextBox.TextChanged += (_, _) =>
            {
                ClearConversionResult();
                UpdateDefaultOutputPath();
                UpdateConvertButtonState();
            };

            var browseInputButton = new Button
            {
                Text = "Обзор",
                Location = new Point(474, 31),
                Size = new Size(74, 25)
            };
            browseInputButton.Click += (_, _) => BrowseInputFile();

            var pairLabel = new Label
            {
                AutoSize = true,
                Location = new Point(12, 68),
                Text = "Преобразование:"
            };

            _pairComboBox = new ComboBox
            {
                Location = new Point(12, 88),
                Size = new Size(536, 23),
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

            var outputPathLabel = new Label
            {
                AutoSize = true,
                Location = new Point(12, 124),
                Text = "Файл после конвертации:"
            };

            _outputPathTextBox = new TextBox
            {
                Location = new Point(12, 144),
                Size = new Size(456, 23),
            };
            _outputPathTextBox.TextChanged += (_, _) =>
            {
                if (!_suppressOutputTextChanged)
                {
                    _outputEditedByUser = true;
                    ClearConversionResult();
                }
                UpdateConvertButtonState();
            };

            var browseOutputButton = new Button
            {
                Text = "Обзор",
                Location = new Point(474, 143),
                Size = new Size(74, 25)
            };
            browseOutputButton.Click += (_, _) => BrowseOutputFile();

            _convertButton = new Button
            {
                Text = "Преобразовать",
                Location = new Point(248, 188),
                Size = new Size(110, 25)
            };
            _convertButton.Click += convertButton_Click;

            _openButton = new Button
            {
                Text = "Открыть",
                Location = new Point(364, 188),
                Size = new Size(85, 25),
                Enabled = false
            };
            _openButton.Click += openButton_Click;

            var cancelButton = new Button
            {
                Text = "Закрыть",
                DialogResult = DialogResult.Cancel,
                Location = new Point(455, 188),
                Size = new Size(93, 25)
            };

            Controls.Add(inputPathLabel);
            Controls.Add(_inputPathTextBox);
            Controls.Add(browseInputButton);
            Controls.Add(pairLabel);
            Controls.Add(_pairComboBox);
            Controls.Add(outputPathLabel);
            Controls.Add(_outputPathTextBox);
            Controls.Add(browseOutputButton);
            Controls.Add(_convertButton);
            Controls.Add(_openButton);
            Controls.Add(cancelButton);

            AcceptButton = _convertButton;
            CancelButton = cancelButton;

            UpdateDefaultOutputPath();
            UpdateConvertButtonState();
        }

        private async void convertButton_Click(object? sender, EventArgs e)
        {
            if (!TryPrepareConversion(
                    out string inputPath,
                    out string outPath,
                    out FormatConversionPair pair))
            {
                return;
            }

            ClearConversionResult();
            SetUiBusy(true);
            bool success;
            try
            {
                success = await Task.Run(() => RunConversion(inputPath, outPath, pair));
            }
            catch (Exception ex)
            {
                _log("Критическая ошибка: " + ex.Message);
                success = false;
            }
            finally
            {
                SetUiBusy(false);
            }

            if (success)
                SetConversionResult(outPath);
        }

        private void openButton_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_convertedOutputPath))
            {
                _log("Нет файла для открытия. Сначала выполните преобразование.");
                return;
            }

            if (!File.Exists(_convertedOutputPath))
            {
                _log("Файл не найден: " + _convertedOutputPath);
                ClearConversionResult();
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
            }
        }

        private void SetConversionResult(string outPath)
        {
            _convertedOutputPath = Path.GetFullPath(outPath);
            _openButton.Enabled = true;
        }

        private void ClearConversionResult()
        {
            _convertedOutputPath = null;
            _openButton.Enabled = false;
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
                _log("Ошибка: файл лога не найден.");
                return false;
            }
            if (Directory.Exists(inputPath))
            {
                _log("Ошибка: для конвертации нужно выбрать файл, а не папку.");
                return false;
            }
            if (!File.Exists(inputPath))
            {
                _log("Ошибка: файл лога не найден.");
                return false;
            }

            string inputExt = Path.GetExtension(inputPath);
            if (!inputExt.Equals(pair.SourceExtension, StringComparison.OrdinalIgnoreCase))
            {
                _log($"Ошибка: выбранный файл не соответствует формату источника ({pair.SourceExtension}).");
                return false;
            }

            if (string.IsNullOrWhiteSpace(outPath))
            {
                _log("Ошибка: укажите путь выходного файла.");
                return false;
            }

            if (!Path.GetExtension(outPath).Equals(pair.TargetExtension, StringComparison.OrdinalIgnoreCase))
                outPath = Path.ChangeExtension(outPath, pair.TargetExtension);

            string? outDir = Path.GetDirectoryName(outPath);
            if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
            {
                _log($"Ошибка: директория для сохранения не существует: {outDir}");
                return false;
            }

            string outFull = Path.GetFullPath(outPath);
            string inFull = Path.GetFullPath(inputPath);
            if (outFull.Equals(inFull, StringComparison.OrdinalIgnoreCase))
            {
                _log("Ошибка: файл вывода совпадает с файлом лога. Укажите другой путь.");
                return false;
            }

            if (File.Exists(outPath))
            {
                try { using var fs = new FileStream(outPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None); }
                catch
                {
                    _log("Ошибка: выходной файл уже открыт в другой программе. Закройте его и попробуйте снова.");
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
                    hadError = true;
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
            _inputPathTextBox.Enabled = !busy;
            _outputPathTextBox.Enabled = !busy;
            _pairComboBox.Enabled = !busy;
            foreach (Control control in Controls)
            {
                if (control is Button button
                    && button != _convertButton
                    && button != _openButton)
                {
                    button.Enabled = !busy;
                }
            }

            _convertButton.Enabled = !busy
                && _pairComboBox.SelectedItem != null
                && !string.IsNullOrWhiteSpace(_inputPathTextBox.Text)
                && !string.IsNullOrWhiteSpace(_outputPathTextBox.Text);
            _convertButton.Text = busy ? "Преобразование..." : "Преобразовать";

            if (busy)
                _openButton.Enabled = false;
            else if (!string.IsNullOrEmpty(_convertedOutputPath))
                _openButton.Enabled = true;

            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        }

        private void BrowseInputFile()
        {
            var pair = SelectedPair;
            using var ofd = new OpenFileDialog();
            string sourceExt = pair.SourceExtension.TrimStart('.');
            ofd.Filter = $"{sourceExt.ToUpperInvariant()} (*{pair.SourceExtension})|*{pair.SourceExtension}|Все файлы (*.*)|*.*";
            if (ofd.ShowDialog(this) == DialogResult.OK)
                _inputPathTextBox.Text = ofd.FileName;
        }

        private void BrowseOutputFile()
        {
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
            _convertButton.Enabled = _pairComboBox.SelectedItem != null
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
