namespace logReader
{
    public sealed class DbcSignal
    {
        public string Name { get; set; } = "";
        public int StartBit { get; set; }
        public int Length { get; set; } = 8;
        public bool IsLittleEndian { get; set; } = true;
        public bool IsSigned { get; set; }
        public double Factor { get; set; } = 1.0;
        public double Offset { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }
        public string Unit { get; set; } = "";
        public string Receiver { get; set; } = "Vector__XXX";

        // «M» — мультиплексор, «m3» — сигнал при значении мультиплексора 3, «m3M» — расширенный вариант.
        public string MultiplexIndicator { get; set; } = "";
        public SignalValueType ValueType { get; set; } = SignalValueType.Integer;

        // Имя сигнала в исходном файле: по нему при сохранении переименовываются ссылки (CM_, VAL_, BA_ …).
        public string? OriginName { get; set; }

        // DBF: строки после сигнала внутри посылки (например, [VALUE_DESCRIPTION]) — сохраняются как есть.
        public List<string> TrailingLines { get; set; } = new();

        public bool IsMultiplexor => MultiplexIndicator == "M";

        public int? MultiplexValue
        {
            get
            {
                if (MultiplexIndicator.Length < 2 || MultiplexIndicator[0] != 'm') return null;
                string digits = MultiplexIndicator.TrimEnd('M')[1..];
                return int.TryParse(digits, System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out int v) ? v : null;
            }
        }

        public DbcSignal Clone() => new()
        {
            Name = Name,
            StartBit = StartBit,
            Length = Length,
            IsLittleEndian = IsLittleEndian,
            IsSigned = IsSigned,
            Factor = Factor,
            Offset = Offset,
            Min = Min,
            Max = Max,
            Unit = Unit,
            Receiver = Receiver,
            MultiplexIndicator = MultiplexIndicator,
            ValueType = ValueType,
            OriginName = OriginName,
            TrailingLines = new List<string>(TrailingLines),
        };
    }

    public sealed class DbcMessage
    {
        public string Name { get; set; } = "";
        public uint Id { get; set; }
        public bool IsExtended { get; set; } = true;
        public int Dlc { get; set; } = 8;
        public string Transmitter { get; set; } = "Vector__XXX";
        public List<DbcSignal> Signals { get; set; } = new();

        // Номер блока посылки в исходном файле; null — посылка добавлена в редакторе.
        public int? OriginId { get; set; }

        // Строки внутри блока посылки, которые не удалось разобрать как сигнал, — сохраняются как есть.
        public List<string> ExtraLines { get; set; } = new();

        public string IdHex => CanId.Format(Id, IsExtended);

        public DbcMessage Clone() => new()
        {
            Name = Name,
            Id = Id,
            IsExtended = IsExtended,
            Dlc = Dlc,
            Transmitter = Transmitter,
            Signals = Signals.Select(s => s.Clone()).ToList(),
            OriginId = OriginId,
            ExtraLines = new List<string>(ExtraLines),
        };
    }

    // Файл описания целиком: посылки + исходный текст для сохранения без потерь.
    public sealed class DbcDatabase
    {
        public List<DbcMessage> Messages { get; set; } = new();

        // Число строк исходного файла, не распознанных как посылки/сигналы (сохраняются как есть).
        public int PreservedLineCount { get; internal set; }

        internal DescriptionSource? Source { get; set; }
    }

    internal sealed class DescriptionSource
    {
        public required List<string> Lines { get; init; }
        public required System.Text.Encoding Encoding { get; init; }
        public required string NewLine { get; init; }
        public List<SourceBlock> Blocks { get; } = new();
    }

    // Блок посылки в исходном тексте: строки [Start, End) и исходные ID/имена сигналов.
    internal sealed class SourceBlock
    {
        public required int OriginId { get; init; }
        public required int Start { get; init; }
        public required int End { get; init; }
        public required uint RawId { get; init; }
        public required HashSet<string> SignalNames { get; init; }

        // Посылка в каноническом виде на момент чтения: если она не изменилась, блок пишется
        // исходными строками (сохраняются пробелы и форматирование автора файла).
        public required string CanonicalText { get; init; }
    }
}
