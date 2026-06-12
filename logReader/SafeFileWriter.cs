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

            // Временный файл с тем же «настоящим» расширением, иначе ClosedXML.SaveAs
            // отклоняет, например, "file.xlsx.tmp" (считает расширение .tmp).
            string ext = Path.GetExtension(path);
            if (string.IsNullOrEmpty(ext)) ext = ".xlsx";
            string tmpName = Path.GetFileNameWithoutExtension(path) + ".tmp" + ext;
            string tmpPath = string.IsNullOrEmpty(dir) ? tmpName : Path.Combine(dir, tmpName);
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
