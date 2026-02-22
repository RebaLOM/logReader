namespace logReader.UI
{
    partial class HelpForm
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
            richTextBoxHelp = new RichTextBox();
            SuspendLayout();
            // 
            // richTextBoxHelp
            // 
            richTextBoxHelp.Dock = DockStyle.Fill;
            richTextBoxHelp.Location = new Point(0, 0);
            richTextBoxHelp.Name = "richTextBoxHelp";
            richTextBoxHelp.ReadOnly = true;
            richTextBoxHelp.Size = new Size(766, 464);
            richTextBoxHelp.TabIndex = 0;
            richTextBoxHelp.Text = "";
            richTextBoxHelp.TextChanged += richTextBoxHelp_TextChanged;
            // 
            // HelpForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(766, 464);
            Controls.Add(richTextBoxHelp);
            Name = "HelpForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Помощь";
            ResumeLayout(false);
        }

        #endregion

        private RichTextBox richTextBoxHelp;
    }
}
