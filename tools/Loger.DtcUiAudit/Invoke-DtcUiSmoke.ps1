param(
    [Parameter(Mandatory = $true)][string]$AppPath,
    [Parameter(Mandatory = $true)][string]$InputPath,
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
    [string]$MainWindowTitle = 'LOGER',
    [ValidateSet('Dark', 'Light')][string]$Theme,
    [string]$SearchSpn = '',
    [int]$ExpectedSummaryRows = -1,
    [int]$ExpectedInitialTab = -1,
    [string]$ExpectedStatus = '',
    [switch]$RequireCompleteRaw,
    [string]$FilterFile = ''
)

$ErrorActionPreference = 'Stop'
$app = (Resolve-Path -LiteralPath $AppPath).Path
$sourcePath = (Resolve-Path -LiteralPath $InputPath).Path
$output = [IO.Path]::GetFullPath($OutputDirectory)
$sourceRoot = if ([IO.Directory]::Exists($sourcePath)) { $sourcePath } else { [IO.Path]::GetDirectoryName($sourcePath) }
if ($output.Equals($sourceRoot, [StringComparison]::OrdinalIgnoreCase) -or
    $output.StartsWith($sourceRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'UI audit artifacts must be outside the source-data directory.'
}
[IO.Directory]::CreateDirectory($output) | Out-Null
foreach ($pair in @(@('APPDATA', 'Roaming'), @('LOCALAPPDATA', 'Local'), @('TEMP', 'Temp'), @('TMP', 'Temp'))) {
    $directory = Join-Path $output ('sandbox/' + $pair[1])
    [IO.Directory]::CreateDirectory($directory) | Out-Null
    [Environment]::SetEnvironmentVariable($pair[0], $directory, 'Process')
}
$sourceFiles = if ([IO.Directory]::Exists($sourcePath)) { [IO.Directory]::GetFiles($sourcePath, '*.trc') } else { @($sourcePath) }
$sourceHashes = @{}
foreach ($sourceFile in $sourceFiles) { $sourceHashes[$sourceFile] = (Get-FileHash -LiteralPath $sourceFile -Algorithm SHA256).Hash }
$checks = [Collections.Generic.List[object]]::new()
$framework = [Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory()
$wpf = Join-Path $framework 'WPF'
Add-Type -Path (Join-Path $wpf 'UIAutomationClient.dll')
Add-Type -Path (Join-Path $wpf 'UIAutomationTypes.dll')
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies @(
    (Join-Path $wpf 'UIAutomationClient.dll'),
    (Join-Path $wpf 'UIAutomationTypes.dll'),
    (Join-Path $wpf 'WindowsBase.dll'),
    'System.Drawing.dll'
) -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Automation;

public static class DtcUiNative
{
    static readonly Dictionary<AutomationElement, IntPtr> WindowHandles = new Dictionary<AutomationElement, IntPtr>();
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    delegate bool EnumWindow(IntPtr hwnd, IntPtr parameter);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindow callback, IntPtr parameter);
    [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr hwnd, EnumWindow callback, IntPtr parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd, StringBuilder name, int count);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr hwnd, StringBuilder title, int count);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wparam, IntPtr lparam);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] static extern IntPtr GetParent(IntPtr hwnd);
    [DllImport("user32.dll")] static extern int GetDlgCtrlID(IntPtr hwnd);
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll")] static extern bool ScreenToClient(IntPtr hwnd, ref Point point);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr hwnd, uint message, IntPtr wparam, string lparam);
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd, uint message, IntPtr wparam, IntPtr lparam);

    public static AutomationElement Window(int pid, string name)
    {
        // Owned modal WinForms windows need not be direct children of the UIA desktop.
        IntPtr found = IntPtr.Zero;
        EnumWindows(delegate(IntPtr hwnd, IntPtr parameter)
        {
            uint owner;
            GetWindowThreadProcessId(hwnd, out owner);
            if (owner != pid) return true;
            StringBuilder title = new StringBuilder(512);
            GetWindowText(hwnd, title, title.Capacity);
            if (title.ToString() != name) return true;
            found = hwnd;
            return false;
        }, IntPtr.Zero);
        if (found == IntPtr.Zero) return null;
        AutomationElement element = AutomationElement.FromHandle(found);
        WindowHandles[element] = found;
        return element;
    }
    public static AutomationElement Find(AutomationElement parent, string id, string name)
    {
        return FindControl(parent, id, name, false);
    }
    public static AutomationElement FindPrefix(AutomationElement parent, string name)
    {
        return FindControl(parent, "", name, true);
    }
    static AutomationElement FindControl(AutomationElement parent, string id, string name, bool prefix, ControlType requiredType = null)
    {
        // Depth-first Descendants walks hundreds of thousands of virtual cells before
        // reaching a sibling textbox. Inspect controls breadth-first and skip grid contents.
        Queue<AutomationElement> pending = new Queue<AutomationElement>();
        pending.Enqueue(parent);
        while (pending.Count != 0)
        {
            AutomationElement current = pending.Dequeue();
            if ((requiredType == null || current.Current.ControlType == requiredType)
                && ((!String.IsNullOrEmpty(id) && current.Current.AutomationId == id)
                    || (!String.IsNullOrEmpty(name) && (prefix ? current.Current.Name.StartsWith(name, StringComparison.Ordinal) : current.Current.Name == name)))) return current;
            object pattern;
            if (current.TryGetCurrentPattern(GridPattern.Pattern, out pattern)) continue;
            AutomationElement child = TreeWalker.ControlViewWalker.GetFirstChild(current);
            while (child != null)
            {
                pending.Enqueue(child);
                child = TreeWalker.ControlViewWalker.GetNextSibling(child);
            }
        }
        return null;
    }
    public static AutomationElement TabHeader(AutomationElement parent, string name)
    {
        return FindControl(parent, "", name, false, ControlType.TabItem);
    }
    public static void Click(AutomationElement element)
    {
        if (element == null) throw new InvalidOperationException("Control not found.");
        // Some WinForms UIA providers synchronously wait inside a modal ShowDialog.
        // A posted native button click exercises the same event without blocking this controller.
        IntPtr hwnd = (IntPtr)element.Current.NativeWindowHandle;
        if (hwnd != IntPtr.Zero)
        {
            SetForegroundWindow(GetAncestor(hwnd, 2));
            if (!PostMessage(hwnd, 0x00F5, IntPtr.Zero, IntPtr.Zero)) throw new InvalidOperationException("BM_CLICK failed.");
        }
        else
        {
            ThreadPool.QueueUserWorkItem(delegate { ((InvokePattern)element.GetCurrentPattern(InvokePattern.Pattern)).Invoke(); });
        }
    }
    public static int Select(AutomationElement element)
    {
        // UIA tab names identify the header, but this provider misreports its bounds/index.
        // TCM_SETCURFOCUS is a scalar message and lets the tab control emit selection notifications.
        IntPtr hwnd = TabControlHandle(element);
        SetForegroundWindow(GetAncestor(hwnd, 2));
        SendMessage(hwnd, 0x1330, (IntPtr)1, IntPtr.Zero);
        return (int)SendMessage(hwnd, 0x130B, IntPtr.Zero, IntPtr.Zero);
    }
    public static int SelectedTab(AutomationElement element)
    {
        return (int)SendMessage(TabControlHandle(element), 0x130B, IntPtr.Zero, IntPtr.Zero);
    }
    static IntPtr TabControlHandle(AutomationElement element)
    {
        IntPtr root;
        if (!WindowHandles.TryGetValue(element, out root)) root = (IntPtr)element.Current.NativeWindowHandle;
        IntPtr found = IntPtr.Zero;
        EnumChildWindows(root, delegate(IntPtr hwnd, IntPtr parameter)
        {
            StringBuilder name = new StringBuilder(256);
            GetClassName(hwnd, name, name.Capacity);
            if (name.ToString().IndexOf("SysTabControl32", StringComparison.OrdinalIgnoreCase) < 0) return true;
            found = hwnd;
            return false;
        }, IntPtr.Zero);
        if (found == IntPtr.Zero) throw new InvalidOperationException("Native tab control unavailable.");
        return found;
    }
    public static void SetText(AutomationElement element, string value)
    {
        object pattern;
        if (element.TryGetCurrentPattern(ValuePattern.Pattern, out pattern))
            ((ValuePattern)pattern).SetValue(value);
        else
            SendMessage((IntPtr)element.Current.NativeWindowHandle, 0x000C, IntPtr.Zero, value);
    }
    public static string Text(AutomationElement element)
    {
        if (element == null) return "";
        object pattern;
        if (element.TryGetCurrentPattern(ValuePattern.Pattern, out pattern)) return ((ValuePattern)pattern).Current.Value;
        return element.Current.Name;
    }
    public static bool Checked(AutomationElement element)
    {
        return ((TogglePattern)element.GetCurrentPattern(TogglePattern.Pattern)).Current.ToggleState == ToggleState.On;
    }
    public static double Number(AutomationElement element)
    {
        object pattern;
        if (element.TryGetCurrentPattern(RangeValuePattern.Pattern, out pattern)) return ((RangeValuePattern)pattern).Current.Value;
        string value = Text(element);
        double result;
        if (Double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out result)) return result;
        AutomationElement edit = element.FindFirst(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
        value = Text(edit);
        if (Double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out result)) return result;
        if (Double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result)) return result;
        throw new InvalidOperationException("Numeric value unavailable: " + value);
    }
    public static int Rows(AutomationElement grid)
    {
        return ((GridPattern)grid.GetCurrentPattern(GridPattern.Pattern)).Current.RowCount;
    }
    public static string Cell(AutomationElement grid, int row, int column)
    {
        string value = Text(((GridPattern)grid.GetCurrentPattern(GridPattern.Pattern)).GetItem(row, column));
        return value == "(null)" ? "" : value;
    }
    public static void SelectCell(AutomationElement grid, int row, int column)
    {
        AutomationElement cell = ((GridPattern)grid.GetCurrentPattern(GridPattern.Pattern)).GetItem(row, column);
        object pattern;
        if (cell.TryGetCurrentPattern(ScrollItemPattern.Pattern, out pattern)) ((ScrollItemPattern)pattern).ScrollIntoView();
        System.Windows.Rect bounds = cell.Current.BoundingRectangle;
        IntPtr hwnd = (IntPtr)grid.Current.NativeWindowHandle;
        Point point = new Point((int)(bounds.Left + bounds.Width / 2), (int)(bounds.Top + bounds.Height / 2));
        ScreenToClient(hwnd, ref point);
        SetForegroundWindow(GetAncestor(hwnd, 2));
        IntPtr location = (IntPtr)((point.Y << 16) | (point.X & 0xFFFF));
        SendMessage(hwnd, 0x0201, (IntPtr)1, location);
        SendMessage(hwnd, 0x0202, IntPtr.Zero, location);
    }
    public static int SelectFile(AutomationElement combo, string file)
    {
        IntPtr hwnd = (IntPtr)combo.Current.NativeWindowHandle;
        // CB_FINDSTRINGEXACT is a system message: Windows marshals its string across processes.
        int index = (int)SendMessage(hwnd, 0x0158, (IntPtr)(-1), file);
        if (index < 0) throw new InvalidOperationException("Source file missing from filter: " + file);
        SendMessage(hwnd, 0x014E, (IntPtr)index, IntPtr.Zero);
        IntPtr command = (IntPtr)((1 << 16) | (GetDlgCtrlID(hwnd) & 0xFFFF));
        SendMessage(GetParent(hwnd), 0x0111, command, hwnd);
        return index;
    }
    public static void Close(AutomationElement window)
    {
        IntPtr known;
        if (WindowHandles.TryGetValue(window, out known))
        {
            PostMessage(known, 0x0010, IntPtr.Zero, IntPtr.Zero);
            return;
        }
        object pattern;
        if (window.TryGetCurrentPattern(WindowPattern.Pattern, out pattern)) ((WindowPattern)pattern).Close();
        else SendMessage((IntPtr)window.Current.NativeWindowHandle, 0x0010, IntPtr.Zero, IntPtr.Zero);
    }
    public static void Capture(AutomationElement window, string path)
    {
        IntPtr hwnd = (IntPtr)window.Current.NativeWindowHandle;
        IntPtr known;
        if (WindowHandles.TryGetValue(window, out known)) hwnd = known;
        Rect rect;
        if (!GetWindowRect(hwnd, out rect)) throw new InvalidOperationException("Window rectangle unavailable.");
        using (Bitmap bitmap = new Bitmap(rect.Right - rect.Left, rect.Bottom - rect.Top))
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            IntPtr hdc = graphics.GetHdc();
            bool captured;
            try { captured = PrintWindow(hwnd, hdc, 2); }
            finally { graphics.ReleaseHdc(hdc); }
            if (!captured) throw new InvalidOperationException("PrintWindow failed.");
            bitmap.Save(path, ImageFormat.Png);
        }
    }
}
'@

function Wait-Condition([scriptblock]$Condition, [string]$Description, [int]$Seconds = 30) {
    $deadline = [DateTime]::UtcNow.AddSeconds($Seconds)
    do {
        $value = & $Condition
        if ($value) { return $value }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Timed out: $Description"
}

function Check([string]$Name, [bool]$Passed, $Details) {
    $checks.Add([pscustomobject]@{ Name = $Name; Passed = $Passed; Details = $Details })
    if (!$Passed) { throw "UI check failed: $Name" }
}

$process = $null
$diagnostics = $null
$main = $null
$failure = $null
$dpi = $null
$summaryData = @()
$initialSnapshots = @()
$countsText = $null
$statusText = $null
$initialTab = $null
$filteredCounts = $null
$filteredStatus = $null
try {
    $start = [Diagnostics.ProcessStartInfo]::new($app)
    $start.UseShellExecute = $false
    $start.RedirectStandardError = $true
    $start.WorkingDirectory = [IO.Path]::GetDirectoryName($app)
    if ($Theme) { $start.EnvironmentVariables['LOGER_THEME'] = $Theme }
    foreach ($pair in @(@('APPDATA', 'Roaming'), @('LOCALAPPDATA', 'Local'), @('TEMP', 'Temp'), @('TMP', 'Temp'))) {
        $directory = Join-Path $output ('sandbox/' + $pair[1])
        [IO.Directory]::CreateDirectory($directory) | Out-Null
        $start.EnvironmentVariables[$pair[0]] = $directory
    }
    $process = [Diagnostics.Process]::Start($start)
    $main = Wait-Condition { [DtcUiNative]::Window($process.Id, $MainWindowTitle) } 'main window'
    $dpi = [DtcUiNative]::GetDpiForWindow([IntPtr]$main.Current.NativeWindowHandle)
    $dtcButton = [DtcUiNative]::Find($main, 'buttonDtcDiagnostics', 'Диагностика DTC')
    Check 'Main diagnostics button accessible and enabled' ($null -ne $dtcButton -and $dtcButton.Current.IsEnabled) $dtcButton.Current.Name
    [DtcUiNative]::Capture($main, (Join-Path $output 'main.png'))

    $saveOptionsButton = [DtcUiNative]::Find($main, 'buttonSaveOptions', 'Параметры сохранения')
    Check 'Save-options button accessible' ($null -ne $saveOptionsButton) ([pscustomobject]@{ Name = $saveOptionsButton.Current.Name; Handle = $saveOptionsButton.Current.NativeWindowHandle })
    [DtcUiNative]::Click($saveOptionsButton)
    $options = Wait-Condition { [DtcUiNative]::Window($process.Id, 'Параметры сохранения') } 'save options'
    $include = [DtcUiNative]::Find($options, 'includeDtc', 'Добавлять DTC в результат обработки')
    Check 'DTC output disabled by default' (![DtcUiNative]::Checked($include)) 'Unchecked'
    $timeout = [DtcUiNative]::Find($options, 'dtcTimeoutSeconds', 'Допустимый разрыв данных DTC, секунд')
    $defaultSeconds = [DtcUiNative]::Number($timeout)
    Check 'DTC timeout defaults to two seconds' ($defaultSeconds -eq 2) $defaultSeconds
    [DtcUiNative]::Capture($options, (Join-Path $output 'save-options-default.png'))
    [DtcUiNative]::Click([DtcUiNative]::Find($options, '', 'Отмена'))
    [DtcUiNative]::SetText([DtcUiNative]::Find($main, 'textBoxCanLog', ''), $sourcePath)
    [DtcUiNative]::Click($dtcButton)
    $diagnostics = Wait-Condition { [DtcUiNative]::Window($process.Id, 'Диагностика DTC — LOGER') } 'DTC diagnostics window'
    $analyze = [DtcUiNative]::Find($diagnostics, 'dtcAnalyze', 'Анализировать')
    Wait-Condition { $analyze.Current.IsEnabled } 'source analysis completion' 60 | Out-Null
    $summary = [DtcUiNative]::Find($diagnostics, 'dtcSummary', 'Сводка кодов DTC')
    $summaryCount = [DtcUiNative]::Rows($summary)
    Check 'Real source diagnostic summary read' ($ExpectedSummaryRows -lt 0 -or $summaryCount -eq $ExpectedSummaryRows) $summaryCount
    $countsText = [DtcUiNative]::Text([DtcUiNative]::FindPrefix($diagnostics, 'Кодов в файлах:'))
    foreach ($prefix in @('Диагностические посылки прочитаны', 'Посылки прочитаны', 'Диагностические снимки не найдены', 'Анализ завершён')) {
        $statusControl = [DtcUiNative]::FindPrefix($diagnostics, $prefix)
        if ($statusControl) { $statusText = [DtcUiNative]::Text($statusControl); break }
    }
    if ($ExpectedStatus) { Check 'Analysis status explains source result' ($statusText -eq $ExpectedStatus) $statusText }
    $initialTab = [DtcUiNative]::SelectedTab($diagnostics)
    if ($ExpectedInitialTab -ge 0) { Check 'Expected details tab opens automatically' ($initialTab -eq $ExpectedInitialTab) $initialTab }
    for ($row = 0; $row -lt [Math]::Min($summaryCount, 100); $row++) {
        $summaryData += [pscustomobject]@{
            SourceFile = [DtcUiNative]::Cell($summary, $row, 0)
            Spn = [DtcUiNative]::Cell($summary, $row, 1)
            Fmi = [DtcUiNative]::Cell($summary, $row, 2)
            FirstSeen = [DtcUiNative]::Cell($summary, $row, 3)
            LastSeen = [DtcUiNative]::Cell($summary, $row, 4)
            IntervalCount = [DtcUiNative]::Cell($summary, $row, 5)
            State = [DtcUiNative]::Cell($summary, $row, 6)
        }
    }
    [DtcUiNative]::Capture($diagnostics, (Join-Path $output 'diagnostics-summary.png'))
    if ($FilterFile) {
        $fileIndex = [DtcUiNative]::SelectFile([DtcUiNative]::Find($diagnostics, 'dtcFileFilter', 'Фильтр по исходному файлу'), $FilterFile)
        Wait-Condition { [DtcUiNative]::Rows($summary) -eq 0 -and [DtcUiNative]::SelectedTab($diagnostics) -eq 1 } 'zero-count file filter opens snapshots' | Out-Null
        $filteredCounts = [DtcUiNative]::Text([DtcUiNative]::FindPrefix($diagnostics, 'Кодов в файлах:'))
        $filteredStatus = [DtcUiNative]::Text([DtcUiNative]::FindPrefix($diagnostics, 'Диагностические посылки прочитаны'))
        Check 'Folder file filter opens zero-count snapshots' ($filteredStatus -eq 'Диагностические посылки прочитаны; подтверждённых ошибок нет') ([pscustomobject]@{ File = $FilterFile; Index = $fileIndex; Status = $filteredStatus })
        [DtcUiNative]::Capture($diagnostics, (Join-Path $output 'diagnostics-file-filter.png'))
    }
    if ($SearchSpn) {
        [DtcUiNative]::SetText([DtcUiNative]::Find($diagnostics, 'dtcSearch', 'Поиск по SPN и FMI'), $SearchSpn)
        Wait-Condition { [DtcUiNative]::Rows($summary) -gt 0 -and [DtcUiNative]::Cell($summary, 0, 1).Contains($SearchSpn) } 'SPN search match' | Out-Null
        Check 'Numeric SPN search matches real code' $true ([DtcUiNative]::Cell($summary, 0, 1))
    }
    Check 'Native snapshot tab control accessible' ($initialTab -ge 0) $initialTab
    $selectedTab = [DtcUiNative]::Select($diagnostics)
    Check 'Native snapshot tab selected' ($selectedTab -eq 1) $selectedTab
    $states = Wait-Condition { [DtcUiNative]::Find($diagnostics, 'dtcSnapshots', 'Снимки DTC') } 'snapshot tab controls'
    $rows = [DtcUiNative]::Rows($states)
    Check 'Snapshot timeline available' ($rows -gt 0) $rows
    for ($row = 0; $row -lt [Math]::Min($rows, 3); $row++) {
        $slots = @()
        for ($column = 4; $column -lt 10; $column++) { $slots += [DtcUiNative]::Cell($states, $row, $column) }
        $initialSnapshots += [pscustomobject]@{
            SourceFile = [DtcUiNative]::Cell($states, $row, 0)
            Time = [DtcUiNative]::Cell($states, $row, 1)
            ReportedCount = [DtcUiNative]::Cell($states, $row, 2)
            Completeness = [DtcUiNative]::Cell($states, $row, 3)
            Slots = $slots
        }
    }
    $expectedRawTime = ''
    if ($ExpectedInitialTab -ne 1 -and !$FilterFile) {
        $selectedRow = 0
        if ($SearchSpn) {
            $matched = $false
            for ($row = 0; $row -lt $rows -and !$matched; $row++) {
                for ($column = 4; $column -lt 10; $column++) {
                    if ([DtcUiNative]::Cell($states, $row, $column).Contains('SPN ' + $SearchSpn + ' /')) {
                        $selectedRow = $row
                        $matched = $true
                        break
                    }
                }
            }
            Check 'Snapshot contains searched SPN' $matched $selectedRow
        }
        [DtcUiNative]::SelectCell($states, $selectedRow, 1)
        $expectedRawTime = [DtcUiNative]::Cell($states, $selectedRow, 1)
    }
    $raw = [DtcUiNative]::Find($diagnostics, 'dtcRawData', 'Исходные данные выбранного снимка')
    $rawText = Wait-Condition {
        $text = [DtcUiNative]::Text($raw)
        if ($text.Contains($sourceRoot) -and (!$expectedRawTime -or $text.Contains($expectedRawTime)) -and (!$FilterFile -or $text.Contains($FilterFile))) { $text }
    } 'selected snapshot raw data and timestamp'
    Check 'Selected snapshot exposes source data' $true $rawText
    if ($RequireCompleteRaw) {
        Check 'Complete snapshot retains all three raw frames' ($rawText.Contains('18FE2503=') -and $rawText.Contains('18FE2603=') -and $rawText.Contains('18FE2703=')) $rawText
    }
    Check 'Six fixed snapshot slots' ([DtcUiNative]::Cell($states, 0, 9) -ne $null) 'DTC1 through DTC6'
    Check 'No separate export action' ($null -eq [DtcUiNative]::Find($diagnostics, '', 'Экспорт')) 'No export button'
    [DtcUiNative]::Capture($diagnostics, (Join-Path $output 'diagnostics-snapshots.png'))
}
catch {
    $failure = $_.Exception.ToString()
    if ($diagnostics) { try { [DtcUiNative]::Capture($diagnostics, (Join-Path $output 'failure.png')) } catch {} }
}
finally {
    if ($diagnostics) { try { [DtcUiNative]::Close($diagnostics) } catch {} }
    if ($main) { try { [DtcUiNative]::Close($main) } catch {} }
    if ($process -and !$process.HasExited) {
        if (!$process.WaitForExit(5000)) { $process.Kill(); $process.WaitForExit() }
    }
    $unchanged = $true
    foreach ($sourceFile in $sourceFiles) {
        if ((Get-FileHash -LiteralPath $sourceFile -Algorithm SHA256).Hash -ne $sourceHashes[$sourceFile]) { $unchanged = $false }
    }
    $checks.Add([pscustomobject]@{ Name = 'Source files unchanged'; Passed = $unchanged; Details = $sourceHashes })
    $report = [pscustomobject]@{
        AppPath = $app; AppSha256 = (Get-FileHash -LiteralPath $app -Algorithm SHA256).Hash
        UiDllSha256 = (Get-FileHash -LiteralPath (Join-Path ([IO.Path]::GetDirectoryName($app)) 'LOGER.dll') -Algorithm SHA256).Hash
        CoreDllSha256 = (Get-FileHash -LiteralPath (Join-Path ([IO.Path]::GetDirectoryName($app)) 'logReader.dll') -Algorithm SHA256).Hash
        InputPath = $sourcePath; SourceSha256 = $sourceHashes; Dpi = $dpi
        CountsText = $countsText; StatusText = $statusText; InitialTab = $initialTab
        FilterFile = $FilterFile; FilteredCountsText = $filteredCounts; FilteredStatusText = $filteredStatus
        SummaryRows = $summaryData; InitialSnapshots = $initialSnapshots
        Passed = (!$failure -and !($checks | Where-Object { !$_.Passed }))
        Checks = $checks.ToArray(); Failure = $failure
        ProcessExitCode = if ($process -and $process.HasExited) { $process.ExitCode } else { $null }
        StandardError = if ($process -and $process.HasExited) { $process.StandardError.ReadToEnd() } else { $null }
    }
    $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $output 'report.json') -Encoding UTF8
}
$report | ConvertTo-Json -Depth 8
if (!$report.Passed) { exit 1 }
