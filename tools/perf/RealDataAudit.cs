using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using logReader;
using logReader.Processing;

internal static class RealDataAudit
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    public static int Run(string suppliedRoot, string work, string reportPath, string? inputFilter = null, bool allExports = false)
    {
        string root = Path.GetFullPath(suppliedRoot);
        string prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
        if (Path.GetFullPath(work).Equals(root, StringComparison.OrdinalIgnoreCase)
            || Path.GetFullPath(work).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            || Path.GetFullPath(reportPath).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Real fixtures are read-only; outputs must be outside their directory.");
        Directory.CreateDirectory(work);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        var extensions = new HashSet<string>(new[] { ".trc", ".asc", ".csv", ".txt", ".xlsx", ".dbc", ".dbf" }, StringComparer.OrdinalIgnoreCase);
        var files = Enumerate(root).Where(p => extensions.Contains(Path.GetExtension(p))).Order(StringComparer.Ordinal).ToArray();
        var inputs = files.Select(p => new { File = Path.GetRelativePath(root, p), Bytes = new FileInfo(p).Length, Sha256 = Hash(p) }).ToArray();
        var configs = new List<(string Path, HashSet<string> Ids, int Parameters)>();
        var descriptions = new List<object>();
        foreach (string file in files.Where(p => Path.GetExtension(p).ToLowerInvariant() is ".dbc" or ".dbf" or ".xlsx"))
        {
            var messages = new AuditMessages();
            try
            {
                var devices = DeviceFiles.LoadDevices(file, messages.Add);
                descriptions.Add(new { File = Path.GetRelativePath(root, file), Devices = devices.Count, Parameters = devices.Sum(d => d.Headers.Length), Messages = messages, MessagesOmitted = messages.Omitted });
                if (devices.Count > 0) configs.Add((file, devices.Select(d => d.ID).ToHashSet(StringComparer.OrdinalIgnoreCase), devices.Sum(d => d.Headers.Length)));
            }
            catch (Exception ex) { descriptions.Add(new { File = Path.GetRelativePath(root, file), Status = "LOAD ERROR", Error = ex.Message, Messages = messages, MessagesOmitted = messages.Omitted }); }
        }
        var assembly = typeof(Device).Assembly;
        var detector = assembly.GetType("logReader.Processing.LogFormatDetector")!.GetMethod("Detect", BindingFlags.Static | BindingFlags.Public)!;
        var serviceType = assembly.GetType("logReader.Processing.LogProcessingService")!;
        var service = Activator.CreateInstance(serviceType, true)!;
        var process = serviceType.GetMethod("ProcessSingleFile")!;
        var inventory = new List<object>();
        var scenarios = new List<object>();
        int passed = 0, failures = 0, unmatched = 0;
        var usable = new List<(string Input, string Config, string Kind)>();
        foreach (string file in files.Where(p => Path.GetExtension(p).ToLowerInvariant() is ".trc" or ".asc" or ".csv" or ".txt"))
        {
            string kind;
            try { kind = detector.Invoke(null, new object[] { file })!.ToString()!; }
            catch (Exception ex) { inventory.Add(new { File = Path.GetRelativePath(root, file), Kind = "DETECT ERROR", Error = Unwrap(ex).Message }); failures++; continue; }
            inventory.Add(new { File = Path.GetRelativePath(root, file), Kind = kind });
            if (kind == "None") continue;
            if (inputFilter != null)
            {
                string relative = Path.GetRelativePath(root, file);
                bool selected = inputFilter.EndsWith('*')
                    ? relative.StartsWith(inputFilter[..^1], StringComparison.OrdinalIgnoreCase)
                    : relative.Equals(inputFilter, StringComparison.OrdinalIgnoreCase);
                if (!selected) continue;
            }
            var ids = SampleIds(file);
            var ranked = configs.Select(c => new { Config = c, Matched = c.Ids.Count(ids.Contains) })
                .OrderByDescending(c => c.Matched).ThenByDescending(c => c.Config.Parameters).ThenBy(c => c.Config.Path, StringComparer.Ordinal).FirstOrDefault();
            if (ranked == null || ranked.Matched == 0)
            {
                scenarios.Add(new { Input = Path.GetRelativePath(root, file), Kind = kind, Status = "NOT TESTED", Reason = "No matching supplied description in the first 2000 lines" }); unmatched++;
                continue;
            }
            usable.Add((file, ranked.Config.Path, kind));
            RunCase(file, ranked.Config.Path, OutputFormat.Csv);
            if (allExports)
            {
                RunCase(file, ranked.Config.Path, OutputFormat.Xlsx);
                if (kind == "Trc") RunCase(file, ranked.Config.Path, OutputFormat.CsvDstConnect);
            }
        }
        foreach (string kind in allExports ? Array.Empty<string>() : new[] { "Trc", "Asc", "MatrixCsv", "StepCsv" })
        {
            var sample = usable.Where(p => p.Kind == kind).OrderBy(p => new FileInfo(p.Input).Length).FirstOrDefault();
            if (sample.Input == null) continue;
            RunCase(sample.Input, sample.Config, OutputFormat.Xlsx);
            if (kind == "Trc") RunCase(sample.Input, sample.Config, OutputFormat.CsvDstConnect);
        }
        // Three batch routes over two real TRC inputs, with fresh separate output folders.
        var trcInputs = usable.Where(p => p.Kind == "Trc").OrderBy(p => new FileInfo(p.Input).Length).Take(2).ToArray();
        if (trcInputs.Length == 2)
        {
            foreach (string mode in new[] { "PerInputFile", "MergeToSingleFile", "SplitTrcByDate" })
            {
                string folder = Path.Combine(work, "batch-" + mode); Directory.CreateDirectory(folder);
                var batchType = assembly.GetType("logReader.Processing.BatchOutputMode")!;
                var messages = new AuditMessages();
                var timer = Stopwatch.StartNew();
                try
                {
                    var result = serviceType.GetMethod("ProcessFolderBatch")!.Invoke(service, new object[] {
                        trcInputs.Select(p => p.Input).ToArray(), folder, trcInputs[0].Config, Enum.Parse(batchType, mode),
                        DeviceFiles.LoadDevices(trcInputs[0].Config), new OutputSettings { Format = OutputFormat.Csv }, new ProcessingContext(messages.Add) })!;
                    timer.Stop();
                    int Property(string name) => (int)result.GetType().GetProperty(name)!.GetValue(result)!;
                    bool success = Property("Failed") == 0 && Property("Created") > 0;
                    if (success) passed++; else failures++;
                    scenarios.Add(new { Scenario = "batch-" + mode, Input = trcInputs.Select(p => Path.GetRelativePath(root, p.Input)).ToArray(),
                        Created = Property("Created"), Expected = Property("Expected"), Failed = Property("Failed"), Status = success ? "PASS" : "FAIL",
                        ElapsedMs = timer.Elapsed.TotalMilliseconds, Outputs = Directory.GetFiles(folder).Order().Select(p => new { File = Path.GetFileName(p), Sha256 = Hash(p) }).ToArray(), Messages = messages, MessagesOmitted = messages.Omitted });
                }
                catch (Exception ex) { failures++; scenarios.Add(new { Scenario = "batch-" + mode, Status = "FAIL", Error = Unwrap(ex).Message, Messages = messages, MessagesOmitted = messages.Omitted }); }
            }
        }
        bool unchanged = inputs.All(input => Hash(Path.Combine(root, input.File)) == input.Sha256);
        if (!unchanged) failures++;
        File.WriteAllText(reportPath, JsonSerializer.Serialize(new { DataRoot = root, CoreAssemblySha256 = Hash(assembly.Location),
            Passed = passed, Failed = failures, Unmatched = unmatched, InputsUnchanged = unchanged, Inputs = inputs,
            InputFilter = inputFilter, AllExports = allExports, Descriptions = descriptions, Inventory = inventory, Scenarios = scenarios,
            Method = "Full supplied logs; sampled matching supplied descriptions. CSV for every selected recognized input, representative or opt-in all XLSX/DST exports, and three batch modes. No edits to source fixtures. One run per case, not a performance benchmark." }, Json));
        Console.WriteLine($"REAL DATA: {passed} PASS, {failures} FAIL, {unmatched} unmatched; {files.Length} source files unchanged={unchanged}; report={reportPath}");
        return failures == 0 ? 0 : 1;

        void RunCase(string input, string config, OutputFormat format)
        {
            string name = $"{scenarios.Count:D3}-{format}";
            string output = Path.Combine(work, name + (format == OutputFormat.Xlsx ? ".xlsx" : ".csv"));
            var messages = new AuditMessages();
            var timer = Stopwatch.StartNew();
            try
            {
                var result = (ProcessingResult)process.Invoke(service, new object[] { input, output, DeviceFiles.LoadDevices(config),
                    new OutputSettings { Format = format }, new ProcessingContext(messages.Add) })!;
                timer.Stop();
                bool success = result.Success && result.RowsWritten > 0 && File.Exists(output);
                if (success) passed++; else failures++;
                string? fingerprint = success ? Fingerprint.Output(output) : null;
                scenarios.Add(new { Scenario = name, Input = Path.GetRelativePath(root, input), Description = Path.GetRelativePath(root, config),
                    Format = format.ToString(), Status = success ? "PASS" : "FAIL", result.RowsWritten, result.Error,
                    ElapsedMs = timer.Elapsed.TotalMilliseconds, Hash = fingerprint, Messages = messages, MessagesOmitted = messages.Omitted });
                Console.WriteLine($"{name}: {(success ? "PASS" : "FAIL")} rows={result.RowsWritten} {Path.GetRelativePath(root, input)}");
            }
            catch (Exception ex) { failures++; scenarios.Add(new { Scenario = name, Input = Path.GetRelativePath(root, input), Status = "FAIL", Error = Unwrap(ex).Message, Messages = messages, MessagesOmitted = messages.Omitted }); Console.WriteLine($"{name}: FAIL {Unwrap(ex).Message}"); }
        }
    }
    private static HashSet<string> SampleIds(string path)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string line in File.ReadLines(path).Take(2000))
        {
            foreach (Match match in Regex.Matches(line, @"(?<![0-9A-Fa-f])[0-9A-Fa-f]{8}(?![0-9A-Fa-f])")) ids.Add(match.Value);
            // CANfox omits leading zeroes; PCAN also writes standard IDs with four digits.
            foreach (Match match in Regex.Matches(line, @"(?:\bRx|\bTx|\s-)\s+([0-9A-Fa-f]{1,8})\s+\d+\b")) ids.Add(CanId.NormalizeOrUpper(match.Groups[1].Value));
        }
        return ids;
    }
    private static IEnumerable<string> Enumerate(string root)
    {
        foreach (string file in Directory.EnumerateFiles(root)) yield return file;
        foreach (string directory in Directory.EnumerateDirectories(root))
        {
            // OneDrive placeholders can also have ReparsePoint; only actual links are excluded.
            if (new DirectoryInfo(directory).LinkTarget != null || Path.GetFileName(directory).Equals("pub", StringComparison.OrdinalIgnoreCase)) continue;
            foreach (string file in Enumerate(directory)) yield return file;
        }
    }
    private static string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
    private static Exception Unwrap(Exception error) => error is TargetInvocationException { InnerException: { } cause } ? cause : error;
    private sealed class AuditMessages : List<string>
    {
        public int Omitted { get; private set; }
        public new void Add(string message) { if (Count < 30) base.Add(message); else Omitted++; }
    }
}
