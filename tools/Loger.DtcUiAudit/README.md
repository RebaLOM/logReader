# Нативная проверка окна DTC

Скрипт запускает отдельный LOGER, проверяет доступность кнопки диагностики,
выключенную по умолчанию опцию добавления DTC в обычный результат и разрыв данных 2 секунды.
Затем открывает выбранный лог или папку DST, при заданном `SearchSpn` ищет числовой
SPN, выбирает вкладку снимков и проверяет таблицу и исходные данные выбранной строки. Отдельный отчёт приложения
не создаётся: JSON и PNG являются только артефактами теста.

Используются встроенные Windows UI Automation и Win32, без дополнительных пакетов.
Запускать в интерактивной Windows-сессии через Windows PowerShell 5.1 в STA:

```powershell
& "$env:WINDIR/System32/WindowsPowerShell/v1.0/powershell.exe" `
  -NoProfile -STA -ExecutionPolicy Bypass `
  -File tools/Loger.DtcUiAudit/Invoke-DtcUiSmoke.ps1 `
  -AppPath artifacts/dtc/app/LOGER.exe `
  -InputPath 'C:/Users/Re/OneDrive/Desktop/DST_LOGER/DST/trace_2025-10-16T06_45_04_085033.trc' `
  -ExpectedSummaryRows 0 `
  -ExpectedInitialTab 1 `
  -ExpectedStatus 'Диагностические посылки прочитаны; подтверждённых ошибок нет' `
  -RequireCompleteRaw `
  -OutputDirectory artifacts/dtc/ui/new-run
```

Для проверки найденного кода используйте `контрвращение_085008.trc` и
`-SearchSpn 32770`. Для анализа папки передайте `DST` целиком.
Параметры `ExpectedStatus` и `ExpectedInitialTab` проверяют пояснение результата и
автоматический выбор вкладки до действий контроллера.

Для каждого запуска задавайте новый каталог артефактов вне папки исходных логов.
Скрипт изолирует `APPDATA`, `LOCALAPPDATA`, `TEMP`, `TMP`, проверяет SHA-256 исходников
до и после анализа и завершает только собственный тестовый процесс.

Поиск UIA-контролов пропускает содержимое виртуальных таблиц, чтобы не обходить
сотни тысяч ячеек при поиске соседнего текстового поля. Модальные окна находятся
по PID и нативному заголовку. Для выбора второй вкладки используется скалярное
сообщение `TCM_SETCURFOCUS`: индекс и границы заголовков, возвращаемые текущим
провайдером UIA WinForms, смещены. Сообщения с указателями в чужой процесс не
используются.

Успешный запуск сохраняет `report.json`, `main.png`, `save-options-default.png`,
`diagnostics-summary.png`, `diagnostics-snapshots.png`; код завершения — 0.
