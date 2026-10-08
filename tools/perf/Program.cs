using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using logReader;
using logReader.Processing;

// Calls the existing compiled processing pipeline. Reflection keeps the benchmark
// independent of production assembly visibility and does not alter its algorithms.
CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ru-RU");
var repo = Path.GetFullPath(Argument("--root") ?? Path.Combine(AppContext.BaseDirectory, "../../../../../"));
var label = Argument("--label") ?? "baseline";
var output = Path.GetFullPath(Argument("--output") ?? Path.Combine(repo, "docs/loger-2/performance", label + ".json"));
var work = Path.Combine(repo, "artifacts/perf", label);
int runs = int.Parse(Argument("--runs") ?? "5", CultureInfo.InvariantCulture);
if (runs < 3) throw new ArgumentException("Use at least three measured repetitions.");
Directory.CreateDirectory(work);
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
var bridge = new CoreBridge();
var devicesPath = Path.Combine(work, "devices.dbc");
File.WriteAllText(devicesPath, Fixtures.Dbc(), new UTF8Encoding(false));
var devices = DeviceFiles.LoadDevices(devicesPath);
var settings = new OutputSettings { Format = OutputFormat.Csv, IncludeDeviceIdHeaderRow = true };
var xlsxSettings = new OutputSettings { Format = OutputFormat.Xlsx, IncludeDeviceIdHeaderRow = true };
var small = Fixtures.Generate(work, "small", 2_000, new DateTime(2026, 10, 8, 23, 59, 59));
// Matrix → ASC takes its header date from the input's last-write date.
// Fix that metadata too, so reruns on a later day produce identical output.
File.SetLastWriteTime(small.Matrix!, new DateTime(2026, 10, 9, 10, 0, 0));
var large = Fixtures.Generate(work, "large", 100_000, new DateTime(2026, 10, 9, 10, 0, 0), additionalFormats: false);
var batchInputs = new[] { small.Trc, Fixtures.Generate(work, "batch-next", 2_000, new DateTime(2026, 10, 9, 0, 0, 15), false).Trc };
var scenarios = new List<Scenario>();
foreach (var fixture in new[] { small, large })
{
    scenarios.Add(new Scenario($"{fixture.Name}-trc-parse-decode-aggregate", fixture.Frames, () =>
    {
        var aggregate = bridge.Aggregate(new[] { fixture.Trc }, devices, settings);
        return new Outcome(aggregate.Rows, () =>
        {
            string verify = Path.Combine(work, "verify-aggregate.csv");
            bridge.Write(aggregate.Data, verify, settings);
            return Fingerprint.File(verify);
        });
    }));
    var collected = bridge.Aggregate(new[] { fixture.Trc }, devices, settings);
    foreach (var format in new[] { OutputFormat.Csv, OutputFormat.Xlsx })
    {
        var exportSettings = format == OutputFormat.Csv ? settings : xlsxSettings;
        string path = Path.Combine(work, $"{fixture.Name}-export.{format.ToString().ToLowerInvariant()}");
        scenarios.Add(new Scenario($"{fixture.Name}-{format.ToString().ToLowerInvariant()}-export", fixture.Frames, () =>
        {
            var result = bridge.Write(collected.Data, path, exportSettings);
            return new Outcome(result.RowsWritten, () => Fingerprint.Output(path));
        }));
    }
}
AddPipeline("large-trc-csv-pipeline", large.Trc, large.Frames, settings);
AddPipeline("small-asc-csv-pipeline", small.Asc!, small.Frames, settings);
AddPipeline("small-legacy-csv-pipeline", small.Legacy!, small.Frames, settings);
AddPipeline("small-matrix-csv-pipeline", small.Matrix!, small.Frames, settings);
AddPipeline("small-canfox-csv-pipeline", small.Canfox!, small.Frames, settings);
AddPipeline("small-dst-csv-pipeline", small.Trc, small.Frames,
    new OutputSettings { Format = OutputFormat.CsvDstConnect, DstConnect = new DstConnectOptions { BlockPeriodMs = 20 } });
foreach (var mode in new[] { BatchOutputMode.PerInputFile, BatchOutputMode.MergeToSingleFile, BatchOutputMode.SplitTrcByDate })
{
    string folder = Path.Combine(work, "batch-" + mode);
    Directory.CreateDirectory(folder);
    scenarios.Add(new Scenario("batch-trc-" + mode, 4_000, () =>
    {
        var counts = bridge.Batch(batchInputs, folder, devicesPath, mode, devices, settings);
        if (counts.Failed != 0 || counts.Created != counts.Expected || counts.Created == 0)
            throw new InvalidOperationException($"Unexpected batch outcome: {counts}");
        return new Outcome(counts.Created, () => Fingerprint.Folder(folder));
    }));
}
foreach (var item in new[] { ("small-trc-to-asc", small.Trc, "TrcToAscConverter"), ("small-matrix-to-asc", small.Matrix!, "MatrixCsvToAscConverter") })
{
    string path = Path.Combine(work, item.Item1 + ".asc");
    scenarios.Add(new Scenario(item.Item1, small.Frames, () =>
    {
        var result = bridge.Convert(item.Item3, item.Item2, path);
        return new Outcome(result.RowsWritten, () => Fingerprint.File(path));
    }));
}

var reports = new List<ScenarioReport>();
foreach (var scenario in scenarios)
{
    Console.WriteLine($"{scenario.Name}: warm-up + {runs} measured runs");
    _ = scenario.Run().Verify();
    var samples = new List<Sample>();
    string? expectedHash = null;
    long? expectedRows = null;
    for (int i = 0; i < runs; i++)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        using var process = Process.GetCurrentProcess();
        var cpuBefore = process.TotalProcessorTime;
        long allocationsBefore = GC.GetTotalAllocatedBytes(precise: true);
        long managedBefore = GC.GetTotalMemory(false);
        var watch = Stopwatch.StartNew();
        var outcome = scenario.Run();
        watch.Stop();
        var cpu = process.TotalProcessorTime - cpuBefore;
        long allocated = GC.GetTotalAllocatedBytes(precise: true) - allocationsBefore;
        long managedAfter = GC.GetTotalMemory(false);
        process.Refresh();
        long workingSet = process.WorkingSet64;
        long processPeak = process.PeakWorkingSet64;
        string hash = outcome.Verify(); // Validation is deliberately outside measured work.
        expectedHash ??= hash;
        expectedRows ??= outcome.Rows;
        if (hash != expectedHash || expectedRows != outcome.Rows)
            throw new InvalidOperationException($"Non-deterministic result in {scenario.Name}.");
        samples.Add(new Sample(watch.Elapsed.TotalMilliseconds, cpu.TotalMilliseconds, allocated,
            managedBefore, managedAfter, workingSet, processPeak, outcome.Rows, hash));
    }
    var report = new ScenarioReport(scenario.Name, scenario.InputFrames, Median(samples.Select(s => s.ElapsedMs)),
        Median(samples.Select(s => s.CpuMs)), Median(samples.Select(s => (double)s.AllocatedBytes)),
        Median(samples.Select(s => (double)s.WorkingSetAfterBytes)), expectedRows!.Value, expectedHash!, samples);
    reports.Add(report);
    Console.WriteLine($"  median {report.MedianElapsedMs:F2} ms, allocations {report.MedianAllocatedBytes / 1048576:F1} MiB, rows {report.Rows}");
}
var inputs = Directory.GetFiles(work, "*", SearchOption.TopDirectoryOnly)
    .Where(p => p == devicesPath || p.EndsWith(".trc") || p == small.Asc || p == small.Matrix || p == small.Legacy || p == small.Canfox)
    .OrderBy(p => p, StringComparer.Ordinal).Select(p => new InputReport(Path.GetFileName(p), new FileInfo(p).Length, Fingerprint.File(p))).ToList();
var benchmark = new BenchmarkReport(label, DateTime.UtcNow, RuntimeInformation.FrameworkDescription,
    RuntimeInformation.OSDescription, Environment.ProcessorCount, System.Runtime.GCSettings.IsServerGC,
    runs, 1, Fingerprint.CoreSource(repo), Fingerprint.File(typeof(Device).Assembly.Location), inputs, reports);
File.WriteAllText(output, JsonSerializer.Serialize(benchmark, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
Console.WriteLine($"Report: {output}");

void AddPipeline(string name, string input, int frames, OutputSettings currentSettings)
{
    string path = Path.Combine(work, name + ".csv");
    scenarios.Add(new Scenario(name, frames, () =>
    {
        var result = bridge.Process(input, path, devices, currentSettings);
        return new Outcome(result.RowsWritten, () => Fingerprint.File(path));
    }));
}
string? Argument(string key)
{
    int index = Array.IndexOf(args, key);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}
static double Median(IEnumerable<double> values)
{
    var sorted = values.Order().ToArray();
    return sorted.Length % 2 == 1 ? sorted[sorted.Length / 2] : (sorted[sorted.Length / 2 - 1] + sorted[sorted.Length / 2]) / 2;
}

record Scenario(string Name, int InputFrames, Func<Outcome> Run);
record Outcome(long Rows, Func<string> Verify);
record Sample(double ElapsedMs, double CpuMs, long AllocatedBytes, long ManagedBeforeBytes, long ManagedAfterBytes,
    long WorkingSetAfterBytes, long ProcessLifetimePeakWorkingSetBytes, long Rows, string Hash);
record ScenarioReport(string Scenario, int InputFrames, double MedianElapsedMs, double MedianCpuMs,
    double MedianAllocatedBytes, double MedianWorkingSetAfterBytes, long Rows, string Hash, List<Sample> Samples);
record InputReport(string Name, long Bytes, string Sha256);
record BenchmarkReport(string Label, DateTime RecordedUtc, string Runtime, string OperatingSystem, int LogicalProcessors,
    bool ServerGc, int MeasuredRuns, int WarmupRuns, string CoreSourceSha256, string CoreAssemblySha256,
    List<InputReport> Inputs, List<ScenarioReport> Scenarios);

sealed class CoreBridge
{
    private readonly Assembly _assembly = typeof(Device).Assembly;
    private readonly object _service;
    private readonly MethodInfo _single;
    private readonly MethodInfo _batch;
    private readonly MethodInfo _aggregate;
    private readonly MethodInfo _writer;
    public CoreBridge()
    {
        var serviceType = Type("LogProcessingService");
        _service = Activator.CreateInstance(serviceType, nonPublic: true)!;
        _single = serviceType.GetMethod("ProcessSingleFile")!;
        _batch = serviceType.GetMethod("ProcessFolderBatch")!;
        _aggregate = Type("TrcBatchAggregator").GetMethod("TryBuildMergedAggregate", BindingFlags.Static | BindingFlags.NonPublic)!;
        _writer = Type("TimeSeriesOutputWriter").GetMethod("Write", BindingFlags.Static | BindingFlags.Public)!;
    }
    private Type Type(string name) => _assembly.GetType("logReader.Processing." + name, throwOnError: true)!;
    public ProcessingResult Process(string input, string output, List<Device> devices, OutputSettings settings)
        => Require(_single.Invoke(_service, new object[] { input, output, devices, settings, new ProcessingContext() }));
    public (object Data, long Rows) Aggregate(IReadOnlyList<string> inputs, List<Device> devices, OutputSettings settings)
    {
        object?[] arguments = { inputs, devices, settings, new ProcessingContext(), null };
        var result = Require(_aggregate.Invoke(null, arguments));
        return (arguments[4]!, result.RowsWritten);
    }
    public ProcessingResult Write(object data, string path, OutputSettings settings)
        => Require(_writer.Invoke(null, new[] { data, path, settings, "Benchmark", new ProcessingContext() }));
    public (int Created, int Expected, int Failed) Batch(IReadOnlyList<string> inputs, string folder, string devicesPath,
        BatchOutputMode mode, List<Device> devices, OutputSettings settings)
    {
        object result = _batch.Invoke(_service, new object[] { inputs, folder, devicesPath, mode, devices, settings, new ProcessingContext() })!;
        int Property(string name) => (int)result.GetType().GetProperty(name)!.GetValue(result)!;
        return (Property("Created"), Property("Expected"), Property("Failed"));
    }
    public ProcessingResult Convert(string converter, string input, string output)
    {
        var type = Type(converter);
        return Require(type.GetMethod("Convert")!.Invoke(Activator.CreateInstance(type, true), new object[] { input, output, new ProcessingContext() }));
    }
    private static ProcessingResult Require(object? value)
    {
        var result = (ProcessingResult)value!;
        if (!result.Success) throw new InvalidOperationException(result.Error);
        return result;
    }
}

static class Fixtures
{
    private static string Id(int device) => (0x0CFF0008u + (uint)device).ToString("X8", CultureInfo.InvariantCulture);
    public static string Dbc()
    {
        var text = new StringBuilder();
        for (int d = 0; d < 4; d++)
        {
            text.AppendLine($"BO_ {0x8CFF0008u + (uint)d} Device{d}: 8 X");
            text.AppendLine($" SG_ D{d}_Raw : 0|8@1+ (1,0) [0|255] \"\" X");
            text.AppendLine($" SG_ D{d}_Scaled : 8|16@1+ (0.1,-100) [-100|6453.5] \"unit\" X");
            text.AppendLine($" SG_ D{d}_Signed : 31|16@0- (0.5,2) [-16382|16385.5] \"unit\" X");
        }
        return text.ToString().Replace("\r\n", "\n");
    }
    public static Fixture Generate(string folder, string name, int frames, DateTime start, bool additionalFormats = true)
    {
        string trc = Path.Combine(folder, name + ".trc");
        string? asc = additionalFormats ? Path.Combine(folder, name + ".asc") : null;
        string? legacy = additionalFormats ? Path.Combine(folder, name + "-legacy.csv") : null;
        string? matrix = additionalFormats ? Path.Combine(folder, name + "-matrix.csv") : null;
        string? canfox = additionalFormats ? Path.Combine(folder, name + "-canfox.txt") : null;
        using var trcWriter = Writer(trc);
        using var ascWriter = asc == null ? null : Writer(asc);
        using var legacyWriter = legacy == null ? null : Writer(legacy);
        using var matrixWriter = matrix == null ? null : Writer(matrix);
        using var canfoxWriter = canfox == null ? null : Writer(canfox);
        trcWriter.WriteLine("; Start time: " + start.ToString("dd.MM.yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture));
        ascWriter?.WriteLine("date Thu Oct 8 23:59:59.000 2026\nbase hex timestamps absolute\nno internal events logged");
        matrixWriter?.WriteLine(";" + string.Join(';', Enumerable.Range(0, 4).Select(Id)));
        canfoxWriter?.WriteLine("Date Time - ID Len Data");
        for (int i = 0; i < frames; i++)
        {
            int d = i % 4;
            var bytes = Enumerable.Range(0, 8).Select(b => (i * (17 + b * 2) + b * 13) & 255).ToArray();
            string payload = string.Join(' ', bytes.Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));
            string time = (i * 5 + 1).ToString(CultureInfo.InvariantCulture);
            trcWriter.WriteLine($"{i + 1,7}) {time}.0 Rx {Id(d)} 8 {payload}");
            if (i % 257 == 0) trcWriter.WriteLine("invalid frame deliberately skipped");
            ascWriter?.WriteLine($"{(i * .005 + .001).ToString("F6", CultureInfo.InvariantCulture)} 1 {Id(d)}x Rx d 8 {payload}");
            string timeOfDay = start.AddMilliseconds(i * 5 + 1).ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
            legacyWriter?.WriteLine($"{i + 1};{timeOfDay};{Id(d)};{(i % 7 == 6 ? 0 : 1)};{string.Join(';', bytes)}");
            var cells = new string[4];
            cells[d] = string.Concat(bytes.Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));
            matrixWriter?.WriteLine(timeOfDay + ";" + string.Join(';', cells));
            var instant = start.AddMilliseconds(i * 5 + 1);
            canfoxWriter?.WriteLine($"{instant:yyyy.MM.dd} {instant:HH:mm:ss.fff} - {Id(d)} 8 {payload}");
        }
        return new Fixture(name, frames, trc, asc, legacy, matrix, canfox);
    }
    private static StreamWriter Writer(string path) => new(path, false, new UTF8Encoding(false)) { NewLine = "\n" };
}
record Fixture(string Name, int Frames, string Trc, string? Asc, string? Legacy, string? Matrix, string? Canfox);

static class Fingerprint
{
    public static string File(string path)
    {
        using var input = System.IO.File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(input));
    }
    public static string Output(string path) => path.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ? Workbook(path) : File(path);
    public static string Folder(string path)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (string file in Directory.GetFiles(path).OrderBy(Path.GetFileName, StringComparer.Ordinal))
            Append(hash, Path.GetFileName(file) + "|" + Output(file));
        return Convert.ToHexString(hash.GetHashAndReset());
    }
    public static string CoreSource(string repo)
    {
        string root = Path.Combine(repo, "logReader");
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Where(p => (p.EndsWith(".cs") || p.EndsWith(".csproj"))
                && !Path.GetRelativePath(root, p).Split(Path.DirectorySeparatorChar).Any(s => s is "bin" or "obj"))
            .OrderBy(p => Path.GetRelativePath(root, p), StringComparer.Ordinal))
            Append(hash, Path.GetRelativePath(root, file).Replace('\\', '/') + "|" + File(file));
        return Convert.ToHexString(hash.GetHashAndReset());
    }
    private static string Workbook(string path)
    {
        using var workbook = new XLWorkbook(path);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var sheet in workbook.Worksheets)
        {
            Append(hash, sheet.Name + "|" + sheet.LastRowUsed()?.RowNumber() + "|" + sheet.LastColumnUsed()?.ColumnNumber());
            Append(hash, "freeze|" + sheet.SheetView.SplitRow + "|" + sheet.SheetView.SplitColumn);
            foreach (var range in sheet.MergedRanges) Append(hash, "merge|" + range.RangeAddress);
            foreach (var cell in sheet.CellsUsed())
                Append(hash, cell.Address + "|" + cell.Value.Type + "|" + cell.Value.ToString(CultureInfo.InvariantCulture)
                    + "|" + cell.Style.NumberFormat.Format + "|" + cell.Style.NumberFormat.NumberFormatId);
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }
    private static void Append(IncrementalHash hash, string text) => hash.AppendData(Encoding.UTF8.GetBytes(text + "\n"));
}
