namespace logReader.UI
{
    partial class CanLogViewForm
    {
        private System.ComponentModel.IContainer components = new System.ComponentModel.Container();

        protected override void Dispose(bool disposing)
        {
            if (disposing) components?.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(720, 640);
            MinimumSize = new Size(540, 420);
            Padding = new Padding(24);
            Name = "CanLogViewForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Посылки лога";

            panelTop = new ModernCard { Dock = DockStyle.Top, Height = 96, Padding = new Padding(16) };
            textBoxSearch = new ModernTextBox
            {
                Name = "textBoxSearch", TabIndex = 0,
                PlaceholderText = "Введите ID посылки, например 0CFF0008"
            };
            textBoxSearch.TextChanged += textBoxSearch_TextChanged;
            var searchField = UiFactory.Field("ПОИСК CAN ID", textBoxSearch);
            labelSearch = searchField.Controls.OfType<Label>().First();
            panelTop.Controls.Add(searchField);

            scrollPanel = new ModernCard { Dock = DockStyle.Fill, Padding = new Padding(12), Name = "scrollPanel" };
            dataGridPackets = new DataGridView
            {
                Dock = DockStyle.Fill, Name = "dataGridPackets", ReadOnly = true,
                AllowUserToAddRows = false, AllowUserToDeleteRows = false,
                RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
                TabIndex = 1
            };
            dataGridPackets.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "CanId", HeaderText = "ID ПОСЫЛКИ", FillWeight = 65,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
            dataGridPackets.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Count", HeaderText = "КОЛИЧЕСТВО", FillWeight = 35,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
            emptyState = new EmptyState
            {
                Dock = DockStyle.Fill, Icon = IconKind.Signal,
                Title = "Посылки не найдены", Description = "В выбранных файлах нет распознанных CAN-посылок.", Visible = false
            };
            scrollPanel.Controls.Add(dataGridPackets);
            scrollPanel.Controls.Add(emptyState);

            labelCount = new Label
            {
                Dock = DockStyle.Bottom, Height = 38, TextAlign = ContentAlignment.MiddleLeft,
                Font = Typography.Secondary, ForeColor = AppTheme.TextSecondary,
                Name = "labelCount", AutoEllipsis = true
            };
            var gap = new Panel { Dock = DockStyle.Top, Height = 16, BackColor = AppTheme.Background };
            var header = UiFactory.Header("Посылки лога", "CAN ID и количество кадров в выбранном файле или папке", IconKind.Signal);
            header.Dock = DockStyle.Top;
            Controls.Add(scrollPanel);
            Controls.Add(labelCount);
            Controls.Add(gap);
            Controls.Add(panelTop);
            Controls.Add(header);
            ResumeLayout(true);
        }

        private Panel panelTop = null!;
        private Label labelSearch = null!;
        private TextBox textBoxSearch = null!;
        private Label labelCount = null!;
        private Panel scrollPanel = null!;
        private DataGridView dataGridPackets = null!;
        private EmptyState emptyState = null!;
    }
}
