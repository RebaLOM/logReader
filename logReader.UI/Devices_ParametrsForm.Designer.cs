namespace logReader.UI
{
    partial class Devices_ParametrsForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            dataGridView = new DataGridView();
            panelButtons = new Panel();
            buttonDisableAll = new Button();
            buttonEnableAll = new Button();
            colDeviceEnabled = new DataGridViewCheckBoxColumn();
            colDeviceId = new DataGridViewTextBoxColumn();
            colParamEnabled = new DataGridViewCheckBoxColumn();
            colParamName = new DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)dataGridView).BeginInit();
            panelButtons.SuspendLayout();
            SuspendLayout();
            // 
            // dataGridView
            // 
            dataGridView.AllowUserToAddRows = false;
            dataGridView.AllowUserToDeleteRows = false;
            dataGridView.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView.Columns.AddRange(new DataGridViewColumn[] { colDeviceEnabled, colDeviceId, colParamEnabled, colParamName });
            dataGridView.Dock = DockStyle.Fill;
            dataGridView.Location = new Point(0, 0);
            dataGridView.Name = "dataGridView";
            dataGridView.RowHeadersVisible = false;
            dataGridView.Size = new Size(484, 361);
            dataGridView.TabIndex = 0;
            dataGridView.CellContentClick += dataGridView_CellContentClick;
            dataGridView.CellValueChanged += dataGridView_CellValueChanged;
            dataGridView.CurrentCellDirtyStateChanged += dataGridView_CurrentCellDirtyStateChanged;
            // 
            // panelButtons
            // 
            panelButtons.Controls.Add(buttonDisableAll);
            panelButtons.Controls.Add(buttonEnableAll);
            panelButtons.Dock = DockStyle.Bottom;
            panelButtons.Location = new Point(0, 361);
            panelButtons.Name = "panelButtons";
            panelButtons.Size = new Size(484, 50);
            panelButtons.TabIndex = 1;
            // 
            // buttonDisableAll
            // 
            buttonDisableAll.Location = new Point(118, 12);
            buttonDisableAll.Name = "buttonDisableAll";
            buttonDisableAll.Size = new Size(120, 28);
            buttonDisableAll.TabIndex = 1;
            buttonDisableAll.Text = "Выключить все";
            buttonDisableAll.UseVisualStyleBackColor = true;
            buttonDisableAll.Click += buttonDisableAll_Click;
            // 
            // buttonEnableAll
            // 
            buttonEnableAll.Location = new Point(12, 12);
            buttonEnableAll.Name = "buttonEnableAll";
            buttonEnableAll.Size = new Size(100, 28);
            buttonEnableAll.TabIndex = 0;
            buttonEnableAll.Text = "Включить все";
            buttonEnableAll.UseVisualStyleBackColor = true;
            buttonEnableAll.Click += buttonEnableAll_Click;
            // 
            // colDeviceEnabled
            // 
            colDeviceEnabled.HeaderText = "Статус устр.";
            colDeviceEnabled.Name = "colDeviceEnabled";
            // 
            // colDeviceId
            // 
            colDeviceId.HeaderText = "ID устройства";
            colDeviceId.Name = "colDeviceId";
            colDeviceId.ReadOnly = true;
            colDeviceId.Width = 110;
            // 
            // colParamEnabled
            // 
            colParamEnabled.HeaderText = "Статус парам.";
            colParamEnabled.Name = "colParamEnabled";
            // 
            // colParamName
            // 
            colParamName.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            colParamName.HeaderText = "Параметр";
            colParamName.Name = "colParamName";
            colParamName.ReadOnly = true;
            // 
            // Devices_ParametrsForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(484, 411);
            Controls.Add(dataGridView);
            Controls.Add(panelButtons);
            Name = "Devices_ParametrsForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Устройства и параметры";
            FormClosing += Devices_ParametrsForm_FormClosing;
            ((System.ComponentModel.ISupportInitialize)dataGridView).EndInit();
            panelButtons.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private DataGridView dataGridView;
        private Panel panelButtons;
        private Button buttonDisableAll;
        private Button buttonEnableAll;
        private DataGridViewCheckBoxColumn colDeviceEnabled;
        private DataGridViewTextBoxColumn colDeviceId;
        private DataGridViewCheckBoxColumn colParamEnabled;
        private DataGridViewTextBoxColumn colParamName;
    }
}
