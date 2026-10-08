namespace logReader.UI
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            AppTheme.Initialize();
            Application.Run(new MainForm());
        }
    }
}
