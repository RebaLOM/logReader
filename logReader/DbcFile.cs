using System.Globalization;
using System.Text;
using logReader.Processing;

namespace logReader
{
    // DBC: посылки/сигналы разбираются для редактора и загрузчика; при сохранении перегенерируются
    // только блоки BO_/SG_, а всё остальное (CM_, BA_*, VAL_, BU_, SIG_VALTYPE_ …) пишется как было.
    public static class DbcFile
    {
        private const string DefaultReceiver = "Vector__XXX";
        private const string DefaultTransmitter = "Vector__XXX";

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
                if (!DbcLineParser.TryParseMessage(line, out var header))
                {
                    if (line.Length > 0) preserved++;
                    continue;
                }

                var message = new DbcMessage
                {
                    Id = header.Id,
                    IsExtended = header.IsExtended,
                    Name = header.Name,
                    Dlc = header.Dlc,
                    Transmitter = header.Transmitter,
                    OriginId = source.Blocks.Count,
                };

                int end = i + 1;
                while (end < lines.Count && lines[end].TrimStart().StartsWith("SG_", StringComparison.Ordinal))
                {
                    string sgLine = lines[end].Trim();
                    if (DbcLineParser.TryParseSignal(sgLine, out var sig))
                        message.Signals.Add(sig);
                    else
                    {
                        message.ExtraLines.Add(lines[end]);
                        preserved++;
                    }
                    end++;
                }

                source.Blocks.Add(new SourceBlock
                {
                    OriginId = message.OriginId.Value,
                    Start = i,
                    End = end,
                    RawId = header.RawId,
                    SignalNames = message.Signals.Select(s => s.Name).ToHashSet(StringComparer.Ordinal),
                    CanonicalText = string.Join("\n", FormatMessageLines(message)),
                });
                db.Messages.Add(message);
                i = end - 1;
            }

            ApplyValueTypes(db, lines);
            db.PreservedLineCount = preserved;
            return db;
        }

        public static void Write(string path, IReadOnlyList<DbcMessage> messages)
            => WriteDatabase(path, new DbcDatabase { Messages = messages.ToList() });

        public static void WriteDatabase(string path, DbcDatabase db)
        {
            ValidateMessages(db.Messages);
            string text = db.Source == null ? BuildNewFile(db.Messages) : BuildFromSource(db);
            var encoding = db.Source?.Encoding ?? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

            SafeFileWriter.Write(path, tmp => File.WriteAllText(tmp, text, encoding), keepBackup: true);
        }

        public static void CreateEmpty(string path)
        {
            Write(path, Array.Empty<DbcMessage>());
        }

        private static string BuildNewFile(IReadOnlyList<DbcMessage> messages)
        {
            var sb = new StringBuilder();
            sb.Append("VERSION \"\"\n\n\n");
            sb.Append("NS_ :\n\n");
            sb.Append("BS_:\n\n");

            var nodes = messages
                .Select(m => m.Transmitter)
                .Concat(messages.SelectMany(m => m.Signals).SelectMany(s => SplitReceivers(s.Receiver)))
                .Where(n => !string.IsNullOrWhiteSpace(n) && n != DefaultTransmitter)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            sb.Append("BU_:");
            foreach (var node in nodes) sb.Append(' ').Append(node);
            sb.Append("\n\n\n");

            foreach (var m in messages)
            {
                AppendMessage(sb, m, "\n");
                sb.Append('\n');
            }

            foreach (var m in messages)
                foreach (var s in m.Signals.Where(s => s.ValueType != SignalValueType.Integer))
                    sb.Append("SIG_VALTYPE_ ").Append(RawId(m).ToString(CultureInfo.InvariantCulture))
                      .Append(' ').Append(s.Name).Append(" : ")
                      .Append(s.ValueType == SignalValueType.Float32 ? '1' : '2').Append(";\n");

            return sb.ToString();
        }

        private static string BuildFromSource(DbcDatabase db)
        {
            var source = db.Source!;
            var byOrigin = db.Messages.Where(m => m.OriginId.HasValue).ToDictionary(m => m.OriginId!.Value);
            var added = db.Messages.Where(m => !m.OriginId.HasValue).ToList();
            var references = new ReferenceMap(source.Blocks, byOrigin);

            var output = new List<string>(source.Lines.Count + added.Count * 4);
            int blockIndex = 0;
            int lastBlock = source.Blocks.Count - 1;
            var lines = source.Lines;

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

                    if (blockIndex == lastBlock)
                        AppendAddedMessages(output, added);

                    i = block.End;
                    blockIndex++;
                    continue;
                }

                if (DbcStatement.StartsReferenceStatement(lines[i]))
                {
                    int end = DbcStatement.FindEnd(lines, i);
                    var statement = lines.GetRange(i, end - i);
                    var rewritten = references.Rewrite(statement);
                    if (rewritten != null) output.AddRange(rewritten);
                    i = end;
                    continue;
                }

                output.Add(lines[i]);
                i++;
            }

            if (source.Blocks.Count == 0)
                AppendAddedMessages(output, added, insertAt: FindInsertPositionForFirstMessage(output));

            return string.Join(source.NewLine, output);
        }

        private static void AppendAddedMessages(List<string> output, List<DbcMessage> added, int? insertAt = null)
        {
            if (added.Count == 0) return;
            var block = new List<string>();
            foreach (var m in added)
            {
                block.Add("");
                block.AddRange(FormatMessageLines(m));
            }
            if (insertAt.HasValue) output.InsertRange(insertAt.Value, block);
            else output.AddRange(block);
        }

        // Новые посылки в файле без BO_ — перед первым разделом атрибутов/комментариев (или в конец).
        private static int FindInsertPositionForFirstMessage(List<string> output)
        {
            for (int i = 0; i < output.Count; i++)
                if (DbcStatement.StartsReferenceStatement(output[i]) || output[i].TrimStart().StartsWith("BA_DEF", StringComparison.Ordinal))
                    return i;
            int end = output.Count;
            while (end > 0 && output[end - 1].Length == 0) end--;
            return end;
        }

        private static IEnumerable<string> FormatMessageLines(DbcMessage m)
        {
            var sb = new StringBuilder();
            AppendMessage(sb, m, "\n");
            foreach (var line in sb.ToString().TrimEnd('\n').Split('\n'))
                yield return line;
            foreach (var extra in m.ExtraLines)
                yield return extra;
        }

        internal static uint RawId(DbcMessage m)
        {
            uint id = m.Id & DbcLineParser.IdMask;
            if (m.IsExtended) id |= DbcLineParser.ExtendedIdFlag;
            return id;
        }

        private static void AppendMessage(StringBuilder sb, DbcMessage m, string newLine)
        {
            string transmitter = string.IsNullOrWhiteSpace(m.Transmitter) ? DefaultTransmitter : m.Transmitter;
            string messageName = string.IsNullOrWhiteSpace(m.Name)
                ? ("Msg_" + m.Id.ToString("X", CultureInfo.InvariantCulture))
                : m.Name;

            sb.Append("BO_ ")
              .Append(RawId(m).ToString(CultureInfo.InvariantCulture))
              .Append(' ')
              .Append(messageName)
              .Append(": ")
              .Append(m.Dlc.ToString(CultureInfo.InvariantCulture))
              .Append(' ')
              .Append(transmitter)
              .Append(newLine);

            foreach (var s in m.Signals)
            {
                string receiver = string.IsNullOrWhiteSpace(s.Receiver) ? DefaultReceiver : s.Receiver;

                sb.Append(" SG_ ").Append(s.Name);
                if (!string.IsNullOrEmpty(s.MultiplexIndicator))
                    sb.Append(' ').Append(s.MultiplexIndicator);
                sb.Append(" : ")
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
                  .Append(newLine);
            }
        }

        private static IEnumerable<string> SplitReceivers(string receivers)
            => (receivers ?? "").Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);

        // SIG_VALTYPE_ <id> <signal> : 1|2; — IEEE float/double вместо целого raw.
        private static void ApplyValueTypes(DbcDatabase db, List<string> lines)
        {
            var byRawId = new Dictionary<uint, DbcMessage>();
            foreach (var m in db.Messages)
                byRawId.TryAdd(RawId(m), m);

            foreach (var raw in lines)
            {
                string line = raw.Trim();
                if (!line.StartsWith("SIG_VALTYPE_", StringComparison.Ordinal)) continue;
                var tokens = line.TrimEnd(';').Split(new[] { ' ', '\t', ':' }, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length < 4
                    || !uint.TryParse(tokens[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out uint rawId)
                    || !byRawId.TryGetValue(rawId, out var message))
                    continue;

                var signal = message.Signals.FirstOrDefault(s => s.Name == tokens[2]);
                if (signal == null) continue;
                signal.ValueType = tokens[3] switch
                {
                    "1" => SignalValueType.Float32,
                    "2" => SignalValueType.Float64,
                    _ => SignalValueType.Integer
                };
            }
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

        // Переименование/удаление ссылок на посылки и сигналы в CM_, BA_, VAL_, SIG_VALTYPE_ и т.п.
        private sealed class ReferenceMap
        {
            private readonly HashSet<uint> _deletedIds = new();
            private readonly Dictionary<uint, uint> _idChanges = new();
            private readonly Dictionary<uint, HashSet<string>> _deletedSignals = new();
            private readonly Dictionary<uint, Dictionary<string, string>> _renamedSignals = new();

            public ReferenceMap(IEnumerable<SourceBlock> blocks, Dictionary<int, DbcMessage> byOrigin)
            {
                foreach (var block in blocks)
                {
                    if (!byOrigin.TryGetValue(block.OriginId, out var message))
                    {
                        _deletedIds.Add(block.RawId);
                        continue;
                    }

                    uint newRaw = RawId(message);
                    if (newRaw != block.RawId)
                        _idChanges[block.RawId] = newRaw;

                    var kept = new HashSet<string>(StringComparer.Ordinal);
                    var renames = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (var s in message.Signals)
                    {
                        if (s.OriginName == null || !block.SignalNames.Contains(s.OriginName)) continue;
                        kept.Add(s.OriginName);
                        if (s.OriginName != s.Name) renames[s.OriginName] = s.Name;
                    }

                    var deleted = block.SignalNames.Where(n => !kept.Contains(n)).ToHashSet(StringComparer.Ordinal);
                    if (deleted.Count > 0) _deletedSignals[block.RawId] = deleted;
                    if (renames.Count > 0) _renamedSignals[block.RawId] = renames;
                }
            }

            // null — оператор ссылается на удалённую посылку/сигнал и не записывается.
            public List<string>? Rewrite(List<string> statement)
            {
                string text = string.Join("\n", statement);
                var reference = DbcStatement.FindReference(text);
                if (reference == null) return statement;

                var (idToken, signalTokens) = reference.Value;
                if (!uint.TryParse(idToken.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint rawId))
                    return statement;

                if (_deletedIds.Contains(rawId))
                    return null;

                var edits = new List<(DbcStatement.Token Token, string Replacement)>();
                if (_deletedSignals.TryGetValue(rawId, out var deleted) && signalTokens.Any(t => deleted.Contains(t.Text)))
                    return null;
                if (_renamedSignals.TryGetValue(rawId, out var renames))
                    foreach (var t in signalTokens)
                        if (renames.TryGetValue(t.Text, out string? newName))
                            edits.Add((t, newName));
                if (_idChanges.TryGetValue(rawId, out uint newRaw))
                    edits.Add((idToken, newRaw.ToString(CultureInfo.InvariantCulture)));

                if (edits.Count == 0) return statement;

                foreach (var (token, replacement) in edits.OrderByDescending(e => e.Token.Start))
                    text = text[..token.Start] + replacement + text[(token.Start + token.Text.Length)..];
                return text.Split('\n').ToList();
            }
        }
    }

    // Операторы DBC, ссылающиеся на посылку по raw ID (и, возможно, на сигнал по имени).
    internal static class DbcStatement
    {
        internal readonly record struct Token(int Start, string Text);

        private static readonly string[] Keywords =
        {
            "CM_", "BA_", "VAL_", "SIG_VALTYPE_", "BO_TX_BU_", "SG_MUL_VAL_", "SIG_GROUP_",
        };

        // В разделе «NS_ :» те же ключевые слова стоят одни в строке — это не операторы.
        public static bool StartsReferenceStatement(string line)
        {
            string t = line.Trim();
            foreach (var k in Keywords)
                if (t.Length > k.Length && t.StartsWith(k, StringComparison.Ordinal) && char.IsWhiteSpace(t[k.Length]))
                    return true;
            return false;
        }

        // Оператор заканчивается «;» вне кавычек (комментарий CM_ может занимать несколько строк).
        public static int FindEnd(List<string> lines, int start)
        {
            bool inQuotes = false;
            for (int i = start; i < lines.Count; i++)
            {
                string line = lines[i];
                for (int c = 0; c < line.Length; c++)
                {
                    if (line[c] == '\\' && inQuotes) { c++; continue; }
                    if (line[c] == '"') inQuotes = !inQuotes;
                    else if (line[c] == ';' && !inQuotes) return i + 1;
                }
            }
            return lines.Count;
        }

        public static (Token Id, List<Token> Signals)? FindReference(string text)
        {
            var tokens = Tokenize(text, 6);
            if (tokens.Count < 2) return null;

            string kw = tokens[0].Text;
            switch (kw)
            {
                case "CM_" when tokens.Count >= 3 && tokens[1].Text == "BO_":
                    return (tokens[2], new List<Token>());
                case "CM_" when tokens.Count >= 4 && tokens[1].Text == "SG_":
                    return (tokens[2], new List<Token> { tokens[3] });
                case "BA_" when tokens.Count >= 4 && tokens[2].Text == "BO_":
                    return (tokens[3], new List<Token>());
                case "BA_" when tokens.Count >= 5 && tokens[2].Text == "SG_":
                    return (tokens[3], new List<Token> { tokens[4] });
                case "VAL_" when tokens.Count >= 3:
                case "SIG_VALTYPE_" when tokens.Count >= 3:
                    return (tokens[1], new List<Token> { tokens[2] });
                case "SG_MUL_VAL_" when tokens.Count >= 4:
                    return (tokens[1], new List<Token> { tokens[2], tokens[3] });
                case "BO_TX_BU_":
                case "SIG_GROUP_":
                    return (tokens[1], new List<Token>());
                default:
                    return null;
            }
        }

        // Первые токены оператора (строки в кавычках — один токен) с позициями в тексте.
        private static List<Token> Tokenize(string text, int max)
        {
            var tokens = new List<Token>();
            int i = 0;
            while (i < text.Length && tokens.Count < max)
            {
                while (i < text.Length && (char.IsWhiteSpace(text[i]) || text[i] == ':' || text[i] == ';')) i++;
                if (i >= text.Length) break;

                int start = i;
                if (text[i] == '"')
                {
                    i++;
                    while (i < text.Length && text[i] != '"') i++;
                    i = Math.Min(i + 1, text.Length);
                }
                else
                {
                    while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] != ':' && text[i] != ';') i++;
                }
                tokens.Add(new Token(start, text[start..i]));
            }
            return tokens;
        }
    }

    // Исходный текст файла описания: кодировка и перевод строк сохраняются при записи.
    internal static class DescriptionText
    {
        public static DescriptionSource Load(string path)
        {
            var encoding = LogFileEncoding.Detect(path);
            // Encoding.UTF8 при записи добавил бы BOM, которого в исходном файле не было.
            if (encoding is UTF8Encoding && !HasUtf8Bom(path))
                encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            string text = File.ReadAllText(path, encoding);
            string newLine = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            var lines = text.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
            return new DescriptionSource { Lines = lines, Encoding = encoding, NewLine = newLine };
        }

        private static bool HasUtf8Bom(string path)
        {
            using var fs = File.OpenRead(path);
            Span<byte> bom = stackalloc byte[3];
            return fs.Read(bom) == 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF;
        }
    }
}
