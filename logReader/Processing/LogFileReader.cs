using System.Text;

namespace logReader.Processing
{
    // Построчное чтение лога с прогрессом и отменой. FileShare.ReadWrite: лог, который ещё
    // пишет PCAN-View/CANoe, читается, а не обрывает пакетную обработку ошибкой доступа.
    internal static class LogFileReader
    {
        private const int CheckEveryLines = 4096;

        public static FileStream OpenShared(string path)
            => new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 1 << 16, FileOptions.SequentialScan);

        public static IEnumerable<string> ReadLines(string path, Encoding encoding, ProcessingContext? context = null)
        {
            using var stream = OpenShared(path);
            using var reader = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: true);
            double length = Math.Max(1, stream.Length);
            int count = 0;
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (context != null && ++count % CheckEveryLines == 0)
                {
                    context.ThrowIfCancellationRequested();
                    context.ReportProgress(stream.Position / length);
                }
                yield return line;
            }
            context?.ReportProgress(1);
        }
    }
}
