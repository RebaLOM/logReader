# Публикация LOGER 2.0

Обычная сборка `dotnet build logReader.UI/logReader.UI.csproj -c Release` остаётся доступной и не требует ReadyToRun. Для распространения на Windows x64 используется предварительная компиляция **только `LOGER.dll`**. Это сокращает первый JIT UI; `logReader.dll` и все сторонние DLL остаются побайтно такими же, как в обычной компиляции. Проверка SHA-256 встроена в скрипт публикации.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Publish-Loger.ps1
```

Понадобятся .NET 10 SDK, доступные NuGet packages и компилятор Crossgen2 для выбранного RID. Итоговый `LOGER.exe` находится в `artifacts/release/win-x64`; рядом должны оставаться DLL/JSON. Это framework-dependent публикация: нужен установленный .NET 10 Windows Desktop Runtime подходящей архитектуры. Runtime не включается, trimming, single-file и Native AOT не применяются.

На машине аудита SDK 10.0.401 / runtime 10.0.12; официальный пакет `Microsoft.NETCore.App.Crossgen2.win-x64 10.0.12` загружен в локальный feed. Для повторения без обращения к NuGet:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Publish-Loger.ps1 `
  -LocalPackageFeed artifacts/compiler-feed -PackageFallback C:\Users\Re\.nuget\packages
```

Crossgen2 — средство SDK, а не новая production dependency. Его распаковка находится в игнорируемом `artifacts/r2r/packages`; пользовательский cache служит входом. `UiOnlyReadyToRun.targets` исключает из обработки все publish assemblies кроме `LOGER.dll`. Поэтому оптимизация упаковки не меняет алгоритмы CAN и библиотеку Excel. Другие RID допускаются параметром, но фактическая проверка выполнена только для **win-x64**.

ReadyToRun сохраняет IL вместе с native code и увеличивает размер UI DLL; значение оптимизации проверяется измерениями, а не предполагается из настройки. Принцип и ограничения описаны в [официальной документации Microsoft](https://learn.microsoft.com/en-us/dotnet/core/deploying/ready-to-run). Фактические startup/working-set значения и обычная IL-сборка отдельно указаны в `docs/loger-2/report.md`.
