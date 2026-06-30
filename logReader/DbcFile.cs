using System.Globalization;
using System.Text;

namespace logReader
{
    public static class DbcFile
    {
        private const string DefaultReceiver = "Vector__XXX";
        private const string DefaultTransmitter = "Vector__XXX";

        public static List<DbcMessage> Read(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"Файл не найден: {path}");

            var messages = new List<DbcMessage>();
            DbcMessage? current = null;

            foreach (string raw in File.ReadLines(path))
            {
                string line = raw.Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;

                if (DbcLineParser.TryParseMessage(line, out var header))
                {
                    current = new DbcMessage
                    {
                        Id = header.Id,
                        IsExtended = header.IsExtended,
                        Name = header.Name,
                        Dlc = header.Dlc,
                        Transmitter = header.Transmitter
                    };
                    messages.Add(current);
                    continue;
                }

                if (current == null || !line.StartsWith("SG_", StringComparison.Ordinal))
                    continue;

                if (DbcLineParser.TryParseSignal(line, out var sig))
                    current.Signals.Add(sig);
            }

            return messages;
        }

        public static void Write(string path, IReadOnlyList<DbcMessage> messages)
        {
            ValidateMessages(messages);

            SafeFileWriter.Write(path, tmp =>
            {
                var sb = new StringBuilder();
                sb.Append("VERSION \"\"\n");
                sb.Append('\n');
                sb.Append('\n');
                sb.Append("BS_:\n");
                sb.Append('\n');
                sb.Append("BU_:\n");
                sb.Append('\n');
                sb.Append('\n');

                foreach (var m in messages)
                {
                    uint id = m.Id & DbcLineParser.IdMask;
                    if (m.IsExtended) id |= DbcLineParser.ExtendedIdFlag;

                    string transmitter = string.IsNullOrWhiteSpace(m.Transmitter)
                        ? DefaultTransmitter
                        : m.Transmitter;
                    string messageName = string.IsNullOrWhiteSpace(m.Name)
                        ? ("Msg_" + m.Id.ToString("X", CultureInfo.InvariantCulture))
                        : m.Name;

                    sb.Append("BO_ ")
                      .Append(id.ToString(CultureInfo.InvariantCulture))
                      .Append(' ')
                      .Append(messageName)
                      .Append(": ")
                      .Append(m.Dlc.ToString(CultureInfo.InvariantCulture))
                      .Append(' ')
                      .Append(transmitter)
                      .Append('\n');

                    foreach (var s in m.Signals)
                    {
                        string receiver = string.IsNullOrWhiteSpace(s.Receiver) ? DefaultReceiver : s.Receiver;

                        sb.Append(" SG_ ")
                          .Append(s.Name)
                          .Append(" : ")
                          .Append(s.StartBit.ToString(CultureInfo.InvariantCulture))
                          .Append('|')
                          .Append(s.Length.ToString(CultureInfo.InvariantCulture))
                          .Append('@')
                          .Append(s.IsLittleEndian ? '1' : '0')
                          .Append(s.IsSigned ? '-' : '+')
                          .Append(" (")
                          .Append(DbcPhysicalValue.FormatForDbc(s.Factor))
                          .Append(',')
                          .Append(DbcPhysicalValue.FormatForDbc(s.Offset))
                          .Append(") [")
                          .Append(DbcPhysicalValue.FormatForDbc(s.Min))
                          .Append('|')
                          .Append(DbcPhysicalValue.FormatForDbc(s.Max))
                          .Append("] \"")
                          .Append(EscapeQuotes(s.Unit ?? ""))
                          .Append("\" ")
                          .Append(receiver)
                          .Append('\n');
                    }

                    sb.Append('\n');
                }

                var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);
                File.WriteAllText(tmp, sb.ToString(), utf8NoBom);
            });
        }

        public static void CreateEmpty(string path)
        {
            Write(path, Array.Empty<DbcMessage>());
        }

        internal static void ValidateMessages(IReadOnlyList<DbcMessage> messages)
        {
            if (messages == null) throw new ArgumentNullException(nameof(messages));

            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenIds = new HashSet<(uint id, bool ext)>();

            foreach (var m in messages)
            {
                if (!DbcLineParser.IsValidSymbolName(m.Name))
                    throw new InvalidDataException(
                        $"Недопустимое имя посылки '{m.Name}'. {DbcLineParser.SymbolNameRulesHint}");

                if (!seenNames.Add(m.Name))
                    throw new InvalidDataException($"Повторяющееся имя посылки: '{m.Name}'.");

                var key = (m.Id, m.IsExtended);
                if (!seenIds.Add(key))
                    throw new InvalidDataException(
                        $"Повторяющийся ID 0x{m.Id:X} ({(m.IsExtended ? "Extended" : "Standard")}).");

                var sigNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var s in m.Signals)
                {
                    if (!DbcLineParser.IsValidSymbolName(s.Name))
                        throw new InvalidDataException(
                            $"Сигнал '{s.Name}' в посылке '{m.Name}': недопустимое имя.");
                    if (!sigNames.Add(s.Name))
                        throw new InvalidDataException(
                            $"Повторяющееся имя сигнала '{s.Name}' в '{m.Name}'.");

                    EnsureFinite(s.Factor, $"{m.Name}.{s.Name}.Factor");
                    EnsureFinite(s.Offset, $"{m.Name}.{s.Name}.Offset");
                    EnsureFinite(s.Min, $"{m.Name}.{s.Name}.Min");
                    EnsureFinite(s.Max, $"{m.Name}.{s.Name}.Max");
                }
            }
        }

        private static void EnsureFinite(double value, string fieldLabel)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new InvalidDataException($"Поле {fieldLabel}: значение должно быть конечным числом.");
        }

        private static string EscapeQuotes(string s)
            => s.IndexOf('"') < 0 ? s : s.Replace("\"", "\\\"");
    }
}
