namespace logReader.UI
{
    // Uses the same result and button contracts as existing confirmations and validation.
    internal static class ThemedMessageBox
    {
        public static DialogResult Show(IWin32Window owner, string text, string caption,
            MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxIcon icon = MessageBoxIcon.None)
        {
            using var messageFont = new Font("Segoe UI", 9F);
            using var iconBitmap = icon switch
            {
                MessageBoxIcon.None => null,
                MessageBoxIcon.Error => SystemIcons.Error.ToBitmap(),
                MessageBoxIcon.Warning => SystemIcons.Warning.ToBitmap(),
                MessageBoxIcon.Question => SystemIcons.Question.ToBitmap(),
                _ => SystemIcons.Information.ToBitmap()
            };
            using var dialog = new Form
            {
                Text = caption,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false,
                AutoScaleMode = AutoScaleMode.Dpi, AutoScaleDimensions = new SizeF(96, 96),
                ClientSize = new Size(560, 220), MinimumSize = new Size(380, 170),
                Font = messageFont, ControlBox = buttons != MessageBoxButtons.YesNo, KeyPreview = true,
                Icon = (owner as Form)?.Icon
            };
            if (buttons == MessageBoxButtons.OK)
                dialog.FormClosing += (_, _) => dialog.DialogResult = DialogResult.OK;
            var body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 2, RowCount = 1
            };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, icon == MessageBoxIcon.None ? 0 : 46));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            if (icon != MessageBoxIcon.None)
            {
                var picture = new PictureBox { Image = iconBitmap, SizeMode = PictureBoxSizeMode.CenterImage, Size = new Size(34, 34) };
                body.Controls.Add(picture, 0, 0);
            }
            var message = new TextBox
            {
                Text = text, Dock = DockStyle.Fill, Multiline = true, ReadOnly = true,
                WordWrap = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.None,
                AccessibleName = "Текст сообщения", TabStop = true
            };
            body.Controls.Add(message, 1, 0);
            dialog.KeyDown += (_, e) =>
            {
                if (!e.Control || e.KeyCode != Keys.C) return;
                if (message.SelectionLength == 0) message.SelectAll();
                message.Copy();
                e.SuppressKeyPress = true;
            };
            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom, Height = 66, FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(16), WrapContents = false, Tag = "surface-alt"
            };
            var choices = buttons switch
            {
                MessageBoxButtons.YesNo => new[] { ("&Да", DialogResult.Yes), ("&Нет", DialogResult.No) },
                MessageBoxButtons.YesNoCancel => new[] { ("&Да", DialogResult.Yes), ("&Нет", DialogResult.No), ("Отмена", DialogResult.Cancel) },
                MessageBoxButtons.OKCancel => new[] { ("OK", DialogResult.OK), ("Отмена", DialogResult.Cancel) },
                MessageBoxButtons.RetryCancel => new[] { ("Повторить", DialogResult.Retry), ("Отмена", DialogResult.Cancel) },
                MessageBoxButtons.AbortRetryIgnore => new[] { ("Прервать", DialogResult.Abort), ("Повторить", DialogResult.Retry), ("Пропустить", DialogResult.Ignore) },
                _ => new[] { ("OK", DialogResult.OK) }
            };
            for (int i = choices.Length - 1; i >= 0; i--)
            {
                var (title, result) = choices[i];
                var button = new Button
                {
                    Text = title, DialogResult = result, AutoSize = true,
                    MinimumSize = new Size(96, 32), Tag = i == 0 ? "primary" : ""
                };
                actions.Controls.Add(button);
                if (i == 0) dialog.AcceptButton = button;
                if (result == DialogResult.Cancel || buttons == MessageBoxButtons.OK)
                    dialog.CancelButton = button;
            }
            dialog.Controls.Add(body);
            dialog.Controls.Add(actions);
            int measured = TextRenderer.MeasureText(text, dialog.Font, new Size(icon == MessageBoxIcon.None ? 512 : 466, 0),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl).Height;
            dialog.ClientSize = new Size(560, Math.Clamp(measured + 120, 190, 620));
            UiScaling.Apply(dialog);
            ThemeManager.Attach(dialog);
            return dialog.ShowDialog(owner);
        }
    }
}
