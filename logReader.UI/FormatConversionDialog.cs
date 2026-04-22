namespace logReader.UI
{
    internal sealed class FormatConversionDialog : Form
    {
        private readonly TextBox _inputPathTextBox;
        private readonly TextBox _outputPathTextBox;
        private readonly ComboBox _pairComboBox;
        private readonly Button _okButton;
        private readonly List<FormatConversionPair> _pairs;
        private bool _suppressOutputTextChanged;
        private bool _outputEditedByUser;

        internal string SelectedInputPath => _inputPathTextBox.Text.Trim();
        internal string SelectedOutputPath => _outputPathTextBox.Text.Trim();
        internal FormatConversionPair SelectedPair => (FormatConversionPair)_pairComboBox.SelectedItem!;

        internal FormatConversionDialog(IEnumerable<FormatConversionPair> pairs, string initialPath)
        {
            _pairs = pairs.ToList();
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
                UpdateDefaultOutputPath();
                UpdateOkButtonState();
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
                UpdateDefaultOutputPath();
                UpdateOkButtonState();
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
                    _outputEditedByUser = true;
                UpdateOkButtonState();
            };

            var browseOutputButton = new Button
            {
                Text = "Обзор",
                Location = new Point(474, 143),
                Size = new Size(74, 25)
            };
            browseOutputButton.Click += (_, _) => BrowseOutputFile();

            _okButton = new Button
            {
                Text = "ОК",
                DialogResult = DialogResult.OK,
                Location = new Point(392, 188),
                Size = new Size(75, 25)
            };

            var cancelButton = new Button
            {
                Text = "Отмена",
                DialogResult = DialogResult.Cancel,
                Location = new Point(473, 188),
                Size = new Size(75, 25)
            };

            Controls.Add(inputPathLabel);
            Controls.Add(_inputPathTextBox);
            Controls.Add(browseInputButton);
            Controls.Add(pairLabel);
            Controls.Add(_pairComboBox);
            Controls.Add(outputPathLabel);
            Controls.Add(_outputPathTextBox);
            Controls.Add(browseOutputButton);
            Controls.Add(_okButton);
            Controls.Add(cancelButton);

            AcceptButton = _okButton;
            CancelButton = cancelButton;

            UpdateDefaultOutputPath();
            UpdateOkButtonState();
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

        private void UpdateOkButtonState()
        {
            _okButton.Enabled = _pairComboBox.SelectedItem != null
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
