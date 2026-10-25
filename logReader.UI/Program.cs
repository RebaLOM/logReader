namespace logReader.UI
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            ThemeManager.Initialize();
            Application.Run(new MainForm());
        }
    }
}
