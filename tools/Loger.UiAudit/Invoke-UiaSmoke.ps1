param(
    [string]$AppPath = 'artifacts/redesign/app/LOGER.exe',
    [string]$OutputPath = 'artifacts/redesign/uia',
    [ValidateSet('Light', 'Dark')][string]$Theme = 'Dark'
)

# Black-box smoke against the separate, actual LOGER process. No reflection into app code.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$taskAppPath = [IO.Path]::GetFullPath($AppPath)
$taskOutput = [IO.Path]::GetFullPath($OutputPath)
[IO.Directory]::CreateDirectory($taskOutput) | Out-Null
$taskChecks = [Collections.Generic.List[object]]::new()
$taskProcess = $null
$taskWindow = $null
$taskFailure = $null

function Wait-Condition([scriptblock]$Condition, [string]$Description, [int]$TimeoutMs = 15000) {
    $watch = [Diagnostics.Stopwatch]::StartNew()
    while (-not (& $Condition)) {
        if ($watch.ElapsedMilliseconds -gt $TimeoutMs) { throw "Timeout: $Description" }
        Start-Sleep -Milliseconds 40
    }
}
function Find-Control([string]$Id, [string]$AccessibleName = '') {
    $byId = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $Id)
    $element = $taskWindow.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $byId)
    if ($null -eq $element -and $AccessibleName) {
        $byName = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, $AccessibleName)
        $element = $taskWindow.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $byName)
    }
    if ($null -eq $element) { throw "UIA control missing: $Id ($AccessibleName)" }
    return $element
}
function Invoke-Control($Element) {
    $pattern = $Element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    $pattern.Invoke()
}
function Set-ControlValue($Element, [string]$Value) {
    $pattern = $Element.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
    $pattern.SetValue($Value)
}
function Get-ControlValue($Element) {
    $pattern = $Element.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
    return $pattern.Current.Value
}
function Record([string]$Name) { $taskChecks.Add(@{ name = $Name; status = 'PASS' }); Write-Output "PASS $Name" }

try {
    $start = [Diagnostics.ProcessStartInfo]::new($taskAppPath)
    $start.UseShellExecute = $false
    $start.WorkingDirectory = $taskOutput
    $start.EnvironmentVariables['LOGER_PREFERENCES_PATH'] = [IO.Path]::Combine($taskOutput, 'preferences.json')
    $start.EnvironmentVariables['LOGER_THEME'] = $Theme
    $taskProcess = [Diagnostics.Process]::Start($start)
    Wait-Condition { $taskProcess.Refresh(); $taskProcess.MainWindowHandle -ne [IntPtr]::Zero } 'LOGER main HWND'
    $taskWindow = [System.Windows.Automation.AutomationElement]::FromHandle($taskProcess.MainWindowHandle)
    if ($taskWindow.Current.ProcessId -ne $taskProcess.Id) { throw 'UIA window belongs to a different process.' }
    Record 'Actual application launched; UIA connected to its HWND and PID'

    $nodes = $taskWindow.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    $inventory = foreach ($node in $nodes) {
        @{ name = $node.Current.Name; id = $node.Current.AutomationId; type = $node.Current.ControlType.ProgrammaticName;
           enabled = $node.Current.IsEnabled; offscreen = $node.Current.IsOffscreen }
    }
    $inventory | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath ([IO.Path]::Combine($taskOutput, 'uia-controls.json')) -Encoding UTF8

    Invoke-Control (Find-Control 'navDecoder' '02   Декодирование')
    Wait-Condition { -not (Find-Control 'textBoxDevices' 'Путь к описанию CAN-посылок').Current.IsOffscreen } 'decoder navigation'
    Record 'UIA InvokePattern opens decoder page'
    Invoke-Control (Find-Control 'navSession' '01   Обработка')
    Wait-Condition { -not (Find-Control 'textBoxCanLog' 'Путь к логу или папке').Current.IsOffscreen } 'session navigation'
    Record 'UIA InvokePattern returns to session page'

    $themeButton = Find-Control 'buttonTheme' 'Светлая тема'
    Invoke-Control $themeButton
    $preferences = [IO.Path]::Combine($taskOutput, 'preferences.json')
    Wait-Condition { [IO.File]::Exists($preferences) -and (Get-Content -LiteralPath $preferences -Raw -Encoding UTF8 | ConvertFrom-Json).Theme -ne $Theme } 'theme preference write'
    $firstTheme = (Get-Content -LiteralPath $preferences -Raw -Encoding UTF8 | ConvertFrom-Json).Theme
    if ($firstTheme -eq $Theme) { throw 'UIA theme invocation did not change preference.' }
    Invoke-Control $themeButton
    Wait-Condition { (Get-Content -LiteralPath $preferences -Raw -Encoding UTF8 | ConvertFrom-Json).Theme -eq $Theme } 'theme return'
    Record 'UIA theme button changes, persists, and restores both themes'

    $source = [IO.Path]::Combine($taskOutput, 'small.trc')
    $description = [IO.Path]::Combine($taskOutput, 'devices.dbc')
    $result = [IO.Path]::Combine($taskOutput, 'result.csv')
    [IO.File]::WriteAllText($source, "     1)        10.0  Rx     0CFF0008  8  11 00 00 00 00 00 00 00`n")
    [IO.File]::WriteAllText($description, "BO_ 2365521928 A: 8 X`n SG_ A1 : 0|8@1+ (1,0) [0|255] `"`" X`n")
    Set-ControlValue (Find-Control 'textBoxCanLog' 'Путь к логу или папке') $source
    Invoke-Control (Find-Control 'navDecoder' '02   Декодирование')
    Wait-Condition { -not (Find-Control 'textBoxDevices' 'Путь к описанию CAN-посылок').Current.IsOffscreen } 'decoder editor visibility'
    Set-ControlValue (Find-Control 'textBoxDevices' 'Путь к описанию CAN-посылок') $description
    Set-ControlValue (Find-Control 'textBoxOutput' 'Путь результата') $result
    $processButton = Find-Control 'buttonProcess' 'Обработать'
    Invoke-Control $processButton
    $journal = Find-Control 'textBoxLog' 'Журнал операций'
    Wait-Condition { [IO.File]::Exists($result) -and (Get-ControlValue $journal).Contains('успешно') -and $processButton.Current.IsEnabled } 'real UIA processing output'
    if (-not ([IO.File]::ReadAllText($result)).Contains('10;17')) { throw 'Unexpected decoded output value.' }
    Record 'UIA ValuePattern inputs and InvokePattern processing produce correct decoded CSV'

    $large = [IO.Path]::Combine($taskOutput, 'large.trc')
    [IO.File]::WriteAllText($large, ("     1)        10.0  Rx     0CFF0008  8  11 00 00 00 00 00 00 00`n" * 2000000))
    $cancelledOutput = [IO.Path]::Combine($taskOutput, 'cancelled.csv')
    if ([IO.File]::Exists($cancelledOutput)) { [IO.File]::Delete($cancelledOutput) }
    Invoke-Control (Find-Control 'navSession' '01   Обработка')
    Wait-Condition { -not (Find-Control 'textBoxCanLog' 'Путь к логу или папке').Current.IsOffscreen } 'cancellation source visibility'
    Set-ControlValue (Find-Control 'textBoxCanLog' 'Путь к логу или папке') $large
    Set-ControlValue (Find-Control 'textBoxOutput' 'Путь результата') ([IO.Path]::Combine($taskOutput, 'cancelled.csv'))
    Invoke-Control (Find-Control 'navDecoder' '02   Декодирование')
    Invoke-Control $processButton
    Wait-Condition { -not (Find-Control 'buttonCancel' 'Отменить операцию').Current.IsOffscreen } 'cancel visibility while decoder page active'
    Invoke-Control (Find-Control 'buttonCancel' 'Отменить операцию')
    Wait-Condition { (Get-ControlValue $journal).Contains('отменена') -and $processButton.Current.IsEnabled } 'cancellation completion'
    if ([IO.File]::Exists([IO.Path]::Combine($taskOutput, 'cancelled.csv'))) { throw 'Cancellation left partial output.' }
    Record 'UIA cancellation while decoder page active removes partial output and restores idle'

    $batch = [IO.Path]::Combine($taskOutput, 'batch-input')
    $batchOutput = [IO.Path]::Combine($taskOutput, 'batch-output')
    [IO.Directory]::CreateDirectory($batch) | Out-Null
    [IO.Directory]::CreateDirectory($batchOutput) | Out-Null
    [IO.File]::Copy($source, [IO.Path]::Combine($batch, 'a.trc'), $true)
    [IO.File]::Copy($source, [IO.Path]::Combine($batch, 'b.trc'), $true)
    Invoke-Control (Find-Control 'navSession' '01   Обработка')
    Wait-Condition { -not (Find-Control 'textBoxCanLog' 'Путь к логу или папке').Current.IsOffscreen } 'batch source visibility'
    Set-ControlValue (Find-Control 'textBoxCanLog' 'Путь к логу или папке') $batch
    Set-ControlValue (Find-Control 'textBoxOutput' 'Путь результата') $batchOutput
    Invoke-Control $processButton
    Wait-Condition { (Get-ControlValue $journal).Contains('Готово:') -and $processButton.Current.IsEnabled } 'per-file batch after cancellation'
    if (@([IO.Directory]::GetFiles($batchOutput, '*.csv')).Length -ne 2) { throw 'Per-file batch count mismatch.' }
    $openResult = Find-Control 'buttonOpenOutput' 'Открыть результат ↗'
    if ($openResult.Current.IsOffscreen) { throw 'Successful batch result action is inaccessible after cancellation.' }
    Record 'UIA repeated per-file batch after cancellation creates both outputs and exposes result action'

    Invoke-Control (Find-Control 'navSession' '01   Обработка')
    Wait-Condition { -not (Find-Control 'textBoxCanLog' 'Путь к логу или папке').Current.IsOffscreen } 'source editor visibility'
    Set-ControlValue (Find-Control 'textBoxCanLog' 'Путь к логу или папке') ''
    Invoke-Control $processButton
    Wait-Condition { (Get-ControlValue $journal).Contains('не указан') } 'journal validation error'
    Record 'UIA journal exposes actionable missing-input validation'

    # Prove preference loading in a fresh process, with no environment theme override.
    $savedTheme = (Get-Content -LiteralPath $preferences -Raw -Encoding UTF8 | ConvertFrom-Json).Theme
    if ($savedTheme -ne 'Light') {
        Invoke-Control (Find-Control 'buttonTheme')
        Wait-Condition { (Get-Content -LiteralPath $preferences -Raw -Encoding UTF8 | ConvertFrom-Json).Theme -eq 'Light' } 'Light preference for restart'
    }
    $null = $taskProcess.CloseMainWindow()
    if (-not $taskProcess.WaitForExit(5000)) { throw 'Application did not close cleanly before preference restart.' }
    $taskProcess.Dispose()
    $restart = [Diagnostics.ProcessStartInfo]::new($taskAppPath)
    $restart.UseShellExecute = $false
    $restart.WorkingDirectory = $taskOutput
    $restart.EnvironmentVariables['LOGER_PREFERENCES_PATH'] = $preferences
    $restart.EnvironmentVariables.Remove('LOGER_THEME')
    $taskProcess = [Diagnostics.Process]::Start($restart)
    Wait-Condition { $taskProcess.Refresh(); $taskProcess.MainWindowHandle -ne [IntPtr]::Zero } 'fresh preference-loading HWND'
    $taskWindow = [System.Windows.Automation.AutomationElement]::FromHandle($taskProcess.MainWindowHandle)
    Wait-Condition { (Find-Control 'buttonTheme').Current.Name.Contains('Тёмная') } 'persisted Light theme after fresh restart'
    Record 'Saved Light theme is restored by fresh application restart without environment override'
}
catch {
    $taskFailure = $_.Exception.ToString()
    if ($null -ne $journal) {
        try { $taskFailure += "`nJournal: " + (Get-ControlValue $journal) } catch { }
    }
    $taskChecks.Add(@{ name = 'UIA smoke'; status = 'FAIL'; error = $taskFailure })
    Write-Output "FAIL $taskFailure"
}
finally {
    if ($null -ne $taskProcess -and -not $taskProcess.HasExited) {
        $null = $taskProcess.CloseMainWindow()
        if (-not $taskProcess.WaitForExit(5000)) { $taskProcess.Kill(); $taskProcess.WaitForExit() }
    }
    @{ app = $taskAppPath; appSha256 = (Get-FileHash -LiteralPath $taskAppPath -Algorithm SHA256).Hash;
       uiAssemblySha256 = (Get-FileHash -LiteralPath ([IO.Path]::ChangeExtension($taskAppPath, '.dll')) -Algorithm SHA256).Hash;
       coreAssemblySha256 = (Get-FileHash -LiteralPath (Join-Path ([IO.Path]::GetDirectoryName($taskAppPath)) 'logReader.dll') -Algorithm SHA256).Hash;
       theme = $Theme; method = 'Windows UI Automation against standalone application process'; checks = @($taskChecks.ToArray());
       limitations = @('Native file dialogs, physical keyboard/mouse and sustained scrolling not exercised') } |
       ConvertTo-Json -Depth 6 | Set-Content -LiteralPath ([IO.Path]::Combine($taskOutput, 'uia-report.json')) -Encoding UTF8
}
if ($null -ne $taskFailure) { exit 1 }
