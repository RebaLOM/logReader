namespace logReader.Tests;

internal static class UserFixtureFiles
{
    public static string? Root => Environment.GetEnvironmentVariable("LOGER_TEST_DATA_ROOT");

    public static IEnumerable<string> Enumerate(string root, string extension)
    {
        foreach (string file in Directory.EnumerateFiles(root))
            if (Path.GetExtension(file).Equals(extension, StringComparison.OrdinalIgnoreCase))
                yield return file;
        foreach (string directory in Directory.EnumerateDirectories(root))
        {
            if (new DirectoryInfo(directory).LinkTarget != null
                || Path.GetFileName(directory).Equals("pub", StringComparison.OrdinalIgnoreCase)) continue;
            foreach (string file in Enumerate(directory, extension)) yield return file;
        }
    }
}
