param(
    [string]$Before = 'docs/loger-2/performance/baseline.json',
    [string]$After = 'docs/loger-2/performance/after.json',
    [string]$Report = 'docs/loger-2/performance/comparison.md'
)
$ErrorActionPreference = 'Stop'
$baseline = Get-Content -LiteralPath $Before -Raw -Encoding UTF8 | ConvertFrom-Json
$current = Get-Content -LiteralPath $After -Raw -Encoding UTF8 | ConvertFrom-Json
$freshBaseline = [bool]$baseline.FreshOutputs # Missing historical field means the original reuse mode.
$freshCurrent = [bool]$current.FreshOutputs
$sameFreshOutputs = $freshBaseline -eq $freshCurrent
$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('# Сравнение производительности ядра LOGER')
$lines.Add('')
$sameCore = $baseline.CoreSourceSha256 -eq $current.CoreSourceSha256
$sameInputs = ($baseline.Inputs | ConvertTo-Json -Depth 5 -Compress) -ceq ($current.Inputs | ConvertTo-Json -Depth 5 -Compress)
$lines.Add("Идентичное ядро: $sameCore. Идентичные входные файлы: $sameInputs.")
$lines.Add('')
$lines.Add("FreshOutputs до / после: $freshBaseline / $freshCurrent. Идентичная подготовка результатов: $sameFreshOutputs. Отсутствующее поле исторического JSON означает False.")
$lines.Add('')
if ($freshBaseline -or $freshCurrent) {
    $lines.Add('Режим --fresh-outputs: перед прогревом и каждым sample удаляется только предыдущий синтетический результат внутри artifacts/perf/<label>; входы защищены. Подготовка выполняется вне таймера. Измеряется создание нового результата, время атомарной перезаписи существующего файла не измеряется. Смешение этой методики с прежним reuse mode не проходит проверку.')
    $lines.Add('')
}
if ($baseline.Label -ceq 'baseline-bfbea81' -and $current.Label -ceq 'after-bfbea81' -and $freshBaseline -and $freshCurrent) {
    $lines.Add('Оба итоговых bfbea81 прогона выполнены вне sandbox с одинаковым --fresh-outputs. Три предыдущих after-прогона с повторной заменой outputs прервались: два внутри sandbox, один вне него; File.Replace сообщил «Не удается удалить заменяемый файл» на разных синтетических CSV. Они не включены в итоговые JSON. Причина ошибки не установлена; алгоритмы ядра не менялись. Проверки atomic replacement остаются в тестовом наборе. Диагностика и сохранившиеся неполные outputs записаны в artifacts/integration-bfbea81/benchmark-failed-runs.md и artifacts/perf/after-bfbea81-failed-*; пользовательские файлы не затронуты.')
    $lines.Add('')
}
$sameAssembly = $baseline.CoreAssemblySha256 -ceq $current.CoreAssemblySha256
$lines.Add("Байтовый SHA256 сборки ядра совпадает: $sameAssembly. До: $($baseline.CoreAssemblySha256); после: $($current.CoreAssemblySha256).")
$lines.Add('')
$lines.Add('Сборка SDK содержит автоматически созданный AssemblyInformationalVersion с Git commit; метаданные и ссылка на PDB также зависят от пути сборки. Поэтому разные сборки могут иметь разные байты DLL при одинаковых исходниках ядра. Проверка совместимости ниже требует одинаковых core .cs/.csproj, входов и выходов, а не одинаковых build metadata.')
$lines.Add('')
$lines.Add('Медианы Release; по одному прогреву и пять измеряемых повторов. Обработка идёт в отдельном консольном процессе без WinForms. Время проверки SHA256 не включено.')
$lines.Add('')
$lines.Add('| Сценарий | До, мс | После, мс | Изменение | Совпадение результата |')
$lines.Add('|---|---:|---:|---:|---|')
$allOutputsMatch = $true
foreach ($old in $baseline.Scenarios) {
    $new = $current.Scenarios | Where-Object { $_.Scenario -ceq $old.Scenario } | Select-Object -First 1
    if ($null -eq $new) { throw "Missing scenario: $($old.Scenario)" }
    $match = $new.Hash -ceq $old.Hash -and $new.Rows -eq $old.Rows
    $allOutputsMatch = $allOutputsMatch -and $match
    $change = 100 * ($new.MedianElapsedMs / $old.MedianElapsedMs - 1)
    $status = if ($match) { 'PASS' } else { 'FAIL' }
    $lines.Add(('| {0} | {1:F2} | {2:F2} | {3:+0.0;-0.0;0.0}% | {4} |' -f $old.Scenario, $old.MedianElapsedMs, $new.MedianElapsedMs, $change, $status))
}
$lines.Add('')
$comparisonPass = $sameInputs -and $sameCore -and $sameFreshOutputs -and $allOutputsMatch
$comparisonStatus = if ($comparisonPass) { 'PASS' } else { 'FAIL' }
$lines.Add("Проверка парного сравнения исходников, входов, подготовки и выходов: $comparisonStatus.")
$lines.Add('')
$lines.Add('| Сценарий | CPU до / после, мс | Выделено managed до / после, MiB | WorkingSet после операции до / после, MiB |')
$lines.Add('|---|---:|---:|---:|')
foreach ($old in $baseline.Scenarios) {
    $new = $current.Scenarios | Where-Object { $_.Scenario -ceq $old.Scenario } | Select-Object -First 1
    $lines.Add(('| {0} | {1:F2} / {2:F2} | {3:F2} / {4:F2} | {5:F2} / {6:F2} |' -f $old.Scenario, $old.MedianCpuMs, $new.MedianCpuMs, ($old.MedianAllocatedBytes / 1MB), ($new.MedianAllocatedBytes / 1MB), ($old.MedianWorkingSetAfterBytes / 1MB), ($new.MedianWorkingSetAfterBytes / 1MB)))
}
$lines.Add('')
$lines.Add('Managed allocation — суммарные выделения за операцию, не удерживаемая память. WorkingSet измерен после операции и включает процесс; измерения разных сценариев не изолированы. Короткие CPU-измерения ограничены разрешением счётчика. Полные samples и managed heap до/после сохранены в JSON.')
$lines.Add('')
$lines.Add('Байтовые SHA256 сравниваются для CSV/ASC. Для XLSX сравнивается семантический SHA256: имена и размеры листов, фиксация строк/колонок, объединения, координаты, типы и значения ячеек, числовые форматы; метаданные ZIP исключены.')
$lines.Add('')
$lines.Add('Изменения времени на таком коротком прогоне зависят от фоновой нагрузки, JIT и дискового кэша. Совпадение алгоритмов и выходов проверяется отдельно; значимое замедление требует повторного измерения в спокойной среде. PeakWorkingSet — максимум за время всего процесса, не выделенный пик отдельного сценария.')
$lines.Add('')
$lines.Add('UI, запуск приложения, навигация, прокрутка больших таблиц, многочасовые утечки памяти здесь NOT TESTED: их необходимо оценивать отдельным UI-аудитом.')
Set-Content -LiteralPath $Report -Value $lines -Encoding UTF8
$lines | Write-Output
if (-not $comparisonPass) { exit 1 }
