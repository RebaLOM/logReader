namespace logReader.UI
{
    public partial class HelpForm : Form
    {
        public HelpForm()
        {
            InitializeComponent();
            
            // Иконка в заголовке формы «Помощь» из встроенных ресурсов
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var iconName = assembly.GetManifestResourceNames().FirstOrDefault(n => n.Contains("CAN_reader.ico"));
                if (iconName != null)
                {
                    using (var stream = assembly.GetManifestResourceStream(iconName))
                    {
                        if (stream != null)
                            Icon = new Icon(stream);
                    }
                }
            }
            catch { }
            
            // Загрузка RTF из встроенных ресурсов
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var rtfName = assembly.GetManifestResourceNames().FirstOrDefault(n => n.Contains("Help.rtf"));
                if (rtfName != null)
                {
                    using (var stream = assembly.GetManifestResourceStream(rtfName))
                    {
                        if (stream != null)
                        {
                            // ReadOnly = true мешает отображению картинок в RTF — снимаем на время загрузки
                            richTextBoxHelp.ReadOnly = false;
                            richTextBoxHelp.LoadFile(stream, RichTextBoxStreamType.RichText);
                            richTextBoxHelp.ReadOnly = true;
                        }
                        else
                        {
                            richTextBoxHelp.Text = "Файл справки не найден во встроенных ресурсах.";
                        }
                    }
                }
                else
                {
                    richTextBoxHelp.Text = "Файл справки не найден во встроенных ресурсах.";
                }
            }
            catch (Exception ex)
            {
                richTextBoxHelp.Text = "Не удалось загрузить файл справки: " + ex.Message;
                richTextBoxHelp.ReadOnly = true;
            }
        }

        private void richTextBoxHelp_TextChanged(object sender, EventArgs e)
        {

        }
    }
}
