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
            using var fs = File.OpenRead(path);

            // BOM надёжнее эвристики по содержимому.
            if (fs.Length >= 3)
            {
                Span<byte> bom = stackalloc byte[3];
                if (fs.Read(bom) == 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
                    return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
                fs.Position = 0;
            }

            // Достаточно первых 64 KB — не читаем многомегабайтные логи целиком.
            const int sampleSize = 64 * 1024;
            byte[] buffer = new byte[sampleSize];
            int total = 0;
            while (total < sampleSize)
            {
                int n = fs.Read(buffer, total, sampleSize - total);
                if (n <= 0) break;
                total += n;
            }

            // На границе выборки UTF-8-символ может быть обрезан — flush:false не считает это ошибкой.
            bool reachedEof = total < sampleSize || fs.Position >= fs.Length;
            try
            {
                Utf8Strict.GetDecoder().GetCharCount(buffer, 0, total, flush: reachedEof);
                return Encoding.UTF8;
            }
            catch (DecoderFallbackException)
            {
                return Encoding.GetEncoding(1251);
            }
        }
    }
}
