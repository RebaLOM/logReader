using System.Globalization;
using System.Text.RegularExpressions;

namespace logReader
{
    // Единый разбор строк BO_/SG_ для редактора и загрузчика — одни и те же правила.
    public static class DbcLineParser
    {
        internal const uint ExtendedIdFlag = 0x80000000u;
        internal const uint IdMask = 0x1FFFFFFFu;

        private const string Number = @"[-+]?(?:\d+(?:[.,]\d*)?|[.,]\d+)(?:[eE][-+]?\d+)?";

        private static readonly Regex MessageRegex = new(
            @"^BO_\s+(?<id>\d+)\s+(?<name>[^\s:]+)\s*:\s*(?<dlc>\d+)\s+(?<tx>\S+)",
            RegexOptions.Compiled);

        private static readonly Regex SignalRegexFull = new(
            @"^SG_\s+(?<name>[^\s:]+)(?:\s+(?<mux>M|m\d+M?))?\s*:\s*(?<start>\d+)\|(?<length>\d+)@(?<order>[01])(?<sign>[+-])\s*" +
            $@"\(\s*(?<factor>{Number})\s*,\s*(?<offset>{Number})\s*\)\s*\[\s*(?<min>{Number})\s*\|\s*(?<max>{Number})\s*\]\s*" +
            @"""(?<unit>[^""]*)""\s*(?<rx>.*?)\s*$",
            RegexOptions.Compiled);

        private static readonly Regex SignalRegexShort = new(
            @"^SG_\s+(?<name>[^\s:]+)(?:\s+(?<mux>M|m\d+M?))?\s*:\s*(?<start>\d+)\|(?<length>\d+)@(?<order>[01])(?<sign>[+-])\s*" +
            $@"\(\s*(?<factor>{Number})\s*,\s*(?<offset>{Number})\s*\)",
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

            // Переполнение uint/int в «BO_ 99999999999 …» — строка не посылка, а не падение всего файла.
            if (!uint.TryParse(m.Groups["id"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint raw))
                return false;
            if (!int.TryParse(m.Groups["dlc"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int dlc))
                return false;

            header = new MessageHeader
            {
                RawId = raw,
                Name = m.Groups["name"].Value,
                Dlc = dlc,
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

            if (!int.TryParse(match.Groups["start"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int start)
                || !int.TryParse(match.Groups["length"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int length)
                || !TryParseNumber(match.Groups["factor"].Value, out double factor)
                || !TryParseNumber(match.Groups["offset"].Value, out double offset))
                return false;

            double min = 0, max = 0;
            if (hasFullForm
                && (!TryParseNumber(match.Groups["min"].Value, out min) || !TryParseNumber(match.Groups["max"].Value, out max)))
                return false;

            string receiver = hasFullForm ? match.Groups["rx"].Value.Trim() : "";
            signal = new DbcSignal
            {
                Name = match.Groups["name"].Value,
                StartBit = start,
                Length = length,
                IsLittleEndian = match.Groups["order"].Value == "1",
                IsSigned = match.Groups["sign"].Value == "-",
                Factor = factor,
                Offset = offset,
                Min = min,
                Max = max,
                Unit = hasFullForm ? match.Groups["unit"].Value : "",
                Receiver = receiver.Length > 0 ? receiver : "Vector__XXX",
                MultiplexIndicator = match.Groups["mux"].Value,
            };
            signal.OriginName = signal.Name;
            return true;
        }

        private static bool TryParseNumber(string text, out double value)
        {
            value = 0;
            try
            {
                value = NumberParseHelper.ParseDoubleInvariant(text);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
            catch (OverflowException)
            {
                return false;
            }
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
