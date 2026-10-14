using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using logReader;
using logReader.Processing;

CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ru-RU");
string work = Path.GetFullPath(args[0]);
string? realRoot = args.Length > 1 && args[1] != "-" ? Path.GetFullPath(args[1]) : null;
int dbcArgument = Array.IndexOf(args, "--dbc");
string? suppliedDbc = dbcArgument >= 0 && dbcArgument + 1 < args.Length ? Path.GetFullPath(args[dbcArgument + 1]) : null;
bool baseline = args.Contains("--baseline");
if (realRoot != null && (work.Equals(realRoot, StringComparison.OrdinalIgnoreCase)
    || work.StartsWith(realRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
    throw new InvalidOperationException("Test outputs must be outside the read-only corpus.");
Directory.CreateDirectory(work);
var checks = new List<object>();
int failures = 0;
var assembly = typeof(Device).Assembly;
var serviceType = assembly.GetType("logReader.Processing.LogProcessingService")!;
object service = Activator.CreateInstance(serviceType, true)!;
var process = serviceType.GetMethod("ProcessSingleFile")!;
string dbc = Path.Combine(work, "devices.dbc");
File.WriteAllText(dbc, "BO_ 2365521928 A: 8 X\n SG_ A1 : 0|8@1+ (1,0) [0|255] \"\" X\n");
var frames = new List<(int Ms, string Id, byte[] Data)>();
AddCycle(1000, 2, new byte[] { 2, 128, 0, 47, 251, 243 });
frames.Add((1010, "0CFF0008", new byte[] { 17, 0, 0, 0, 0, 0, 0, 0 }));
AddCycle(1200, 2, new byte[] { 47, 251, 243, 2, 128, 0 });
frames.Add((1210, "0CFF0008", new byte[] { 18, 0, 0, 0, 0, 0, 0, 0 }));
AddCycle(1400, 0, Array.Empty<byte>());
frames.Add((4010, "0CFF0008", new byte[] { 19, 0, 0, 0, 0, 0, 0, 0 }));
string Hex(byte[] data) => string.Join(" ", data.Select(b => b.ToString("X2")));
string Clock(int ms) => TimeSpan.FromHours(12).Add(TimeSpan.FromMilliseconds(ms)).ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);
var fixtures = new Dictionary<string, string>
{
    ["trc"] = "; Start time: 14.10.2026 12:00:00.000\n" + string.Join("\n", frames.Select((f, i) => $"{i + 1}) {f.Ms} Rx {f.Id} 8 {Hex(f.Data)}")),
    ["asc"] = "date Wed Oct 14 12:00:00.000 2026\nbase hex timestamps absolute\n" + string.Join("\n", frames.Select(f => FormattableString.Invariant($"{f.Ms / 1000d:F3} 1 {f.Id}x Rx d 8 {Hex(f.Data)}"))),
    ["txt"] = "Date Time ID Len Data\n" + string.Join("\n", frames.Select(f => $"2026.10.14 {Clock(f.Ms)} - {f.Id} 8 {Hex(f.Data)}")),
    ["legacy.csv"] = string.Join("\n", frames.Select(f => $"{(f.Id == "18FE2503" || f.Ms == 4010 ? (f.Ms / 200).ToString() : "")};{Clock(f.Ms)};{f.Id};1;{string.Join(";", f.Data)}")),
    ["matrix.csv"] = ";18FE2703;0CFF0008;18FE2503;18FE2603\n" + string.Join("\n", new[] { 1000, 1200, 1400 }.Select(ms =>
        $"{Clock(ms)};{Convert.ToHexString(frames.First(f => f.Ms == ms + 2).Data)};1100000000000000;{Convert.ToHexString(frames.First(f => f.Ms == ms).Data)};{Convert.ToHexString(frames.First(f => f.Ms == ms + 1).Data)}"))
};
foreach (var fixture in fixtures)
{
    string input = Path.Combine(work, "fixture." + fixture.Key);
    File.WriteAllText(input, fixture.Value + "\n");
    foreach (var format in new[] { OutputFormat.Csv, OutputFormat.Xlsx })
    {
        Run(fixture.Key + "-off-" + format, input, dbc, format, false);
        if (!baseline) Run(fixture.Key + "-on-" + format, input, dbc, format, true);
    }
    if (fixture.Key == "trc")
    {
        Run("dst-off", input, dbc, OutputFormat.CsvDstConnect, false);
        if (!baseline) Run("dst-on", input, dbc, OutputFormat.CsvDstConnect, true);
    }
    if (!baseline) Analyze("fixture-analysis-" + fixture.Key, input);
}
var sourceHashes = new Dictionary<string, string>();
if (!baseline && realRoot != null)
{
    string realDbc = suppliedDbc ?? dbc;
    if (suppliedDbc != null) sourceHashes[realDbc] = Hash(realDbc);
    string[] realTraces = Directory.GetFiles(realRoot, "*.trc", SearchOption.TopDirectoryOnly).Order().ToArray();
    if (realTraces.Length == 0) throw new InvalidOperationException("The specified DTC log directory has no TRC files.");
    foreach (string input in realTraces)
    {
        sourceHashes[input] = Hash(input);
        Analyze("real-trc-analysis-" + Path.GetFileNameWithoutExtension(input), input);
        Run("real-trc-output-" + Path.GetFileNameWithoutExtension(input), input, realDbc, OutputFormat.Csv, true);
    }
    foreach (BatchOutputMode mode in Enum.GetValues<BatchOutputMode>()) RunBatch(mode, realTraces, realDbc);
    Run("real-active-xlsx", realTraces.Single(p => Path.GetFileName(p).StartsWith("контрвращение")), realDbc, OutputFormat.Xlsx, true);
    Run("real-empty-trace-xlsx", realTraces.Single(p => Path.GetFileName(p).StartsWith("trace_2025-10-16")), realDbc, OutputFormat.Xlsx, true);
    Analyze("real-trc-folder", realRoot);
}
bool unchanged = sourceHashes.All(item => Hash(item.Key) == item.Value);
if (!unchanged) failures++;
File.WriteAllText(Path.Combine(work, "audit.json"), JsonSerializer.Serialize(new
{
    CoreSha256 = Hash(assembly.Location), Baseline = baseline, Failed = failures, SourceFilesUnchanged = unchanged,
    SourceHashes = sourceHashes.Select(p => new { File = Path.GetFileName(p.Key), Sha256 = p.Value }), Checks = checks
}, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"AUDIT: {checks.Count} cases, {failures} failures; source files unchanged={unchanged}");
Environment.ExitCode = failures == 0 ? 0 : 1;

void AddCycle(int ms, int count, byte[] codes)
{
    var packed = new byte[18]; codes.CopyTo(packed, 0);
    frames.Add((ms, "18FE2503", new byte[] { 0, 0, 0, (byte)count, 0, 0, packed[0], packed[1] }));
    frames.Add((ms + 1, "18FE2603", packed[2..10]));
    frames.Add((ms + 2, "18FE2703", packed[10..18]));
}
void Run(string key, string input, string description, OutputFormat format, bool includeDtc)
{
    var messages = new List<string>();
    string output = Path.Combine(work, key + (format == OutputFormat.Xlsx ? ".xlsx" : ".csv"));
    try
    {
        var settings = new OutputSettings { Format = format, DstConnect = format == OutputFormat.CsvDstConnect
            ? new DstConnectOptions { BlockPeriodMs = 200, BlockStartIndex = 1 } : new DstConnectOptions() };
        typeof(OutputSettings).GetProperty("IncludeDtc")?.SetValue(settings, includeDtc);
        var result = (ProcessingResult)process.Invoke(service, new object[] { input, output, DeviceFiles.LoadDevices(description), settings, new ProcessingContext(messages.Add) })!;
        if (!result.Success) throw new InvalidOperationException(result.Error);
        string fingerprint;
        string[] headers;
        int dtcColumns;
        if (format == OutputFormat.Xlsx)
        {
            using var book = new XLWorkbook(output);
            var cells = book.Worksheet(1).CellsUsed().ToArray();
            dtcColumns = cells.Count(c => c.GetString() is "DTC1" or "DTC2" or "DTC3" or "DTC4" or "DTC5" or "DTC6");
            if (includeDtc && cells.Any(c => c.GetString().StartsWith("SPN ") && c.DataType != XLDataType.Text)) throw new InvalidOperationException("DTC cell is not text.");
            if (includeDtc && cells.Any(c => c.GetString() is "Н/Д" or "Количество ошибок DTC" or "Полнота DTC"))
                throw new InvalidOperationException("Diagnostic metadata must not be exported.");
            if (includeDtc && Path.GetFileName(input).StartsWith("fixture.") && !cells.Any(c => c.GetString() == "SPN 32770 / FMI 0"))
                throw new InvalidOperationException("Expected SPN/FMI text is missing from fixture output.");
            headers = book.Worksheet(1).Row(1).CellsUsed().Select(c => c.GetString()).ToArray();
            var contents = new StringBuilder();
            foreach (var sheet in book.Worksheets)
                foreach (var cell in sheet.CellsUsed()) contents.AppendLine($"{sheet.Name}|{cell.Address}|{cell.DataType}|{cell.Value}");
            fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(contents.ToString())));
        }
        else
        {
            headers = File.ReadLines(output).First().Split(';');
            dtcColumns = headers.Count(h => h is "DTC1" or "DTC2" or "DTC3" or "DTC4" or "DTC5" or "DTC6");
            fingerprint = Hash(output);
            if (includeDtc && File.ReadLines(output).SelectMany(line => line.Split(';')).Any(cell => cell is "Н/Д" or "Количество ошибок DTC" or "Полнота DTC"))
                throw new InvalidOperationException("Diagnostic metadata must not be exported.");
            if (includeDtc && Path.GetFileName(input).StartsWith("fixture.") && !File.ReadAllText(output).Contains("SPN 32770 / FMI 0"))
                throw new InvalidOperationException("Expected SPN/FMI text is missing from fixture output.");
        }
        if (dtcColumns != (includeDtc ? 6 : 0)) throw new InvalidOperationException($"Expected fixed six DTC columns, got {dtcColumns}.");
        checks.Add(new { Key = key, Status = "PASS", result.RowsWritten, Hash = fingerprint, Headers = headers, Messages = messages });
        Console.WriteLine($"PASS {key}: {result.RowsWritten}");
    }
    catch (Exception ex)
    {
        failures++;
        checks.Add(new { Key = key, Status = "FAIL", Error = (ex.InnerException ?? ex).Message, Messages = messages });
        Console.WriteLine($"FAIL {key}: {(ex.InnerException ?? ex).Message}");
    }
}
void Analyze(string key, string input)
{
    try
    {
        var analyzer = assembly.GetType("logReader.Processing.DtcAnalysisService")!;
        object result = analyzer.GetMethod("Analyze", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, new object[] { input, 2000d, new ProcessingContext() })!;
        var json = JsonSerializer.SerializeToElement(result, result.GetType());
        int observations = json.GetProperty("Observations").GetArrayLength();
        var summaries = json.GetProperty("Summaries");
        if (observations == 0) throw new InvalidOperationException("Real input has no diagnostic observations.");
        if (key.StartsWith("fixture-analysis-") && !summaries.EnumerateArray().Any(s => s.GetProperty("Code").GetProperty("Spn").GetInt32() == 32770 && s.GetProperty("Code").GetProperty("Fmi").GetInt32() == 0))
            throw new InvalidOperationException("Fixture SPN32770/FMI0 was not decoded.");
        if (key.EndsWith("контрвращение_085008") && !summaries.EnumerateArray().Any(s => s.GetProperty("Code").GetProperty("Spn").GetInt32() == 32770 && s.GetProperty("Code").GetProperty("Fmi").GetInt32() == 0))
            throw new InvalidOperationException("Real SPN32770/FMI0 was not decoded.");
        var states = json.GetProperty("Observations").EnumerateArray().Select(o => o.GetProperty("Snapshot")).ToArray();
        int complete = states.Count(s => s.GetProperty("IsComplete").GetBoolean());
        if (key.StartsWith("real-trc-analysis-") && !key.EndsWith("контрвращение_085008") && summaries.GetArrayLength() != 0)
            throw new InvalidOperationException("Unexpected DTC in a raw DST log whose six packed slots are zero.");
        if (key.EndsWith("trace_2025-10-16T06_45_04_085033") && (complete != 1038 || summaries.GetArrayLength() != 0))
            throw new InvalidOperationException("Trace should contain 1038 complete zero-error cycles.");
        if (key.EndsWith("контрвращение_085008") && states.Count(s => s.GetProperty("IsComplete").GetBoolean()
            && s.GetProperty("Slots").EnumerateArray().Any(slot => slot.GetProperty("Code").ValueKind == JsonValueKind.Object
                && slot.GetProperty("Code").GetProperty("Spn").GetInt32() == 32770 && slot.GetProperty("Code").GetProperty("Fmi").GetInt32() == 0)) != 15)
            throw new InvalidOperationException("Counterrotation should confirm SPN32770/FMI0 in exactly 15 complete cycles.");
        checks.Add(new { Key = key, Status = "PASS", Observations = observations, CompleteCycles = complete,
            Summaries = summaries, Episodes = json.GetProperty("Episodes").GetArrayLength(), Warnings = json.GetProperty("Warnings") });
        Console.WriteLine($"PASS {key}: {observations} observations, {summaries.GetArrayLength()} codes");
    }
    catch (Exception ex) { failures++; checks.Add(new { Key = key, Status = "FAIL", Error = (ex.InnerException ?? ex).Message }); Console.WriteLine($"FAIL {key}: {(ex.InnerException ?? ex).Message}"); }
}
void RunBatch(BatchOutputMode mode, string[] inputs, string description)
{
    string key = "real-batch-" + mode;
    try
    {
        string folder = Path.Combine(work, key);
        Directory.CreateDirectory(folder);
        var method = serviceType.GetMethod("ProcessFolderBatch")!;
        object result = method.Invoke(service, new object[] { inputs, folder, Path.GetFullPath(description), mode,
            DeviceFiles.LoadDevices(description), new OutputSettings { IncludeDtc = true }, new ProcessingContext() })!;
        var json = JsonSerializer.SerializeToElement(result, result.GetType());
        if (json.GetProperty("Failed").GetInt32() != 0 || json.GetProperty("Created").GetInt32() == 0)
            throw new InvalidOperationException("Batch failed or produced no output.");
        string[] files = Directory.GetFiles(folder, "*.csv");
        if (files.Any(p => File.ReadLines(p).First().Split(';').Count(h => h is "DTC1" or "DTC2" or "DTC3" or "DTC4" or "DTC5" or "DTC6") != 6))
            throw new InvalidOperationException("Batch output does not have six DTC columns.");
        if (!files.Any(p => File.ReadAllText(p).Contains("SPN 32770 / FMI 0")))
            throw new InvalidOperationException("Batch lost the real FMI0 code.");
        checks.Add(new { Key = key, Status = "PASS", Outcome = json, OutputCount = files.Length });
        Console.WriteLine($"PASS {key}: {files.Length} outputs");
    }
    catch (Exception ex) { failures++; checks.Add(new { Key = key, Status = "FAIL", Error = (ex.InnerException ?? ex).Message }); Console.WriteLine($"FAIL {key}: {(ex.InnerException ?? ex).Message}"); }
}
static string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
