namespace logReader.UI.Controls;

/// <summary>Consistent, keyboard-accessible modal confirmation and error messages.</summary>
internal static class AppDialog
{
    public static DialogResult Show(string text, string caption,
        MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxIcon icon = MessageBoxIcon.None,
        MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1)
        => Show(Form.ActiveForm, text, caption, buttons, icon, defaultButton);

    public static DialogResult Show(IWin32Window? owner, string text, string caption,
        MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxIcon icon = MessageBoxIcon.None,
        MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1)
    {
        bool destructive = caption.Contains("удален", StringComparison.OrdinalIgnoreCase)
            || caption.Contains("удалён", StringComparison.OrdinalIgnoreCase)
            || text.TrimStart().StartsWith("Удалить", StringComparison.OrdinalIgnoreCase);
        bool savePrompt = buttons == MessageBoxButtons.YesNoCancel;
        StatusTone tone = icon switch
        {
            MessageBoxIcon.Error => StatusTone.Error,
            MessageBoxIcon.Warning => StatusTone.Warning,
            _ => StatusTone.Info
        };
        using var dialog = new Form();
        dialog.SuspendLayout();
        dialog.Text = caption;
        dialog.Font = Typography.Body;
        dialog.BackColor = AppTheme.Background;
        dialog.StartPosition = FormStartPosition.CenterParent;
        dialog.ShowInTaskbar = false;
        dialog.MinimizeBox = false;
        dialog.MaximizeBox = false;
        dialog.FormBorderStyle = FormBorderStyle.Sizable;
        dialog.AutoScaleDimensions = new SizeF(96, 96);
        dialog.AutoScaleMode = AutoScaleMode.Dpi;
        dialog.ClientSize = new Size(620, 340);
        dialog.MinimumSize = new Size(440, 280);
        dialog.Icon = (owner as Form)?.Icon ?? Form.ActiveForm?.Icon;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(24) };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var header = UiFactory.Header(caption,
            destructive ? "Это действие изменит выбранные данные." : savePrompt ? "Выберите, что сделать с несохранёнными изменениями." : "",
            tone is StatusTone.Error or StatusTone.Warning ? IconKind.Warning : IconKind.Info);
        foreach (var mark in header.Controls.OfType<IconView>())
            mark.ForeColor = destructive || tone == StatusTone.Error ? AppTheme.Error : tone == StatusTone.Warning ? AppTheme.Warning : AppTheme.Info;
        root.Controls.Add(header, 0, 0);
        var message = new RichTextBox
        {
            Text = text, ReadOnly = true, BorderStyle = BorderStyle.None,
            BackColor = AppTheme.Background, ForeColor = AppTheme.TextPrimary,
            Font = Typography.Body, Dock = DockStyle.Fill, DetectUrls = false,
            ScrollBars = RichTextBoxScrollBars.Vertical, TabStop = false,
            AccessibleName = text, Margin = new Padding(0, 0, 0, 20)
        };
        root.Controls.Add(message, 0, 1);

        var choices = buttons switch
        {
            MessageBoxButtons.OKCancel => new[] { DialogResult.OK, DialogResult.Cancel },
            MessageBoxButtons.YesNo => new[] { DialogResult.Yes, DialogResult.No },
            MessageBoxButtons.YesNoCancel => new[] { DialogResult.Yes, DialogResult.No, DialogResult.Cancel },
            MessageBoxButtons.RetryCancel => new[] { DialogResult.Retry, DialogResult.Cancel },
            MessageBoxButtons.AbortRetryIgnore => new[] { DialogResult.Abort, DialogResult.Retry, DialogResult.Ignore },
            MessageBoxButtons.CancelTryContinue => new[] { DialogResult.Cancel, DialogResult.TryAgain, DialogResult.Continue },
            _ => new[] { DialogResult.OK }
        };
        var actions = new List<ModernButton>();
        foreach (var result in choices)
        {
            bool primary = result is DialogResult.OK or DialogResult.Yes or DialogResult.Retry or DialogResult.TryAgain;
            string label = result switch
            {
                DialogResult.Yes => destructive ? "Удалить" : savePrompt ? "Сохранить" : "Да",
                DialogResult.No => savePrompt ? "Не сохранять" : "Нет",
                DialogResult.Cancel => "Отмена",
                DialogResult.Retry or DialogResult.TryAgain => "Повторить",
                DialogResult.Abort => "Прервать",
                DialogResult.Ignore => "Пропустить",
                DialogResult.Continue => "Продолжить",
                _ => "Понятно"
            };
            var button = new ModernButton
            {
                Text = label, DialogResult = result, AutoSize = true,
                MinimumSize = new Size(104, 36),
                Variant = destructive && result == DialogResult.Yes ? ButtonVariant.Danger : primary ? ButtonVariant.Primary : ButtonVariant.Secondary,
                Icon = destructive && result == DialogResult.Yes ? IconKind.Trash : result is DialogResult.OK or DialogResult.Yes ? IconKind.Check : IconKind.None
            };
            actions.Add(button);
        }
        // Secondary actions sit to the left, with the default action at the right edge.
        var footer = UiFactory.Footer(actions.Cast<Control>().ToArray());
        footer.Padding = new Padding(0, 16, 0, 0);
        footer.BackColor = AppTheme.Background;
        root.Controls.Add(footer, 0, 2);
        dialog.Controls.Add(root);
        int defaultIndex = Math.Clamp((int)defaultButton / 256, 0, actions.Count - 1);
        if (destructive && defaultButton == MessageBoxDefaultButton.Button1 && choices.Contains(DialogResult.No))
            defaultIndex = Array.IndexOf(choices, DialogResult.No);
        dialog.AcceptButton = actions[defaultIndex];
        var cancel = actions.FirstOrDefault(action => action.DialogResult == DialogResult.Cancel)
            ?? actions.FirstOrDefault(action => action.DialogResult == DialogResult.No) ?? actions[0];
        dialog.CancelButton = cancel;
        dialog.Shown += (_, _) => actions[defaultIndex].Focus();
        dialog.ResumeLayout(true);
        AppTheme.Apply(dialog);
        message.BackColor = AppTheme.Background;
        var answer = owner == null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
        // Closing a two-choice prompt must never imply an affirmative action.
        return answer == DialogResult.Cancel && !choices.Contains(DialogResult.Cancel) ? cancel.DialogResult : answer;
    }
}
