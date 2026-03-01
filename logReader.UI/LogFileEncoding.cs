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

            // Проверяем, что файл корректно декодируется как UTF-8.
            try
            {
                using var sr = new StreamReader(path, Utf8Strict);
                while (sr.ReadLine() != null) { }
                return Encoding.UTF8;
            }
            catch (DecoderFallbackException)
            {
                // Частый legacy-кейс для русских логов
                return Encoding.GetEncoding(1251);
            }
        }
    }
}

