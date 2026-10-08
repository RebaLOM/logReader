using System.Runtime.ExceptionServices;
using logReader.UI.Controls;

namespace logReader.UI.Tests;

public class ConfirmationRegressionTests
{
    [Fact]
    public void Generic_delete_confirmation_has_safe_default_and_window_close_returns_no() => UiThread.Run(() =>
    {
        var answer = AnswerPrompt("Удалить выбранную посылку?", "Подтверждение", MessageBoxButtons.YesNo, dialog =>
        {
            var delete = UiThread.Descendants(dialog).OfType<ModernButton>().Single(b => b.Text == "Удалить");
            Assert.Equal(ButtonVariant.Danger, delete.Variant);
            var safe = Assert.IsType<ModernButton>(dialog.AcceptButton);
            Assert.Equal(DialogResult.No, safe.DialogResult);
            Assert.True(safe.Focused);
            Assert.Same(safe, dialog.CancelButton);
            dialog.Close(); // Same cancellation path as the title-bar X.
        });
        Assert.Equal(DialogResult.No, answer);
    });

    [Theory]
    [InlineData(DialogResult.Yes)]
    [InlineData(DialogResult.No)]
    [InlineData(DialogResult.Cancel)]
    public void Save_confirmation_retains_all_three_results(DialogResult selected) => UiThread.Run(() =>
    {
        var answer = AnswerPrompt("Сохранить изменения перед закрытием?", "Несохранённые изменения",
            MessageBoxButtons.YesNoCancel, dialog =>
            {
                var buttons = UiThread.Descendants(dialog).OfType<ModernButton>().ToList();
                Assert.Equal(3, buttons.Count);
                Assert.Equal("Сохранить", buttons.Single(b => b.DialogResult == DialogResult.Yes).Text);
                Assert.Equal("Не сохранять", buttons.Single(b => b.DialogResult == DialogResult.No).Text);
                Assert.Equal("Отмена", buttons.Single(b => b.DialogResult == DialogResult.Cancel).Text);
                buttons.Single(b => b.DialogResult == selected).PerformClick();
            });
        Assert.Equal(selected, answer);
    });

    private static DialogResult AnswerPrompt(string text, string caption, MessageBoxButtons buttons, Action<Form> answer)
    {
        using var owner = new Form();
        UiThread.Show(owner);
        ExceptionDispatchInfo? failure = null;
        // A native modal loop is driven automatically. No test leaves a prompt
        // waiting for a person or throws an exception into WinForms dispatch.
        using var responder = new System.Windows.Forms.Timer { Interval = 20 };
        responder.Tick += (_, _) =>
        {
            var dialog = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f != owner && f.Text == caption);
            if (dialog == null) return;
            responder.Stop();
            try { answer(dialog); }
            catch (Exception ex)
            {
                failure = ExceptionDispatchInfo.Capture(ex);
                dialog.DialogResult = DialogResult.Cancel;
                dialog.Close();
            }
        };
        responder.Start();
        DialogResult result = AppDialog.Show(owner, text, caption, buttons, MessageBoxIcon.Warning);
        failure?.Throw();
        return result;
    }
}
