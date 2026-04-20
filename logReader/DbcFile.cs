using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace logReader
{
    public static class DbcFile
    {
        private const uint ExtendedIdFlag = 0x80000000u;
        private const uint IdMask = 0x1FFFFFFFu;

        private static readonly Regex MessageRegex = new(
            @"^BO_\s+(?<id>\d+)\s+(?<name>\S+)\s*:\s*(?<dlc>\d+)\s+(?<tx>\S+)",
            RegexOptions.Compiled);

        private static readonly Regex SignalRegex = new(
            @"^SG_\s+(?<name>\S+)\s*:\s*(?<start>\d+)\|(?<length>\d+)@(?<order>[01])(?<sign>[+-])\s+\((?<factor>[-+]?\d+(?:[.,]\d+)?(?:[eE][-+]?\d+)?),(?<offset>[-+]?\d+(?:[.,]\d+)?(?:[eE][-+]?\d+)?)\)\s*\[(?<min>[-+]?\d+(?:[.,]\d+)?(?:[eE][-+]?\d+)?)\|(?<max>[-+]?\d+(?:[.,]\d+)?(?:[eE][-+]?\d+)?)\]\s*""(?<unit>[^""]*)""\s+(?<rx>\S+)",
            RegexOptions.Compiled);

        private static readonly Regex SignalRegexFallback = new(
            @"^SG_\s+(?<name>\S+)\s*:\s*(?<start>\d+)\|(?<length>\d+)@(?<order>[01])(?<sign>[+-])\s+\((?<factor>[-+]?\d+(?:[.,]\d+)?(?:[eE][-+]?\d+)?),(?<offset>[-+]?\d+(?:[.,]\d+)?(?:[eE][-+]?\d+)?)\)",
            RegexOptions.Compiled);

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

                var msgMatch = MessageRegex.Match(line);
                if (msgMatch.Success)
                {
                    uint rawId = uint.Parse(msgMatch.Groups["id"].Value, CultureInfo.InvariantCulture);
                    bool isExt = (rawId & ExtendedIdFlag) != 0;
                    uint id = rawId & IdMask;

                    current = new DbcMessage
                    {
                        Id = id,
                        IsExtended = isExt,
                        Name = msgMatch.Groups["name"].Value,
                        Dlc = int.Parse(msgMatch.Groups["dlc"].Value, CultureInfo.InvariantCulture),
                        Transmitter = msgMatch.Groups["tx"].Value
                    };
                    messages.Add(current);
                    continue;
                }

                if (current == null || !line.StartsWith("SG_", StringComparison.Ordinal))
                    continue;

                var sigMatch = SignalRegex.Match(line);
                if (!sigMatch.Success)
                {
                    var fb = SignalRegexFallback.Match(line);
                    if (!fb.Success) continue;

                    current.Signals.Add(new DbcSignal
                    {
                        Name = fb.Groups["name"].Value,
                        StartBit = int.Parse(fb.Groups["start"].Value, CultureInfo.InvariantCulture),
                        Length = int.Parse(fb.Groups["length"].Value, CultureInfo.InvariantCulture),
                        IsLittleEndian = fb.Groups["order"].Value == "1",
                        IsSigned = fb.Groups["sign"].Value == "-",
                        Factor = ParseDouble(fb.Groups["factor"].Value),
                        Offset = ParseDouble(fb.Groups["offset"].Value),
                        Min = 0,
                        Max = 0,
                        Unit = "",
                        Receiver = "Vector__XXX"
                    });
                    continue;
                }

                current.Signals.Add(new DbcSignal
                {
                    Name = sigMatch.Groups["name"].Value,
                    StartBit = int.Parse(sigMatch.Groups["start"].Value, CultureInfo.InvariantCulture),
                    Length = int.Parse(sigMatch.Groups["length"].Value, CultureInfo.InvariantCulture),
                    IsLittleEndian = sigMatch.Groups["order"].Value == "1",
                    IsSigned = sigMatch.Groups["sign"].Value == "-",
                    Factor = ParseDouble(sigMatch.Groups["factor"].Value),
                    Offset = ParseDouble(sigMatch.Groups["offset"].Value),
                    Min = ParseDouble(sigMatch.Groups["min"].Value),
                    Max = ParseDouble(sigMatch.Groups["max"].Value),
                    Unit = sigMatch.Groups["unit"].Value,
                    Receiver = sigMatch.Groups["rx"].Value
                });
            }

            return messages;
        }

        public static void Write(string path, IReadOnlyList<DbcMessage> messages)
        {
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var sb = new StringBuilder();
            sb.AppendLine("VERSION \"\"");
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("BS_:");
            sb.AppendLine();
            sb.AppendLine("BU_:");
            sb.AppendLine();
            sb.AppendLine();

            foreach (var m in messages)
            {
                uint id = m.Id & IdMask;
                if (m.IsExtended) id |= ExtendedIdFlag;

                string transmitter = string.IsNullOrWhiteSpace(m.Transmitter) ? "Vector__XXX" : m.Transmitter;
                string messageName = string.IsNullOrWhiteSpace(m.Name) ? ("Msg_" + m.Id.ToString("X", CultureInfo.InvariantCulture)) : m.Name;

                sb.Append("BO_ ")
                  .Append(id.ToString(CultureInfo.InvariantCulture))
                  .Append(' ')
                  .Append(messageName)
                  .Append(": ")
                  .Append(m.Dlc.ToString(CultureInfo.InvariantCulture))
                  .Append(' ')
                  .Append(transmitter)
                  .AppendLine();

                foreach (var s in m.Signals)
                {
                    string receiver = string.IsNullOrWhiteSpace(s.Receiver) ? "Vector__XXX" : s.Receiver;

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
                      .Append(s.Unit ?? "")
                      .Append("\" ")
                      .Append(receiver)
                      .AppendLine();
                }

                sb.AppendLine();
            }

            File.WriteAllText(path, sb.ToString());
        }

        public static void CreateEmpty(string path)
        {
            Write(path, Array.Empty<DbcMessage>());
        }

        private static double ParseDouble(string value)
        {
            string normalized = value.Trim().Replace(',', '.');
            return double.Parse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

    }
}
