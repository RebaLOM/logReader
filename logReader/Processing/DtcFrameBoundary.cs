using System.Globalization;

namespace logReader.Processing;

// A damaged payload must still delimit the diagnostic cycle. These readers only
// recognize existing frame headers; numeric processing continues to require a
// fully valid frame from the ordinary parsers.
internal static class DtcFrameBoundary
{
    internal static bool TryTrc(string line, out int index, out decimal timeMs, out string id)
        => TrcLogParser.TryParseTrcFrameLine(line, out index, out timeMs, out _, out id, out _, Span<int>.Empty, out _)
            && LocarusDtcDecoder.IsDiagnosticId(id);

    internal static bool TryCanfox(string line, out DateOnly date, out double timeFraction, out string id)
    {
        date = default;
        timeFraction = 0;
        id = "";
        string[] tokens = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length > 5 && tokens[5].Equals("RTR", StringComparison.OrdinalIgnoreCase)) return false;
        return CanfoxLogParser.TryParseCanfoxFrameLine(line, out date, out timeFraction, out id, Span<int>.Empty, out _)
            && LocarusDtcDecoder.IsDiagnosticId(id);
    }

    internal static bool TryAsc(string line, AscFileFormat format, out long offsetTicks, out string id)
    {
        if (AscLogParser.TryParseFrameLine(line, format, out offsetTicks, out id, Span<int>.Empty, out _))
            return LocarusDtcDecoder.IsDiagnosticId(id);

        // The normal CAN FD header finder also checks its first data byte. When
        // that byte is corrupt or absent, validate the same header with a dummy
        // data byte, without ever treating the dummy byte as diagnostic data.
        string[] tokens = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 9 || !tokens[1].Equals("CANFD", StringComparison.OrdinalIgnoreCase)) return false;
        for (int i = 5; i <= tokens.Length - 4; i++)
        {
            if (!int.TryParse(tokens[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int brs) || brs is < 0 or > 1
                || !int.TryParse(tokens[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int esi) || esi is < 0 or > 1
                || !int.TryParse(tokens[i + 2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int dlc) || dlc is < 0 or > 15
                || !int.TryParse(tokens[i + 3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int dataLength) || dataLength is < 0 or > 64)
                continue;
            string header = string.Join(' ', tokens.Take(i + 4)) + " 00";
            return AscLogParser.TryParseFrameLine(header, format, out offsetTicks, out id, Span<int>.Empty, out _)
                && LocarusDtcDecoder.IsDiagnosticId(id);
        }
        return false;
    }
}
