using logReader.UI.Theme;

namespace logReader.UI
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            AntdThemeBridge.EnsureConfigured();
            AppTheme.InitializeFromPreferences();
            Application.Run(new MainForm());
        }
    }
}
