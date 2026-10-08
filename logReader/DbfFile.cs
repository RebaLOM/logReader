using System.Globalization;
using System.Text;

namespace logReader
{
    // Текстовая база BUSMASTER (.dbf), не dBase. При сохранении перегенерируются только блоки
    // [START_MSG]…[END_MSG] и счётчик посылок; описания, параметры, таблицы значений — как были.
    public static class DbfFile
    {
        private const string EndMsg = "[END_MSG]";
        private const string NumberOfMessages = "[NUMBER_OF_MESSAGES]";

        public static List<DbcMessage> Read(string path) => ReadDatabase(path).Messages;

        public static DbcDatabase ReadDatabase(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"Файл не найден: {path}");

            var source = DescriptionText.Load(path);
            var db = new DbcDatabase { Source = source };
            var lines = source.Lines;
            int preserved = 0;

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i].Trim();
                if (!DbfLineParser.TryParseStartMsg(line, out var header))
                    continue;

                var message = DbfLineParser.ToDbcMessage(header);
                message.OriginId = source.Blocks.Count;
                DbcSignal? lastSignal = null;

                int end = i + 1;
                for (; end < lines.Count; end++)
                {
                    string inner = lines[end].Trim();
                    if (inner.Equals(EndMsg, StringComparison.OrdinalIgnoreCase))
                    {
                        end++;
                        break;
                    }
                    if (DbfLineParser.TryParseStartSignals(inner, out var fields))
                    {
                        lastSignal = DbfLineParser.ToDbcSignal(fields);
                        message.Signals.Add(lastSignal);
                        continue;
                    }

                    // [VALUE_DESCRIPTION] и прочие строки относятся к предыдущему сигналу.
                    if (lastSignal != null) lastSignal.TrailingLines.Add(lines[end]);
                    else message.ExtraLines.Add(lines[end]);
                    if (inner.Length > 0) preserved++;
                }

                source.Blocks.Add(new SourceBlock
                {
                    OriginId = message.OriginId.Value,
                    Start = i,
                    End = end,
                    RawId = header.Id,
                    SignalNames = message.Signals.Select(s => s.Name).ToHashSet(StringComparer.Ordinal),
                    CanonicalText = string.Join("\n", FormatMessageLines(message)),
                });
                db.Messages.Add(message);
                i = end - 1;
            }

            db.PreservedLineCount = preserved;
            return db;
        }

        public static void Write(string path, IReadOnlyList<DbcMessage> messages)
            => WriteDatabase(path, new DbcDatabase { Messages = messages.ToList() });

        public static void WriteDatabase(string path, DbcDatabase db)
        {
            DbcFile.ValidateMessages(db.Messages);
            string text = db.Source == null ? BuildNewFile(db.Messages) : BuildFromSource(db);
            var encoding = db.Source?.Encoding ?? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

            SafeFileWriter.Write(path, tmp => File.WriteAllText(tmp, text, encoding), keepBackup: true);
        }

        public static void CreateEmpty(string path) => Write(path, Array.Empty<DbcMessage>());

        private static string BuildFromSource(DbcDatabase db)
        {
            var source = db.Source!;
            var byOrigin = db.Messages.Where(m => m.OriginId.HasValue).ToDictionary(m => m.OriginId!.Value);
            var added = db.Messages.Where(m => !m.OriginId.HasValue).ToList();
            var output = new List<string>(source.Lines.Count);
            var lines = source.Lines;
            int blockIndex = 0;
            bool addedWritten = false;

            for (int i = 0; i < lines.Count;)
            {
                if (blockIndex < source.Blocks.Count && source.Blocks[blockIndex].Start == i)
                {
                    var block = source.Blocks[blockIndex];
                    if (byOrigin.TryGetValue(block.OriginId, out var message))
                    {
                        var formatted = FormatMessageLines(message).ToList();
                        if (string.Join("\n", formatted) == block.CanonicalText)
                            output.AddRange(lines.GetRange(block.Start, block.End - block.Start));
                        else
                            output.AddRange(formatted);
                    }

                    i = block.End;
                    blockIndex++;
                    if (blockIndex == source.Blocks.Count)
                    {
                        foreach (var m in added)
                        {
                            output.Add("");
                            output.AddRange(FormatMessageLines(m));
                        }
                        addedWritten = true;
                    }
                    continue;
                }

                string trimmed = lines[i].Trim();
                if (trimmed.StartsWith(NumberOfMessages, StringComparison.OrdinalIgnoreCase))
                {
                    output.Add(NumberOfMessages + " " + db.Messages.Count.ToString(CultureInfo.InvariantCulture));
                    i++;
                    continue;
                }

                // Файл без посылок: новые посылки — перед разделом таблиц значений.
                if (!addedWritten && source.Blocks.Count == 0 && trimmed.StartsWith("[START_VALUE_TABLE]", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var m in added)
                    {
                        output.AddRange(FormatMessageLines(m));
                        output.Add("");
                    }
                    addedWritten = true;
                }

                output.Add(lines[i]);
                i++;
            }

            if (!addedWritten)
                foreach (var m in added)
                    output.AddRange(FormatMessageLines(m));

            return string.Join(source.NewLine, output);
        }

        private static IEnumerable<string> FormatMessageLines(DbcMessage m)
        {
            yield return DbfLineParser.FormatStartMsg(m);
            foreach (var extra in m.ExtraLines)
                yield return extra;
            foreach (var s in m.Signals)
            {
                yield return "[START_SIGNALS] " + DbfLineParser.FormatStartSignals(s);
                foreach (var trailing in s.TrailingLines)
                    yield return trailing;
            }
            yield return EndMsg;
        }

        private static string BuildNewFile(IReadOnlyList<DbcMessage> messages)
        {
            var sb = new StringBuilder();
            AppendHeader(sb, messages.Count);

            foreach (var m in messages)
            {
                foreach (var line in FormatMessageLines(m))
                    sb.Append(line).Append('\n');
                sb.Append('\n');
            }

            AppendFooter(sb);
            return sb.ToString();
        }

        private static void AppendHeader(StringBuilder sb, int messageCount)
        {
            sb.Append("//******************************BUSMASTER Messages and signals Database ******************************//")
              .Append('\n').Append('\n');
            sb.Append("[DATABASE_VERSION] 1.3").Append('\n').Append('\n');
            sb.Append("[PROTOCOL] CAN").Append('\n').Append('\n');
            sb.Append("[BUSMASTER_VERSION] [3.2.2]").Append('\n');
            sb.Append(NumberOfMessages).Append(' ')
              .Append(messageCount.ToString(CultureInfo.InvariantCulture))
              .Append('\n').Append('\n');
        }

        private static void AppendFooter(StringBuilder sb)
        {
            string[] sections =
            {
                "[START_VALUE_TABLE]", "[END_VALUE_TABLE]", "", "[NODE] ", "",
                "[START_DESC]", "[START_DESC_NET]", "[END_DESC_NET]", "", "[START_DESC_NODE]", "[END_DESC_NODE]", "",
                "[START_DESC_MSG]", "[END_DESC_MSG]", "", "[START_DESC_SIG]", "[END_DESC_SIG]", "[END_DESC]", "",
                "[START_PARAM]", "[START_PARAM_NET]", "[END_PARAM_NET]", "", "[START_PARAM_NODE]", "[END_PARAM_NODE]", "",
                "[START_PARAM_MSG]", "[END_PARAM_MSG]", "", "[START_PARAM_SIG]", "[END_PARAM_SIG]", "",
                "[START_PARAM_NODE_RX_SIG]", "[END_PARAM_NODE_RX_SIG]", "", "[START_PARAM_NODE_TX_MSG]", "[END_PARAM_NODE_TX_MSG]",
                "[END_PARAM]", "", "[START_PARAM_VAL]", "[START_PARAM_NET_VAL]", "[END_PARAM_NET_VAL]", "",
                "[START_PARAM_NODE_VAL]", "[END_PARAM_NODE_VAL]", "", "[START_PARAM_MSG_VAL]", "[END_PARAM_MSG_VAL]", "",
                "[START_PARAM_SIG_VAL]", "[END_PARAM_SIG_VAL]", "", "[END_PARAM_VAL]", "", "",
                "[START_NOT_SUPPORTED]", "[END_NOT_SUPPORTED]", "", "[START_NOT_PROCESSED]", "OF_:", "", "[END_NOT_PROCESSED]",
            };
            foreach (var line in sections)
                sb.Append(line).Append('\n');
        }
    }
}
