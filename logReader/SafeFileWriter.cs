namespace logReader
{
    /// <summary>
    /// Атомарная запись: пишем во временный файл, затем File.Replace на целевой
    /// с созданием резервной копии *.bak. При сбое — оригинал остаётся неповреждённым.
    /// </summary>
    public static class SafeFileWriter
    {
        public static void Write(string path, Action<string> writeToPath)
        {
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string tmpPath = path + ".tmp";
            string bakPath = path + ".bak";

            if (File.Exists(tmpPath))
            {
                try { File.Delete(tmpPath); } catch { /* перезапишем */ }
            }

            writeToPath(tmpPath);

            if (!File.Exists(path))
            {
                File.Move(tmpPath, path);
                return;
            }

            File.Replace(tmpPath, path, bakPath, ignoreMetadataErrors: true);
        }
    }
}
