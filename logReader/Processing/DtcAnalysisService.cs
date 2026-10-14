using System.Globalization;
using System.Text;

namespace logReader.Processing;

// Read-only analysis uses the same CAN parsers as ordinary conversion and does not
// require a message-description file. Source timestamps retain their own time system.
internal static class DtcAnalysisService
{
    public static DtcAnalysisResult Analyze(string path, double timeoutMs = 2000, ProcessingContext? context = null)
    {
        _ = new LocarusDtcDecoder(timeoutMs); // Validate the setting before opening a file.
        context ??= new ProcessingContext();
        context.ThrowIfCancellationRequested();
        var observations = new List<DtcObservation>();
        var episodes = new List<DtcEpisode>();
        var summaries = new List<DtcSummary>();
        var warnings = new List<string>();
        string[] paths = Directory.Exists(path)
            ? LogFolderScanner.EnumerateSupportedLogFiles(path).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray()
            : File.Exists(path) ? [path] : throw new FileNotFoundException("Файл или папка лога не найдены.", path);
        if (paths.Length == 0) warnings.Add("В папке нет поддерживаемых файлов логов.");
        for (int i = 0; i < paths.Length; i++)
        {
            context.ThrowIfCancellationRequested();
            string source = paths[i];
            var history = new History(source, timeoutMs, observations, episodes, summaries);
            try
            {
                AnalyzeFile(source, timeoutMs, context.Slice((double)i / paths.Length, 1d / paths.Length, Path.GetFileName(source)), history, warnings);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                warnings.Add($"{Path.GetFileName(source)}: {ex.Message}");
            }
            history.Finish();
            if (history.HasTruncatedList) warnings.Add($"{Path.GetFileName(source)}: заявлено больше шести ошибок; профиль содержит только шесть позиций, отсутствие остальных кодов подтвердить нельзя.");
            if (!history.HasObservations) warnings.Add($"{Path.GetFileName(source)}: диагностические данные Локаруса не найдены.");
        }
        context.ReportProgress(1);
        return new DtcAnalysisResult(observations, episodes, summaries, warnings);
    }

    private static void AnalyzeFile(string path, double timeout, ProcessingContext context, History history, List<string> warnings)
    {
        Encoding encoding = LogFileEncoding.Detect(path);
        if (Path.GetExtension(path).Equals(".csv", StringComparison.OrdinalIgnoreCase)
            && TryVendorHeader(path, encoding, out var header))
        {
            AnalyzeVendor(path, encoding, header, context, history, warnings);
            return;
        }
        LogFormatKind format = LogFormatDetector.Detect(path);
        if (format == LogFormatKind.MatrixCsv) { AnalyzeMatrix(path, encoding, timeout, context, history); return; }
        if (format == LogFormatKind.StepCsv) { AnalyzeSteps(path, encoding, timeout, context, history); return; }
        if (format is not (LogFormatKind.Trc or LogFormatKind.Asc or LogFormatKind.CanfoxTxt))
            throw new InvalidDataException("Формат не поддерживается для диагностики DTC.");

        var decoder = new LocarusDtcDecoder(timeout);
        var asc = format == LogFormatKind.Asc ? AscFileFormat.Read(path, encoding) : AscFileFormat.Default;
        DateTime? trcStart = format == LogFormatKind.Trc ? TrcLogParser.ParseStartTime(path, encoding) : null;
        double ascOffset = 0;
        int[] bytes = new int[Device.MaxDataLength];
        foreach (string line in LogFileReader.ReadLines(path, encoding, context))
        {
            context.ThrowIfCancellationRequested();
            string id = "";
            int length = 0;
            double time = 0;
            string timeText = "";
            bool parsed = false;
            switch (format)
            {
                case LogFormatKind.Trc:
                    parsed = TrcLogParser.TryParseTrcFrameLine(line, out decimal trcMs, out _, out id, out _, bytes, out length);
                    if (!parsed && DtcFrameBoundary.TryTrc(line, out _, out trcMs, out id)) { parsed = true; length = 0; }
                    time = (double)trcMs;
                    timeText = TrcTimeText(trcStart, time);
                    break;
                case LogFormatKind.Asc:
                    parsed = AscLogParser.TryParseFrameLine(line, asc, out long ticks, out id, bytes, out length);
                    if (!parsed && DtcFrameBoundary.TryAsc(line, asc, out ticks, out id)) { parsed = true; length = 0; }
                    if (parsed && asc.RelativeTimestamps) ascOffset += ticks / (double)TimeSpan.TicksPerMillisecond;
                    time = asc.RelativeTimestamps ? ascOffset : ticks / (double)TimeSpan.TicksPerMillisecond;
                    timeText = asc.BaseTimeTicks is { } baseTicks ? FormatClock(baseTicks / (double)TimeSpan.TicksPerMillisecond + time) : FormatOffset(time);
                    break;
                case LogFormatKind.CanfoxTxt:
                    parsed = CanfoxLogParser.TryParseCanfoxFrameLine(line, out DateOnly date, out double days, out id, bytes, out length);
                    if (!parsed && DtcFrameBoundary.TryCanfox(line, out date, out days, out id)) { parsed = true; length = 0; }
                    time = date.DayNumber * 86400000d + days * 86400000d;
                    timeText = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + " " + FormatClock(days * 86400000d);
                    break;
            }
            if (!parsed) continue;
            bool diagnostic = LocarusDtcDecoder.IsDiagnosticId(id);
            DtcSnapshot? snapshot = decoder.OnFrame(id, bytes.AsSpan(0, length), time);
            if (snapshot != null && length == 0) snapshot = snapshot with { RawData = line };
            if (diagnostic && snapshot != null) history.Add(snapshot, timeText);
            else history.Advance(time, timeText, decoder.SnapshotAt(time));
        }
    }

    private static void AnalyzeMatrix(string path, Encoding encoding, double timeout, ProcessingContext context, History history)
    {
        List<MatrixCsvColumn>? columns = null;
        var tracker = new MatrixCsvTimeTracker();
        var clock = new ClockTracker();
        var decoder = new LocarusDtcDecoder(timeout);
        int[] bytes = new int[8];
        foreach (string line in LogFileReader.ReadLines(path, encoding, context))
        {
            context.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (columns == null)
            {
                if (MatrixCsvLogParser.TryReadHeader(line, out var all, out _))
                    columns = all.Where(c => LocarusDtcDecoder.IsDiagnosticId(c.Id)).OrderBy(c => c.Id, StringComparer.Ordinal).ToList();
                continue;
            }
            string[] fields = line.Split(';');
            if (!tracker.TryAdvance(fields[0], out TimeSpan time)) continue;
            double ms = clock.Advance(time.TotalMilliseconds);
            decoder.ResetCycle();
            foreach (var column in columns)
            {
                if (column.ColumnIndex >= fields.Length || string.IsNullOrWhiteSpace(fields[column.ColumnIndex])) continue;
                decoder.OnFrame(column.Id, MatrixCsvLogParser.TryParsePayloadHex(fields[column.ColumnIndex], bytes)
                    ? bytes : ReadOnlySpan<int>.Empty, ms);
            }
            if (columns.Count > 0) history.Add(decoder.SnapshotAt(ms), FormatClock(ms));
        }
    }

    private static void AnalyzeSteps(string path, Encoding encoding, double timeout, ProcessingContext context, History history)
    {
        var clock = new ClockTracker();
        var partsById = new Dictionary<string, int[]>(StringComparer.Ordinal);
        string timeText = "";
        double time = 0;
        bool haveStep = false, anyDiagnostic = false;
        var decoder = new LocarusDtcDecoder(timeout);
        void Flush()
        {
            if (!haveStep || !anyDiagnostic) return;
            decoder.ResetCycle();
            foreach (var part in partsById.OrderBy(p => p.Key, StringComparer.Ordinal)) decoder.OnFrame(part.Key, part.Value, time);
            history.Add(decoder.SnapshotAt(time), timeText);
        }
        foreach (string line in LogFileReader.ReadLines(path, encoding, context))
        {
            context.ThrowIfCancellationRequested();
            string[] fields = line.Split(';');
            if (fields.Length < 3) continue;
            if (int.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int step))
            {
                Flush();
                partsById.Clear();
                haveStep = true;
                timeText = fields[1];
                time = TimeOfDayParse.TryParse(timeText, out TimeSpan parsed) ? clock.Advance(parsed.TotalMilliseconds) : double.NaN;
            }
            if (fields.Length < StepCsvLogParser.FirstByteColumn || !StepCsvLogParser.IsAccepted(fields[3])
                || !CanToken.TryNormalizeId(fields[2], out string id) || !LocarusDtcDecoder.IsDiagnosticId(id)) continue;
            anyDiagnostic = true;
            if (fields.Length < StepCsvLogParser.FirstByteColumn + 8) { partsById[id] = []; continue; }
            var bytes = new int[8];
            bool valid = true;
            for (int i = 0; i < 8; i++) valid &= StepCsvLogParser.TryParseByte(fields[4 + i], out bytes[i]);
            partsById[id] = valid ? bytes : [];
        }
        Flush();
    }

    private readonly record struct VendorHeader(int Time, int Count, int Errors);

    private static bool TryVendorHeader(string path, Encoding encoding, out VendorHeader header)
    {
        header = default;
        string? first = LogFileReader.ReadLines(path, encoding).FirstOrDefault(l => !string.IsNullOrWhiteSpace(l));
        if (first == null) return false;
        string[] fields = SplitCsv(first);
        int Index(string name) => Array.FindIndex(fields, s => s.Trim().Equals(name, StringComparison.OrdinalIgnoreCase));
        header = new VendorHeader(Index("time"), Index("ErrorsQuantity"), Index("Err"));
        return header.Time >= 0 && header.Count >= 0 && header.Errors >= 0;
    }

    private static void AnalyzeVendor(string path, Encoding encoding, VendorHeader header, ProcessingContext context, History history, List<string> warnings)
    {
        bool skippedHeader = false;
        int malformed = 0, incomplete = 0;
        foreach (string line in LogFileReader.ReadLines(path, encoding, context))
        {
            context.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (!skippedHeader) { skippedHeader = true; continue; }
            string[] fields = SplitCsv(line);
            if (fields.Length <= Math.Max(header.Time, Math.Max(header.Count, header.Errors))
                || !DateTimeOffset.TryParse(fields[header.Time], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var stamp))
            { malformed++; continue; }
            double ms = stamp.UtcTicks / (double)TimeSpan.TicksPerMillisecond;
            DtcSnapshot snapshot = ParseVendorSnapshot(ms, fields[header.Count], fields[header.Errors]);
            if (!snapshot.IsComplete) incomplete++;
            history.Add(snapshot, fields[header.Time]);
        }
        if (malformed > 0) warnings.Add($"{Path.GetFileName(path)}: строк с некорректным временем или структурой: {malformed}.");
        if (incomplete > 0) warnings.Add($"{Path.GetFileName(path)}: неполных диагностических снимков: {incomplete}.");
    }

    internal static DtcSnapshot ParseVendorSnapshot(double timeMs, string countText, string errorsText)
    {
        int? count = int.TryParse(countText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) && n >= 0 ? n : null;
        string raw = errorsText.Trim().Trim('"');
        string[] values = raw is "" or "-" ? [] : raw.Split(['|', ','], StringSplitOptions.None);
        // A trailing list separator is common in the producer's output, but an
        // empty position in the middle must retain its slot and remain unknown.
        while (values.Length > 0 && string.IsNullOrWhiteSpace(values[^1])) values = values[..^1];
        var slots = Enumerable.Repeat(DtcSlot.Unknown, 6).ToArray();
        bool valid = true;
        for (int i = 0; i < Math.Min(6, values.Length); i++)
        {
            string[] pair = values[i].Trim().Split('.');
            if (pair.Length == 2
                && int.TryParse(pair[0], NumberStyles.None, CultureInfo.InvariantCulture, out int spn) && spn is >= 0 and <= 524287
                && int.TryParse(pair[1], NumberStyles.None, CultureInfo.InvariantCulture, out int fmi) && fmi is >= 0 and <= 31)
                slots[i] = DtcSlot.Present(new DtcCode(spn, fmi));
            else valid = false;
        }
        bool complete = count is >= 0 and <= 6 && values.Length == count && valid
            && slots.Where(s => s.Code != null).Select(s => s.Code).Distinct().Count() == count;
        if (complete)
            for (int i = count!.Value; i < 6; i++) slots[i] = DtcSlot.Empty;
        return new DtcSnapshot(timeMs, slots, count, complete, $"ErrorsQuantity={countText}; Err={errorsText}");
    }

    private static string[] SplitCsv(string line)
    {
        var cells = new List<string>();
        var cell = new StringBuilder();
        bool quoted = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"') { cell.Append('"'); i++; }
                else quoted = !quoted;
            }
            else if (c == ';' && !quoted) { cells.Add(cell.ToString()); cell.Clear(); }
            else cell.Append(c);
        }
        cells.Add(cell.ToString());
        return cells.ToArray();
    }

    private static string FormatOffset(double ms) => ms.ToString("0.###", CultureInfo.InvariantCulture) + " мс";
    private static string FormatClock(double ms) => MatrixCsvLogParser.FormatTimeWithMs(TimeSpan.FromMilliseconds(ms));
    private static string TrcTimeText(DateTime? start, double time)
    {
        try { return start?.AddMilliseconds(time).ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) ?? FormatOffset(time); }
        catch (ArgumentOutOfRangeException) { return FormatOffset(time); }
    }

    // A midnight crossing is part of the source clock, unlike small backwards
    // jumps which History treats as a discontinuity.
    private sealed class ClockTracker
    {
        private double _last = double.NaN, _days;
        public double Advance(double ms)
        {
            if (!double.IsNaN(_last) && ms - _last < -43200000) _days += 86400000;
            _last = ms;
            return _days + ms;
        }
    }

    private sealed class History(string file, double timeout, List<DtcObservation> observations,
        List<DtcEpisode> episodes, List<DtcSummary> summaries)
    {
        private readonly Dictionary<DtcCode, (string First, string Last, double LastMs)> _active = [];
        private readonly List<DtcEpisode> _fileEpisodes = [];
        private DtcSnapshot? _last;
        public bool HasObservations { get; private set; }
        public bool HasTruncatedList { get; private set; }

        public void Advance(double time, string timeText, DtcSnapshot current)
        {
            if (_last == null) return;
            if (_last.ReportedCount == null && _last.Slots.All(s => !s.IsKnown) && time >= _last.TimeMs) return;
            if (time < _last.TimeMs || time - _last.TimeMs > timeout)
                Add(current, timeText);
        }

        public void Add(DtcSnapshot snapshot, string timeText)
        {
            HasTruncatedList |= snapshot.ReportedCount > 6;
            if (_last != null)
            {
                if (snapshot.TimeMs < _last.TimeMs) CloseAll(null, "Время источника пошло назад");
                else if (snapshot.TimeMs - _last.TimeMs > timeout) CloseAll(null, "Пропуск диагностических данных");
            }
            var present = snapshot.Slots.Where(s => s.Code != null).Select(s => s.Code!.Value).ToHashSet();
            foreach (DtcCode code in _active.Keys.Where(c => snapshot.TimeMs - _active[c].LastMs > timeout).ToArray())
                Close(code, null, "Пропуск диагностических данных");
            foreach (DtcCode code in present)
                _active[code] = _active.TryGetValue(code, out var episode) ? (episode.First, timeText, snapshot.TimeMs) : (timeText, timeText, snapshot.TimeMs);
            if (snapshot.IsComplete)
                foreach (DtcCode code in _active.Keys.Where(c => !present.Contains(c)).ToArray()) Close(code, timeText, "Отсутствует в полном снимке");
            else if (snapshot.ReportedCount == null && snapshot.Slots.All(s => !s.IsKnown))
                CloseAll(null, "Недостаточно данных");
            observations.Add(new DtcObservation(file, timeText, snapshot));
            _last = snapshot;
            HasObservations = true;
        }

        public void Finish()
        {
            CloseAll(null, "Конец файла");
            episodes.AddRange(_fileEpisodes);
            foreach (var group in _fileEpisodes.GroupBy(e => e.Code).OrderBy(g => g.Key.Spn).ThenBy(g => g.Key.Fmi))
            {
                var items = group.ToArray();
                bool present = _last?.Slots.Any(s => s.Code == group.Key) == true;
                string state = present ? "Присутствует в последнем снимке" : _last?.IsComplete == true
                    ? "Отсутствует в полном снимке" : "Недостаточно данных";
                summaries.Add(new DtcSummary(file, group.Key, items[0].FirstSeen, items[^1].LastSeen, items.Length, state));
            }
        }

        private void CloseAll(string? absent, string reason)
        {
            foreach (DtcCode code in _active.Keys.ToArray()) Close(code, absent, reason);
        }

        private void Close(DtcCode code, string? absent, string reason)
        {
            var episode = _active[code];
            _fileEpisodes.Add(new DtcEpisode(file, code, episode.First, episode.Last, absent, reason));
            _active.Remove(code);
        }
    }
}
