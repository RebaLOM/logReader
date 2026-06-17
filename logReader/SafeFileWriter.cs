namespace logReader
{
    // Атомарная запись: tmp → Replace с .bak, чтобы сбой не оставил битый целевой файл.
    public static class SafeFileWriter
    {
        public static string CreateTempPath(string path)
        {
            string? dir = Path.GetDirectoryName(path);
            string ext = Path.GetExtension(path);
            if (string.IsNullOrEmpty(ext)) ext = ".xlsx";
            string tmpName = Path.GetFileNameWithoutExtension(path) + ".tmp" + ext;
            return string.IsNullOrEmpty(dir) ? tmpName : Path.Combine(dir, tmpName);
        }

        public static void Write(string path, Action<string> writeToPath)
        {
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string tmpPath = CreateTempPath(path);
            string bakPath = path + ".bak";

            if (File.Exists(tmpPath))
            {
                try { File.Delete(tmpPath); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }

            writeToPath(tmpPath);
            Publish(tmpPath, path, bakPath);
        }

        public static void Publish(string tempPath, string destinationPath)
            => Publish(tempPath, destinationPath, destinationPath + ".bak");

        public static void Publish(string tempPath, string destinationPath, string backupPath)
        {
            string? dir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (!File.Exists(destinationPath))
            {
                File.Move(tempPath, destinationPath);
                return;
            }

            File.Replace(tempPath, destinationPath, backupPath, ignoreMetadataErrors: true);
        }
    }
}
