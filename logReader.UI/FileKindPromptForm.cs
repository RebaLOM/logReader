using System.Linq;

namespace logReader.UI
{
    internal sealed class FileKindPromptForm : Form
    {
        public enum FileKind { None, Xlsx, Dbc }

        public FileKind SelectedKind { get; private set; } = FileKind.None;

        public FileKindPromptForm()
        {
            Text = "Новый файл посылок";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(360, 170);

            Icon = Application.OpenForms.OfType<MainForm>().FirstOrDefault()?.Icon;

            var label = new Label
            {
                Text = "Выберите тип создаваемого файла:",
                Location = new Point(20, 20),
                AutoSize = true
            };
            Controls.Add(label);

            var btnXlsx = new Button
            {
                Text = "Excel (.xlsx)",
                Location = new Point(20, 55),
                Size = new Size(150, 40)
            };
            btnXlsx.Click += (_, _) => { SelectedKind = FileKind.Xlsx; DialogResult = DialogResult.OK; Close(); };
            Controls.Add(btnXlsx);

            var btnDbc = new Button
            {
                Text = "DBC (.dbc)",
                Location = new Point(190, 55),
                Size = new Size(150, 40)
            };
            btnDbc.Click += (_, _) => { SelectedKind = FileKind.Dbc; DialogResult = DialogResult.OK; Close(); };
            Controls.Add(btnDbc);

            var btnCancel = new Button
            {
                Text = "Отмена",
                Location = new Point(265, 120),
                Size = new Size(75, 28),
                DialogResult = DialogResult.Cancel
            };
            Controls.Add(btnCancel);

            CancelButton = btnCancel;
        }
    }
}
