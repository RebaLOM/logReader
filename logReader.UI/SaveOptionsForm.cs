namespace logReader.UI
{
    internal sealed class SaveOptionsForm : Form
    {
        private readonly ComboBox _comboOutputFormat;
        private readonly ComboBox _comboBatchMode;

        internal OutputFormat SelectedOutputFormat { get; private set; }
        internal MainForm.BatchOutputMode SelectedBatchMode { get; private set; }

        internal SaveOptionsForm(OutputFormat currentFormat, MainForm.BatchOutputMode currentBatchMode)
        {
            Text = "Параметры сохранения";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(680, 210);
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
            _comboBatchMode.Items.Add("Сохранить все .trc в единый файл");
            _comboBatchMode.Items.Add("Разбить .trc на отдельные файлы по датам (из содержимого)");

            var buttonOk = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Location = new Point(505, 164),
                Size = new Size(75, 26)
            };
            buttonOk.Click += buttonOk_Click;

            var buttonCancel = new Button
            {
                Text = "Отмена",
                DialogResult = DialogResult.Cancel,
                Location = new Point(586, 164),
                Size = new Size(75, 26)
            };

            Controls.Add(labelFormat);
            Controls.Add(_comboOutputFormat);
            Controls.Add(labelBatch);
            Controls.Add(_comboBatchMode);
            Controls.Add(buttonOk);
            Controls.Add(buttonCancel);

            AcceptButton = buttonOk;
            CancelButton = buttonCancel;

            _comboOutputFormat.SelectedIndex = currentFormat == OutputFormat.Csv ? 1 : 0;
            _comboBatchMode.SelectedIndex = currentBatchMode switch
            {
                MainForm.BatchOutputMode.MergeTrcToSingleFile => 1,
                MainForm.BatchOutputMode.SplitTrcByDate => 2,
                _ => 0
            };
        }

        private void buttonOk_Click(object? sender, EventArgs e)
        {
            SelectedOutputFormat = _comboOutputFormat.SelectedIndex == 1 ? OutputFormat.Csv : OutputFormat.Xlsx;
            SelectedBatchMode = _comboBatchMode.SelectedIndex switch
            {
                1 => MainForm.BatchOutputMode.MergeTrcToSingleFile,
                2 => MainForm.BatchOutputMode.SplitTrcByDate,
                _ => MainForm.BatchOutputMode.PerInputFile
            };
        }
    }
}

