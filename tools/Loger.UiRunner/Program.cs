using System.Diagnostics;
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA2;

// Повторяемые скриншоты LOGER для ui-audit (неразрушающая навигация).
if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: Loger.UiRunner <exe> <outputDir> [section]");
    return 2;
}

string exe = Path.GetFullPath(args[0]);
string outputDir = Path.GetFullPath(args[1]);
string? section = args.Length >= 3 ? args[2] : null;

if (!File.Exists(exe))
{
    Console.Error.WriteLine($"EXE not found: {exe}");
    return 2;
}

// Реальные точки входа MainForm (кастомный NavigationItem + кнопки).
var sectionTargets = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
{
    ["Обработка"] = ["navProcess"],
    ["Справка"] = ["navHelp"],
    ["Конвертация"] = ["navConvert"],
    ["Устройства и параметры"] = ["buttonDevicesParams"],
};

if (section != null && !sectionTargets.ContainsKey(section))
{
    Console.Error.WriteLine(
        "Section is not allowlisted. Allowed: " +
        string.Join(", ", sectionTargets.Keys));
    return 2;
}

Directory.CreateDirectory(outputDir);

var startInfo = new ProcessStartInfo(exe)
{
    WorkingDirectory = Path.GetDirectoryName(exe)!,
    UseShellExecute = false
};

using var app = FlaUI.Core.Application.Launch(startInfo);
using var automation = new UIA2Automation();

try
{
    var window = app.GetMainWindow(automation, TimeSpan.FromSeconds(20));
    if (window == null)
        throw new Exception("Main window not found.");

    window.SetForeground();
    Thread.Sleep(1000);

    if (section != null)
    {
        AutomationElement? target = FindSectionTarget(window, section, sectionTargets[section]);
        if (target == null)
            throw new Exception($"Navigation target not found: {section}");

        target.Click();
        Thread.Sleep(1000);

        // Диалог «Устройства и параметры» — отдельное окно.
        if (section.Equals("Устройства и параметры", StringComparison.OrdinalIgnoreCase))
        {
            var dialog = window.ModalWindows
                .FirstOrDefault(w =>
                    (w.Name ?? string.Empty).Contains("Устройства", StringComparison.OrdinalIgnoreCase));
            if (dialog != null)
                window = dialog;
        }
    }

    string screenshotPath = Path.Combine(outputDir, "screen.png");
    window.CaptureToFile(screenshotPath);

    var elements = new List<object>();
    foreach (var element in window.FindAllDescendants().Take(600))
    {
        try
        {
            var bounds = element.BoundingRectangle;
            elements.Add(new
            {
                name = element.Name,
                automationId = element.AutomationId,
                controlType = element.ControlType.ToString(),
                bounds = new
                {
                    x = bounds.X,
                    y = bounds.Y,
                    width = bounds.Width,
                    height = bounds.Height
                }
            });
        }
        catch
        {
            // Недоступный UIA-узел пропускаем.
        }
    }

    var report = new
    {
        section = section ?? "Main window",
        capturedAt = DateTimeOffset.Now,
        screenshot = screenshotPath,
        elements
    };

    File.WriteAllText(
        Path.Combine(outputDir, "elements.json"),
        JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));

    Console.WriteLine($"Screenshot: {screenshotPath}");
    Console.WriteLine($"UI elements discovered: {elements.Count}");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}
finally
{
    try { app.Close(); }
    catch { /* best-effort */ }
}

static AutomationElement? FindSectionTarget(
    AutomationElement root,
    string sectionName,
    string[] automationIds)
{
    var descendants = root.FindAllDescendants();

    foreach (string id in automationIds)
    {
        var byId = descendants.FirstOrDefault(e =>
            string.Equals(e.AutomationId, id, StringComparison.OrdinalIgnoreCase));
        if (byId != null)
            return byId;
    }

    // Кастомный NavigationItem может отдавать Name из Text.
    return descendants.FirstOrDefault(e =>
        string.Equals(e.Name, sectionName, StringComparison.OrdinalIgnoreCase)
        && (
            e.ControlType == ControlType.Button
            || e.ControlType == ControlType.TabItem
            || e.ControlType == ControlType.ListItem
            || e.ControlType == ControlType.Custom
            || e.ControlType == ControlType.Pane
            || e.ControlType == ControlType.Text
        ));
}
