namespace logReader.UI;

internal sealed class DisplaySettingsForm : Form
{
    internal DisplaySettings SelectedSettings { get; private set; }

    internal DisplaySettingsForm(DisplaySettings current)
    {
        ArgumentNullException.ThrowIfNull(current);
        SelectedSettings = current.Normalized();

        Text = "Настройки отображения";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(16);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 4,
            Margin = Padding.Empty,
            MinimumSize = new Size(560, 0)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int row = 0; row < root.RowCount; row++)
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(new Label
        {
            Text = "Формат кода DTC:",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 6)
        }, 0, 0);

        var format = new ComboBox
        {
            Name = "dtcDisplayFormat",
            AccessibleName = "Формат отображения DTC",
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Top,
            MinimumSize = new Size(560, 0),
            Margin = new Padding(0, 0, 0, 8)
        };
        format.Items.AddRange(["12345.0", "SPN 12345 / FMI 0"]);
        format.SelectedIndex = SelectedSettings.DtcFormat == DtcDisplayFormat.Detailed ? 1 : 0;
        root.Controls.Add(format, 0, 1);

        root.Controls.Add(new Label
        {
            Text = "До точки указан SPN, после — FMI. Выбранный вид используется в диагностике и в результатах CSV/XLSX.",
            AutoSize = true,
            MaximumSize = new Size(560, 0),
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 0, 0, 18)
        }, 0, 2);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = Padding.Empty
        };
        var cancel = new Button
        {
            Name = "displaySettingsCancel",
            Text = "Отмена",
            TabIndex = 1,
            AutoSize = true,
            MinimumSize = new Size(90, 28),
            DialogResult = DialogResult.Cancel,
            Margin = Padding.Empty
        };
        var ok = new Button
        {
            Name = "displaySettingsOk",
            Text = "OK",
            TabIndex = 0,
            AutoSize = true,
            MinimumSize = new Size(90, 28),
            DialogResult = DialogResult.OK,
            Margin = new Padding(0, 0, 8, 0)
        };
        ok.Click += (_, _) => SelectedSettings = SelectedSettings with
        {
            DtcFormat = format.SelectedIndex == 1 ? DtcDisplayFormat.Detailed : DtcDisplayFormat.Compact
        };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);
        root.Controls.Add(buttons, 0, 3);

        Controls.Add(root);
        AcceptButton = ok;
        CancelButton = cancel;
        UiScaling.Apply(this);
    }
}
