using System.Globalization;
using System.Text.RegularExpressions;

namespace logReader
{
    /// <summary>
    /// Единый синтаксический разбор строк DBC (BO_ и SG_). Используется и редактором,
    /// и загрузчиком устройств, чтобы правила распознавания совпадали один к одному.
    /// </summary>
    public static class DbcLineParser
    {
        internal const uint ExtendedIdFlag = 0x80000000u;
        internal const uint IdMask = 0x1FFFFFFFu;

        private static readonly Regex MessageRegex = new(
            @"^BO_\s+(?<id>\d+)\s+(?<name>\S+)\s*:\s*(?<dlc>\d+)\s+(?<tx>\S+)",
            RegexOptions.Compiled);

        private static readonly Regex SignalRegexFull = new(
            @"^SG_\s+(?<name>\S+)\s*:\s*(?<start>\d+)\|(?<length>\d+)@(?<order>[01])(?<sign>[+-])\s+\((?<factor>[-+]?\d+(?:[.,]\d+)?(?:[eE][-+]?\d+)?),(?<offset>[-+]?\d+(?:[.,]\d+)?(?:[eE][-+]?\d+)?)\)\s*\[(?<min>[-+]?\d+(?:[.,]\d+)?(?:[eE][-+]?\d+)?)\|(?<max>[-+]?\d+(?:[.,]\d+)?(?:[eE][-+]?\d+)?)\]\s*""(?<unit>[^""]*)""\s+(?<rx>\S+)",
            RegexOptions.Compiled);

        private static readonly Regex SignalRegexShort = new(
            @"^SG_\s+(?<name>\S+)\s*:\s*(?<start>\d+)\|(?<length>\d+)@(?<order>[01])(?<sign>[+-])\s+\((?<factor>[-+]?\d+(?:[.,]\d+)?(?:[eE][-+]?\d+)?),(?<offset>[-+]?\d+(?:[.,]\d+)?(?:[eE][-+]?\d+)?)\)",
            RegexOptions.Compiled);

        public readonly struct MessageHeader
        {
            public uint RawId { get; init; }
            public uint Id => RawId & IdMask;
            public bool IsExtended => (RawId & ExtendedIdFlag) != 0;
            public string Name { get; init; }
            public int Dlc { get; init; }
            public string Transmitter { get; init; }
        }

        public static bool TryParseMessage(string line, out MessageHeader header)
        {
            header = default;
            var m = MessageRegex.Match(line);
            if (!m.Success) return false;

            uint raw = uint.Parse(m.Groups["id"].Value, CultureInfo.InvariantCulture);
            header = new MessageHeader
            {
                RawId = raw,
                Name = m.Groups["name"].Value,
                Dlc = int.Parse(m.Groups["dlc"].Value, CultureInfo.InvariantCulture),
                Transmitter = m.Groups["tx"].Value
            };
            return true;
        }

        public static bool TryParseSignal(string line, out DbcSignal signal)
        {
            signal = default!;

            var match = SignalRegexFull.Match(line);
            bool hasFullForm = match.Success;
            if (!hasFullForm) match = SignalRegexShort.Match(line);
            if (!match.Success) return false;

            signal = new DbcSignal
            {
                Name = match.Groups["name"].Value,
                StartBit = int.Parse(match.Groups["start"].Value, CultureInfo.InvariantCulture),
                Length = int.Parse(match.Groups["length"].Value, CultureInfo.InvariantCulture),
                IsLittleEndian = match.Groups["order"].Value == "1",
                IsSigned = match.Groups["sign"].Value == "-",
                Factor = NumberParseHelper.ParseDoubleInvariant(match.Groups["factor"].Value),
                Offset = NumberParseHelper.ParseDoubleInvariant(match.Groups["offset"].Value),
                Min = hasFullForm ? NumberParseHelper.ParseDoubleInvariant(match.Groups["min"].Value) : 0,
                Max = hasFullForm ? NumberParseHelper.ParseDoubleInvariant(match.Groups["max"].Value) : 0,
                Unit = hasFullForm ? match.Groups["unit"].Value : "",
                Receiver = hasFullForm ? match.Groups["rx"].Value : "Vector__XXX"
            };
            return true;
        }

        public static bool IsValidSymbolName(string? name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            char first = name[0];
            if (!(char.IsLetter(first) || first == '_')) return false;
            for (int i = 1; i < name.Length; i++)
            {
                char c = name[i];
                if (!(char.IsLetterOrDigit(c) || c == '_')) return false;
            }
            return true;
        }

        public const string SymbolNameRulesHint =
            "Разрешены буквы/цифры/подчёркивания, первый символ — не цифра.";
    }
}
