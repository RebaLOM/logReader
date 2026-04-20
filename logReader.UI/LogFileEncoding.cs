using System.Text;

namespace logReader.UI
{
    internal static class LogFileEncoding
    {
        private static readonly UTF8Encoding Utf8Strict = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        static LogFileEncoding()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public static Encoding Detect(string path)
        {
            // BOM приоритетнее эвристики
            using (var fs = File.OpenRead(path))
            {
                if (fs.Length >= 3)
                {
                    Span<byte> bom = stackalloc byte[3];
                    _ = fs.Read(bom);
                    if (bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
                        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
                }
            }

            // Проверяем только первые 64 KB файла — этого достаточно, чтобы поймать
            // некорректную UTF-8 кодировку, и не нужно читать логи в сотни МБ целиком.
            try
            {
                const int sampleSize = 64 * 1024;
                using var fs = File.OpenRead(path);
                using var sr = new StreamReader(fs, Utf8Strict);
                byte[] buffer = new byte[sampleSize];
                int total = 0;
                while (total < sampleSize)
                {
                    int n = fs.Read(buffer, 0, Math.Min(buffer.Length - total, sampleSize - total));
                    if (n <= 0) break;
                    total += n;
                }

                Utf8Strict.GetCharCount(buffer, 0, total);
                return Encoding.UTF8;
            }
            catch (DecoderFallbackException)
            {
                return Encoding.GetEncoding(1251);
            }
        }
    }
}

