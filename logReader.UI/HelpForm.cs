using System.Linq;
using System.Reflection;

namespace logReader.UI
{
    public partial class HelpForm : Form
    {
        public HelpForm()
        {
            InitializeComponent();
            Icon = Application.OpenForms.OfType<MainForm>().FirstOrDefault()?.Icon;

            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var rtfName = assembly.GetManifestResourceNames()
                                       .FirstOrDefault(n => n.Contains("Help.rtf"));
                if (rtfName != null)
                {
                    using var stream = assembly.GetManifestResourceStream(rtfName);
                    if (stream != null)
                    {
                        richTextBoxHelp.ReadOnly = false;
                        richTextBoxHelp.LoadFile(stream, RichTextBoxStreamType.RichText);
                        richTextBoxHelp.ReadOnly = true;
                        return;
                    }
                }
                richTextBoxHelp.Text = "Файл справки не найден во встроенных ресурсах.";
            }
            catch (Exception ex)
            {
                richTextBoxHelp.Text = "Не удалось загрузить файл справки: " + ex.Message;
                richTextBoxHelp.ReadOnly = true;
            }
        }

        private void richTextBoxHelp_TextChanged(object sender, EventArgs e) { }
    }
}