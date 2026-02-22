namespace logReader.UI
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            labelCANlog = new Label();
            labelDevices = new Label();
            labelResult = new Label();
            textBoxCanLog = new TextBox();
            textBoxDevices = new TextBox();
            textBoxOutput = new TextBox();
            buttonCANlog = new Button();
            buttonDevices = new Button();
            buttonOutput = new Button();
            textBoxLog = new TextBox();
            buttonProcess = new Button();
            SuspendLayout();
            // 
            // labelCANlog
            // 
            labelCANlog.AutoSize = true;
            labelCANlog.Location = new Point(12, 9);
            labelCANlog.Name = "labelCANlog";
            labelCANlog.Size = new Size(132, 15);
            labelCANlog.TabIndex = 0;
            labelCANlog.Text = "Файл CAN-логов (.csv)";
            // 
            // labelDevices
            // 
            labelDevices.AutoSize = true;
            labelDevices.Location = new Point(12, 99);
            labelDevices.Name = "labelDevices";
            labelDevices.Size = new Size(120, 15);
            labelDevices.TabIndex = 1;
            labelDevices.Text = "Файл посылок (.xlsx)";
            // 
            // labelResult
            // 
            labelResult.AutoSize = true;
            labelResult.Location = new Point(12, 182);
            labelResult.Name = "labelResult";
            labelResult.Size = new Size(74, 15);
            labelResult.TabIndex = 2;
            labelResult.Text = "Сохранить в";
            // 
            // textBoxCanLog
            // 
            textBoxCanLog.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            textBoxCanLog.Location = new Point(12, 27);
            textBoxCanLog.Name = "textBoxCanLog";
            textBoxCanLog.Size = new Size(776, 23);
            textBoxCanLog.TabIndex = 3;
            // 
            // textBoxDevices
            // 
            textBoxDevices.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            textBoxDevices.Location = new Point(12, 117);
            textBoxDevices.Name = "textBoxDevices";
            textBoxDevices.Size = new Size(776, 23);
            textBoxDevices.TabIndex = 4;
            // 
            // textBoxOutput
            // 
            textBoxOutput.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            textBoxOutput.Location = new Point(12, 200);
            textBoxOutput.Name = "textBoxOutput";
            textBoxOutput.Size = new Size(776, 23);
            textBoxOutput.TabIndex = 5;
            // 
            // buttonCANlog
            // 
            buttonCANlog.Location = new Point(12, 56);
            buttonCANlog.Name = "buttonCANlog";
            buttonCANlog.Size = new Size(75, 23);
            buttonCANlog.TabIndex = 6;
            buttonCANlog.Text = "Обзор";
            buttonCANlog.UseVisualStyleBackColor = true;
            buttonCANlog.Click += buttonCANlog_Click;
            // 
            // buttonDevices
            // 
            buttonDevices.Location = new Point(12, 146);
            buttonDevices.Name = "buttonDevices";
            buttonDevices.Size = new Size(75, 23);
            buttonDevices.TabIndex = 7;
            buttonDevices.Text = "Обзор";
            buttonDevices.UseVisualStyleBackColor = true;
            buttonDevices.Click += buttonDevices_Click;
            // 
            // buttonOutput
            // 
            buttonOutput.Location = new Point(12, 229);
            buttonOutput.Name = "buttonOutput";
            buttonOutput.Size = new Size(75, 23);
            buttonOutput.TabIndex = 8;
            buttonOutput.Text = "Обзор";
            buttonOutput.UseVisualStyleBackColor = true;
            buttonOutput.Click += buttonOutput_Click;
            // 
            // textBoxLog
            // 
            textBoxLog.Dock = DockStyle.Bottom;
            textBoxLog.Location = new Point(0, 350);
            textBoxLog.Multiline = true;
            textBoxLog.Name = "textBoxLog";
            textBoxLog.ReadOnly = true;
            textBoxLog.ScrollBars = ScrollBars.Vertical;
            textBoxLog.Size = new Size(800, 100);
            textBoxLog.TabIndex = 9;
            // 
            // buttonProcess
            // 
            buttonProcess.Location = new Point(12, 309);
            buttonProcess.Name = "buttonProcess";
            buttonProcess.Size = new Size(94, 23);
            buttonProcess.TabIndex = 10;
            buttonProcess.Text = "Обработать";
            buttonProcess.UseVisualStyleBackColor = true;
            buttonProcess.Click += buttonProcess_Click;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(buttonProcess);
            Controls.Add(textBoxLog);
            Controls.Add(buttonOutput);
            Controls.Add(buttonDevices);
            Controls.Add(buttonCANlog);
            Controls.Add(textBoxOutput);
            Controls.Add(textBoxDevices);
            Controls.Add(textBoxCanLog);
            Controls.Add(labelResult);
            Controls.Add(labelDevices);
            Controls.Add(labelCANlog);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "Form1";
            Text = "LogReader";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label labelCANlog;
        private Label labelDevices;
        private Label labelResult;
        private TextBox textBoxCanLog;
        private TextBox textBoxDevices;
        private TextBox textBoxOutput;
        private Button buttonCANlog;
        private Button buttonDevices;
        private Button buttonOutput;
        private TextBox textBoxLog;
        private Button buttonProcess;
    }
}
