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
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(380, 190);

            Icon = Application.OpenForms.OfType<MainForm>().FirstOrDefault()?.Icon;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16),
                RowCount = 3,
                ColumnCount = 2
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            var header = new Label
            {
                Text = "Выберите тип создаваемого файла:",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 0, 0, 12)
            };
            root.Controls.Add(header, 0, 0);
            root.SetColumnSpan(header, 2);

            var btnXlsx = new Button
            {
                Text = "Excel (.xlsx)",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 8, 0),
                Height = 44
            };
            btnXlsx.Click += (_, _) =>
            {
                SelectedKind = FileKind.Xlsx;
                DialogResult = DialogResult.OK;
                Close();
            };
            root.Controls.Add(btnXlsx, 0, 1);

            var btnDbc = new Button
            {
                Text = "DBC (.dbc)",
                Dock = DockStyle.Fill,
                Margin = new Padding(8, 0, 0, 0),
                Height = 44
            };
            btnDbc.Click += (_, _) =>
            {
                SelectedKind = FileKind.Dbc;
                DialogResult = DialogResult.OK;
                Close();
            };
            root.Controls.Add(btnDbc, 1, 1);

            var bottom = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Margin = new Padding(0, 12, 0, 0),
                AutoSize = true
            };
            var btnCancel = new Button
            {
                Text = "Отмена",
                AutoSize = true,
                MinimumSize = new Size(80, 28),
                DialogResult = DialogResult.Cancel
            };
            bottom.Controls.Add(btnCancel);
            root.Controls.Add(bottom, 0, 2);
            root.SetColumnSpan(bottom, 2);

            Controls.Add(root);
            CancelButton = btnCancel;
        }
    }
}
