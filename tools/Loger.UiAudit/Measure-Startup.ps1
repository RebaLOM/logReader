param(
    [string]$AppPath = 'artifacts/redesign/app/LOGER.exe',
    [string]$OutputPath = 'artifacts/redesign/startup',
    [ValidateSet('Light', 'Dark', 'existing')][string]$Theme = 'Dark',
    [int]$Iterations = 7
)
$ErrorActionPreference = 'Stop'
$taskApp = [IO.Path]::GetFullPath($AppPath)
$taskOutput = [IO.Path]::GetFullPath($OutputPath)
[IO.Directory]::CreateDirectory($taskOutput) | Out-Null
$taskPreferences = [IO.Path]::Combine($taskOutput, 'preferences.json')
$taskSavedTheme = $null
if ([IO.File]::Exists($taskPreferences)) {
    try { $taskSavedTheme = (Get-Content -LiteralPath $taskPreferences -Raw -Encoding UTF8 | ConvertFrom-Json).Theme }
    catch { $taskSavedTheme = 'invalid preference JSON' }
}
$samples = [Collections.Generic.List[object]]::new()
for ($sample = 1; $sample -le $Iterations; $sample++) {
    $start = [Diagnostics.ProcessStartInfo]::new($taskApp)
    $start.UseShellExecute = $false
    $start.WorkingDirectory = $taskOutput
    $start.EnvironmentVariables['LOGER_PREFERENCES_PATH'] = $taskPreferences
    if ($Theme -eq 'existing') { $start.EnvironmentVariables.Remove('LOGER_THEME') }
    else { $start.EnvironmentVariables['LOGER_THEME'] = $Theme }
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $taskProcess = [Diagnostics.Process]::Start($start)
    try {
        if (-not $taskProcess.WaitForInputIdle(15000)) { throw 'Process did not reach input-idle.' }
        do {
            $taskProcess.Refresh()
            if ($watch.ElapsedMilliseconds -gt 15000) { throw 'Main HWND was not created.' }
        } while ($taskProcess.MainWindowHandle -eq [IntPtr]::Zero)
        $watch.Stop()
        $samples.Add(@{ sample = $sample; startupToInputIdleMs = $watch.Elapsed.TotalMilliseconds;
            workingSetBytes = $taskProcess.WorkingSet64; privateBytes = $taskProcess.PrivateMemorySize64;
            cpuMsAtIdle = $taskProcess.TotalProcessorTime.TotalMilliseconds })
    }
    finally {
        if (-not $taskProcess.HasExited) {
            $null = $taskProcess.CloseMainWindow()
            if (-not $taskProcess.WaitForExit(5000)) { $taskProcess.Kill(); $taskProcess.WaitForExit() }
        }
        $taskProcess.Dispose()
    }
}
$timings = @($samples | ForEach-Object { $_.startupToInputIdleMs } | Sort-Object)
$memory = @($samples | ForEach-Object { $_.workingSetBytes } | Sort-Object)
$report = @{ app = $taskApp; sha256 = (Get-FileHash -LiteralPath $taskApp -Algorithm SHA256).Hash;
    uiAssemblySha256 = (Get-FileHash -LiteralPath ([IO.Path]::ChangeExtension($taskApp, '.dll')) -Algorithm SHA256).Hash;
    coreAssemblySha256 = (Get-FileHash -LiteralPath (Join-Path ([IO.Path]::GetDirectoryName($taskApp)) 'logReader.dll') -Algorithm SHA256).Hash;
    theme = $Theme; iterations = $Iterations; samples = $samples.ToArray();
    environmentThemeOverride = ($Theme -ne 'existing');
    preferenceFileExists = [IO.File]::Exists($taskPreferences); savedPreferenceTheme = $taskSavedTheme;
    medianStartupMs = $timings[[int][Math]::Floor($timings.Length / 2)];
    medianWorkingSetBytes = $memory[[int][Math]::Floor($memory.Length / 2)];
    method = 'Fresh actual WinExe processes; wall clock from Process.Start to WaitForInputIdle with nonzero main HWND; includes runtime bootstrap. Warm OS/disk cache, same machine/session.';
    limitations = @('Input-idle is a Windows readiness proxy, not a guarantee of every pixel being painted.', 'CPU snapshot at idle is process CPU time, not CPU utilization or sustained workload.') }
$report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath ([IO.Path]::Combine($taskOutput, 'startup-report.json')) -Encoding UTF8
Write-Output "Startup median: $($report.medianStartupMs) ms; working set median: $($report.medianWorkingSetBytes) bytes ($Iterations fresh processes)."
