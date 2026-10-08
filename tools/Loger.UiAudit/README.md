# LOGER native UI audit

Dependency-free .NET 10 WinForms STA harness that loads a supplied `LOGER.dll` without
building or referencing the application project. The same executable checks the preserved
baseline and the redesigned application.

```powershell
dotnet restore tools/Loger.UiAudit/Loger.UiAudit.csproj --source C:\Users\Re\.nuget\packages --ignore-failed-sources
dotnet build tools/Loger.UiAudit/Loger.UiAudit.csproj -c Release --no-restore
dotnet tools/Loger.UiAudit/bin/Release/net10.0-windows/Loger.UiAudit.dll --assembly artifacts/baseline/app/LOGER.dll --output artifacts/baseline/ui --theme existing
dotnet tools/Loger.UiAudit/bin/Release/net10.0-windows/Loger.UiAudit.dll --assembly artifacts/redesign/app/LOGER.dll --output artifacts/redesign/dark --theme Dark
dotnet tools/Loger.UiAudit/bin/Release/net10.0-windows/Loger.UiAudit.dll --assembly artifacts/redesign/app/LOGER.dll --output artifacts/redesign/light --theme Light
```

Windows desktop access is required. In a restricted process without a usable window
station, `Form.Show()` can fail in GDI+; run the harness with desktop access rather than
interpreting that environmental failure as an application regression.

All fixture logs, outputs, and theme preferences stay under `--output`. The fixtures are
generated locally; no user logs or configurations are used. Test runs can be repeated.

The harness invokes the real button event routes and checks actual outputs:

- Original MainForm action buttons remain connected to their original handlers.
- Empty input and nonexistent output-directory errors reach the journal.
- Small TRC processing creates output, restores idle controls, and remains repeatable.
- Repeated processing returns identical output bytes.
- Cancellation of a 2,000,000-frame fixture removes partial output and restores idle state,
  including while the decoder page is active.
- Parameter filter Cancel preserves target maps; OK commits changes; enable-all affects
  the whole model even while search filters the visible rows.
- All three output and batch mode selections, DST parameters, and device-ID header option
  commit through the actual options dialog button.
- Log ID search and help search work on rendered, shown forms.
- Every existing typed application dialog is constructed and rendered.
- The actual theme button changes and persists the theme in an isolated preferences file.
- Custom confirmation dialogs return Yes/No/Cancel through actual modal button clicks;
  editor deletion and dirty-close Save/Discard/Cancel preserve the expected file semantics.
- Keyboard event routing switches pages and focuses the journal; this does not substitute
  for physical-keyboard testing.

`--extras-only` adds native captures and checks for the source-choice dialog, the DBF
editor, and all six folder format choices (All, TRC, ASC, matrix CSV, legacy CSV, CANfox
TXT), including DST export settings. It writes a separate `extra-report.json` without
overwriting the full audit report.

`Invoke-UiaSmoke.ps1` launches the standalone application and uses Windows UI Automation
against that separate process, without app reflection. It checks navigation, theme
persistence, input through ValuePattern, processing through InvokePattern, validation,
and cancellation followed by a repeated batch. It closes and restarts the app without a
theme environment override to check that the saved preference is loaded.
`--message-contracts-only` compares actual native MessageBox and themed OK-only modal
results when closing the window and when posting Escape key messages. This is message
dispatch testing, not a physical-keyboard check. `Measure-Startup.ps1` measures seven fresh
WinExe processes from `Process.Start` to a nonzero HWND and Windows input-idle, recording
wall time and idle memory. Unlike the in-process form microbenchmark, this includes
runtime bootstrap; it uses warm OS/disk caches.

For default/persisted-theme startup use `Measure-Startup.ps1 -Theme existing`; the child
process then receives no `LOGER_THEME` environment override. The isolated preferences
path remains `<OutputPath>/preferences.json`; placing `{"Theme":"Light"}` or
`{"Theme":"Dark"}` there before measuring covers a saved preference separately from
first launch without a preferences file. Forced `-Theme Light`/`Dark` remain available.

`report.json` records checks, assemblies and SHA-256, timings, working set/managed memory,
GDI/user handles, captures, and control inventories. `*.png` uses `Control.DrawToBitmap`;
`*-window.png` captures the actual shown native HWND via Windows `PrintWindow`. Neither
capture is a design mockup; unrelated desktop content is not captured. Native HWND captures
should be preferred for visual review. A successful PrintWindow return does not itself
prove the resulting pixels are useful; inspect captures visually.

Form construction and showing are measured seven times in one process (configurable with
`--iterations`). The median includes the first sample. The separately reported cold value
starts at harness Main entry and includes assembly loading; it excludes operating-system
process launch and .NET runtime bootstrap. These figures are UI microbenchmarks, not a
claim about full startup, data processing throughput, scrolling frame rate, or CPU load.

NOT TESTED: native file/folder pickers and baseline system MessageBoxes, physical
keyboard/mouse input, expanded ComboBox popups, real monitor DPI changes, sustained
scrolling FPS, CPU utilization profiling, and long-session leak behavior. The full
WinForms harness holds a persistent STA synchronization context around temporary
`DoEvents` loops so application async handlers resume on the UI dispatcher.
