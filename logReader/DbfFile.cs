using System.Globalization;
using System.Text;

namespace logReader
{
    // Текстовая база BUSMASTER (.dbf), не dBase.
    public static class DbfFile
    {
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

                if (DbfLineParser.TryParseStartMsg(line, out var header))
                {
                    current = DbfLineParser.ToDbcMessage(header);
                    messages.Add(current);
                    continue;
                }

                if (line.Equals("[END_MSG]", StringComparison.OrdinalIgnoreCase))
                {
                    current = null;
                    continue;
                }

                if (current == null || !line.StartsWith("[START_SIGNALS]", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (DbfLineParser.TryParseStartSignals(line, out var fields))
                    current.Signals.Add(DbfLineParser.ToDbcSignal(fields));
            }

            return messages;
        }

        public static void Write(string path, IReadOnlyList<DbcMessage> messages)
        {
            DbcFile.ValidateMessages(messages);

            SafeFileWriter.Write(path, tmp =>
            {
                var sb = new StringBuilder();
                AppendHeader(sb, messages.Count);

                foreach (var m in messages)
                {
                    sb.Append(DbfLineParser.FormatStartMsg(m)).Append('\n');
                    foreach (var s in m.Signals)
                        sb.Append("[START_SIGNALS] ").Append(DbfLineParser.FormatStartSignals(s)).Append('\n');
                    sb.Append("[END_MSG]").Append('\n').Append('\n');
                }

                AppendFooter(sb);

                var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);
                File.WriteAllText(tmp, sb.ToString(), utf8NoBom);
            });
        }

        public static void CreateEmpty(string path) => Write(path, Array.Empty<DbcMessage>());

        private static void AppendHeader(StringBuilder sb, int messageCount)
        {
            sb.Append("//******************************BUSMASTER Messages and signals Database ******************************//")
              .Append('\n').Append('\n');
            sb.Append("[DATABASE_VERSION] 1.3").Append('\n').Append('\n');
            sb.Append("[PROTOCOL] CAN").Append('\n').Append('\n');
            sb.Append("[BUSMASTER_VERSION] [3.2.2]").Append('\n');
            sb.Append("[NUMBER_OF_MESSAGES] ")
              .Append(messageCount.ToString(CultureInfo.InvariantCulture))
              .Append('\n').Append('\n');
        }

        private static void AppendFooter(StringBuilder sb)
        {
            sb.Append("[START_VALUE_TABLE]").Append('\n');
            sb.Append("[END_VALUE_TABLE]").Append('\n').Append('\n');
            sb.Append("[NODE] ").Append('\n').Append('\n');
            sb.Append("[START_DESC]").Append('\n');
            sb.Append("[START_DESC_NET]").Append('\n');
            sb.Append("[END_DESC_NET]").Append('\n').Append('\n');
            sb.Append("[START_DESC_NODE]").Append('\n');
            sb.Append("[END_DESC_NODE]").Append('\n').Append('\n');
            sb.Append("[START_DESC_MSG]").Append('\n');
            sb.Append("[END_DESC_MSG]").Append('\n').Append('\n');
            sb.Append("[START_DESC_SIG]").Append('\n');
            sb.Append("[END_DESC_SIG]").Append('\n');
            sb.Append("[END_DESC]").Append('\n').Append('\n');
            sb.Append("[START_PARAM]").Append('\n');
            sb.Append("[START_PARAM_NET]").Append('\n');
            sb.Append("[END_PARAM_NET]").Append('\n').Append('\n');
            sb.Append("[START_PARAM_NODE]").Append('\n');
            sb.Append("[END_PARAM_NODE]").Append('\n').Append('\n');
            sb.Append("[START_PARAM_MSG]").Append('\n');
            sb.Append("[END_PARAM_MSG]").Append('\n').Append('\n');
            sb.Append("[START_PARAM_SIG]").Append('\n');
            sb.Append("[END_PARAM_SIG]").Append('\n').Append('\n');
            sb.Append("[START_PARAM_NODE_RX_SIG]").Append('\n');
            sb.Append("[END_PARAM_NODE_RX_SIG]").Append('\n').Append('\n');
            sb.Append("[START_PARAM_NODE_TX_MSG]").Append('\n');
            sb.Append("[END_PARAM_NODE_TX_MSG]").Append('\n');
            sb.Append("[END_PARAM]").Append('\n').Append('\n');
            sb.Append("[START_PARAM_VAL]").Append('\n');
            sb.Append("[START_PARAM_NET_VAL]").Append('\n');
            sb.Append("[END_PARAM_NET_VAL]").Append('\n').Append('\n');
            sb.Append("[START_PARAM_NODE_VAL]").Append('\n');
            sb.Append("[END_PARAM_NODE_VAL]").Append('\n').Append('\n');
            sb.Append("[START_PARAM_MSG_VAL]").Append('\n');
            sb.Append("[END_PARAM_MSG_VAL]").Append('\n').Append('\n');
            sb.Append("[START_PARAM_SIG_VAL]").Append('\n');
            sb.Append("[END_PARAM_SIG_VAL]").Append('\n').Append('\n');
            sb.Append("[END_PARAM_VAL]").Append('\n').Append('\n').Append('\n');
            sb.Append("[START_NOT_SUPPORTED]").Append('\n');
            sb.Append("[END_NOT_SUPPORTED]").Append('\n').Append('\n');
            sb.Append("[START_NOT_PROCESSED]").Append('\n');
            sb.Append("OF_:").Append('\n').Append('\n');
            sb.Append("[END_NOT_PROCESSED]").Append('\n');
        }
    }
}
