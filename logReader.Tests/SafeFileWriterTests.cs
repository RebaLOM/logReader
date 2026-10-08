namespace logReader.Tests;

public sealed class SafeFileWriterTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("safe-writer-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Temp_path_for_logs_does_not_look_like_an_input_file()
    {
        string tmp = SafeFileWriter.CreateTempPath(Path.Combine(_dir, "result.csv"));

        Assert.EndsWith(".tmp", tmp);
        Assert.NotEqual(tmp, SafeFileWriter.CreateTempPath(Path.Combine(_dir, "result.csv")));
    }

    [Fact]
    public void Temp_path_for_xlsx_keeps_extension_required_by_closedxml()
    {
        Assert.EndsWith(".tmp.xlsx", SafeFileWriter.CreateTempPath(Path.Combine(_dir, "devices.xlsx")));
    }

    [Fact]
    public void Failed_write_keeps_original_and_removes_temp()
    {
        string path = Path.Combine(_dir, "devices.dbc");
        File.WriteAllText(path, "original");

        Assert.Throws<InvalidOperationException>(() => SafeFileWriter.Write(path, tmp =>
        {
            File.WriteAllText(tmp, "partial");
            throw new InvalidOperationException("boom");
        }));

        Assert.Equal("original", File.ReadAllText(path));
        Assert.Single(Directory.GetFiles(_dir));
    }

    [Fact]
    public void Backup_is_created_only_when_requested()
    {
        string path = Path.Combine(_dir, "a.csv");
        File.WriteAllText(path, "v1");

        SafeFileWriter.Write(path, tmp => File.WriteAllText(tmp, "v2"));
        Assert.False(File.Exists(path + ".bak"));

        SafeFileWriter.Write(path, tmp => File.WriteAllText(tmp, "v3"), keepBackup: true);
        Assert.Equal("v3", File.ReadAllText(path));
        Assert.Equal("v2", File.ReadAllText(path + ".bak"));
    }
}
