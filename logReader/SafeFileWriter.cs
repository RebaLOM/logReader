namespace logReader
{
    // Атомарная запись: tmp в той же папке → Move/Replace, чтобы сбой не оставил битый целевой файл.
    public static class SafeFileWriter
    {
        // ClosedXML определяет формат по расширению, поэтому для .xlsx оно сохраняется в конце имени;
        // для остальных — «.tmp», чтобы временный файл не подхватывался как входной лог (*.csv, *.asc).
        public static string CreateTempPath(string path)
        {
            string? dir = Path.GetDirectoryName(path);
            string name = Path.GetFileName(path);
            string ext = Path.GetExtension(path);
            string suffix = ".~" + Guid.NewGuid().ToString("N")[..8] + ".tmp";
            if (ext.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                suffix += ext;
            string tmpName = name + suffix;
            return string.IsNullOrEmpty(dir) ? tmpName : Path.Combine(dir, tmpName);
        }

        public static void Write(string path, Action<string> writeToPath, bool keepBackup = false)
        {
            EnsureDirectory(path);
            string tmpPath = CreateTempPath(path);
            try
            {
                writeToPath(tmpPath);
                Publish(tmpPath, path, keepBackup);
            }
            finally
            {
                TryDelete(tmpPath);
            }
        }

        public static void Publish(string tempPath, string destinationPath, bool keepBackup = false)
        {
            EnsureDirectory(destinationPath);

            if (!File.Exists(destinationPath))
            {
                File.Move(tempPath, destinationPath);
                return;
            }

            string? backupPath = keepBackup ? destinationPath + ".bak" : null;
            File.Replace(tempPath, destinationPath, backupPath, ignoreMetadataErrors: true);
        }

        public static void TryDelete(string? path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            try { File.Delete(path); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }

        private static void EnsureDirectory(string path)
        {
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
    }
}
