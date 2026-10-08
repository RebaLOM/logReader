param(
    [string]$Baseline = 'artifacts/baseline/ui-final',
    [string]$Dark = 'artifacts/redesign/final-dark',
    [string]$Light = 'artifacts/redesign/final-light',
    [string]$Destination = 'docs/loger-2'
)
$ErrorActionPreference = 'Stop'
$evidenceDestination = [IO.Path]::GetFullPath($Destination)
$evidenceData = [Collections.Generic.List[object]]::new()
foreach ($source in @(
    @{ theme = 'baseline'; path = $Baseline },
    @{ theme = 'dark'; path = $Dark },
    @{ theme = 'light'; path = $Light }
)) {
    $captureDestination = Join-Path $evidenceDestination ('screenshots/' + $source.theme)
    New-Item -ItemType Directory -Path $captureDestination -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $evidenceDestination 'evidence') -Force | Out-Null
    foreach ($reportName in @('report.json', 'extra-report.json')) {
        $sourceReport = Join-Path $source.path $reportName
        $report = Get-Content -LiteralPath $sourceReport -Raw -Encoding UTF8 | ConvertFrom-Json
        Copy-Item -LiteralPath $sourceReport -Destination (Join-Path $evidenceDestination ('evidence/ui-' + $source.theme + '-' + $reportName))
        foreach ($capture in $report.screenshots) {
            if (-not $capture.windowPath -or -not (Test-Path -LiteralPath $capture.windowPath)) {
                throw "Native capture is missing for $($source.theme)/$($capture.name)"
            }
            $captureFile = [IO.Path]::GetFileName($capture.windowPath)
            Copy-Item -LiteralPath $capture.windowPath -Destination (Join-Path $captureDestination $captureFile)
            $evidenceData.Add(@{ theme = $source.theme; name = $capture.name;
                file = $source.theme + '/' + $captureFile; width = $capture.width; height = $capture.height; dpi = $capture.dpi })
        }
    }
}
$evidenceJson = $evidenceData.ToArray() | ConvertTo-Json -Depth 4 -Compress
$galleryTemplate = @'
<!doctype html>
<html lang="ru"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>LOGER 2.0 — реальные окна до и после</title>
<style>
*{box-sizing:border-box}body{margin:0;background:#111419;color:#eff3f8;font:15px system-ui,sans-serif}
header{padding:28px 32px 20px;position:sticky;top:0;background:#111419ed;border-bottom:1px solid #353d49;z-index:1}
h1{margin:0 0 8px;font-size:26px}p{color:#a5afbd;margin:8px 0;line-height:1.5}
nav{display:flex;gap:8px;margin-top:18px;flex-wrap:wrap}button{border:1px solid #353d49;background:#242a33;color:#eff3f8;padding:9px 16px;font:inherit;cursor:pointer}
button[aria-pressed=true]{background:#f2c94c;color:#211b09;border-color:#f2c94c}button:focus-visible,a:focus-visible{outline:2px solid #f2c94c;outline-offset:3px}
main{display:grid;grid-template-columns:repeat(auto-fit,minmax(min(100%,530px),1fr));gap:24px;padding:24px 32px}
figure{margin:0;padding:16px;background:#1a1e25;border:1px solid #353d49}figure img{display:block;width:100%;height:auto;background:#242a33}
figcaption{margin-bottom:12px;display:flex;justify-content:space-between;gap:12px}small{color:#a5afbd}a{color:#f2c94c}
</style>
<header><h1>LOGER 2.0 — реальные окна до и после</h1><p>Native HWND captures через Windows PrintWindow, 96 DPI. Это доказательства визуальной проверки; они не заменяют функциональные тесты.</p>
<nav aria-label="Версия интерфейса"><button data-theme="baseline">До: 6310fd5</button><button data-theme="dark" aria-pressed="true">После: Dark</button><button data-theme="light">После: Light</button></nav><p id="count"></p></header>
<main id="gallery"></main>
<script>
const captures = __CAPTURE_DATA__;
const gallery = document.getElementById('gallery');
function show(theme) {
  document.querySelectorAll('button').forEach(button => button.setAttribute('aria-pressed', String(button.dataset.theme === theme)));
  gallery.replaceChildren();
  const selected = captures.filter(capture => capture.theme === theme);
  document.getElementById('count').textContent = `${selected.length} снимков. Нажмите изображение для открытия в исходном размере.`;
  for (const capture of selected) {
    const figure = document.createElement('figure');
    const caption = document.createElement('figcaption');
    const name = document.createElement('span'); name.textContent = capture.name;
    const size = document.createElement('small'); size.textContent = `${capture.width} × ${capture.height} · ${capture.dpi} DPI`;
    caption.append(name, size);
    const link = document.createElement('a'); link.href = capture.file; link.target = '_blank'; link.rel = 'noopener';
    const img = document.createElement('img'); img.src = capture.file; img.alt = `${capture.theme}: ${capture.name}`; img.loading = 'lazy';
    link.append(img); figure.append(caption, link); gallery.append(figure);
  }
}
document.querySelectorAll('button').forEach(button => button.addEventListener('click', () => show(button.dataset.theme)));
show('dark');
</script></html>
'@
$galleryHtml = $galleryTemplate.Replace('__CAPTURE_DATA__', $evidenceJson)
[IO.File]::WriteAllText((Join-Path $evidenceDestination 'screenshots/index.html'), $galleryHtml, [Text.UTF8Encoding]::new($false))
Write-Output "Exported $($evidenceData.Count) native captures and six reports to $evidenceDestination"
