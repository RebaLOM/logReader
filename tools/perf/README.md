# Воспроизводимый baseline ядра

`Loger.Perf` запускает скомпилированные алгоритмы из `logReader.dll`; сам harness не изменяет производственные исходники, форматы, видимость классов или настройки сборки ядра. Внутренние точки входа вызываются через заранее найденные `MethodInfo`. Один вызов reflection на целую операцию — постоянная малая накладная стоимость и одинаковая в обеих версиях.

```powershell
# С корня репозитория. Сеть не нужна при наличии пользовательского кэша NuGet.
$env:NUGET_PACKAGES = 'C:\Users\Re\.nuget\packages'
dotnet restore tools/perf/Loger.Perf.csproj --source 'C:\Users\Re\.nuget\packages' --ignore-failed-sources
dotnet build tools/perf/Loger.Perf.csproj -c Release --no-restore
dotnet run --project tools/perf/Loger.Perf.csproj -c Release --no-build -- --root . --label baseline --runs 5
# После изменений — тот же бинарный harness и те же настройки:
dotnet run --project tools/perf/Loger.Perf.csproj -c Release --no-build -- --root . --label after --runs 5
powershell -NoProfile -ExecutionPolicy Bypass -File tools/perf/compare.ps1
```

При отсутствии локального кэша используется обычный `dotnet restore`; путь к кэшу — особенность этой машины. Команды тестов:

```powershell
dotnet restore logReader.Tests/logReader.Tests.csproj --source 'C:\Users\Re\.nuget\packages' --ignore-failed-sources
dotnet test logReader.Tests/logReader.Tests.csproj -c Release --no-restore
```

Каждый сценарий имеет один прогрев и пять измеряемых повторов. До повторов выполняется полная GC; генерация входов, загрузка DBC и проверка выходов исключены из измерений. Файловый кэш ОС не очищается; измеряется обычный повторный запуск. Release, .NET 10, workstation GC, культура ru-RU. В отчёте сохранены все измерения, CPU-time процесса, сумма выделенных managed bytes, managed heap до/после, WorkingSet после операции и PeakWorkingSet за всё время процесса. Последний показатель **не** означает изолированный пик сценария. CPU-time не заменяет измерение загрузки CPU интерфейса.

Детерминированные входы: 2 000 и 100 000 кадров, четыре CAN-ID, по три сигнала (raw byte, Intel с factor/offset, знаковый Motorola), DLC=8; в TRC также вставлены невалидные строки. Для малого набора генерируются ASC, legacy CSV (`accepted=0` на каждом седьмом кадре), matrix CSV и CANfox TXT. Два малых TRC содержат переход через полночь. Дата изменения matrix CSV фиксируется, потому что конвертер использует её для ASC-заголовка. Подробности и SHA256 каждого входа находятся в JSON.

17 сценариев: разбор/декодирование/накопление TRC; отдельный CSV и XLSX export малого/большого набора; полная цепочка большого TRC→CSV; ASC, legacy CSV, matrix CSV, CANfox, ДСТ→CSV; per-input, merge, split-by-date; два конвертера ASC. В результатах экспорта рядов на устройство 500/25 000; шаговые CSV содержат 2 000 строк. Для batch поле Rows обозначает число созданных файлов.

Байтовый SHA256 CSV/ASC и семантический XLSX SHA256 обязан совпадать между всеми повторами: harness прекращает работу при расхождении. Семантический XLSX hash включает данные и структуру без изменчивых метаданных ZIP. `compare.ps1` дополнительно проверяет набор входов, fingerprint всех `.cs`/`.csproj` ядра и результат каждого сценария.

Генерируемые входы и выходы находятся в игнорируемом `artifacts/perf/<label>/`. Отчёты JSON и Markdown — `docs/loger-2/performance/`. Не запускайте рядом сборки одного проекта и тяжёлый UI-аудит: они искажают время или конфликтуют за `obj`.

После уточнения исходной функциональной версии пользователем ядро обновлено до `bfbea81`. Пара `baseline.json` / `after.json` относится к прежнему ядру `6310fd5` и сохранена как история редизайна. Для актуальной версии используются `baseline-bfbea81.json` (независимая сборка ядра из Git `bfbea81`) и `after-bfbea81.json` (текущая ветка), результат — `comparison-bfbea81.md`. Сравнивать выходы исправленного ядра с `6310fd5` как обязательное побайтное равенство некорректно: DST Time/Step и дробная ось CSV изменены намеренно. Проверка актуальной пары:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/perf/compare.ps1 `
  -Before docs/loger-2/performance/baseline-bfbea81.json `
  -After docs/loger-2/performance/after-bfbea81.json `
  -Report docs/loger-2/performance/comparison-bfbea81.md
```

Этот harness не доказывает скорость UI, число кадров анимации, плавность прокрутки, холодный запуск и отсутствие многочасовых утечек; такие показатели здесь **NOT TESTED**. 100 000 кадров — воспроизводимый большой синтетический случай, а не гарантия для любого размера/вида реального лога.

Итоговая пара `bfbea81` использует одинаковый opt-in `--fresh-outputs`:

```powershell
dotnet run --project tools/perf/Loger.Perf.csproj -c Release --no-build -- `
  --root . --label after-bfbea81 --runs 5 --fresh-outputs
```

До каждого прогрева/sample удаляется только прежний synthetic output, вне таймера. Абсолютный путь обязан лежать внутри `artifacts/perf/<label>`; входные файлы защищены. Для batch разрешены только известные CSV/XLSX outputs. Default без флага сохраняет историческую методику. JSON записывает `FreshOutputs`; `compare.ps1` требует одинаковый режим и отклоняет смешанные измерения. Независимый archive baseline запущен с тем же кодом harness и флагом.

Причина перехода: три прогона повторной замены CSV прервались на `File.Replace`, в том числе вне sandbox. Причина не установлена; core не менялся. Новая методика не измеряет atomic-overwrite time и не объявляет проблему исправленной. Детали, ограничения и все числа — `docs/loger-2/performance/comparison-bfbea81.md` и `docs/loger-2/evidence/bfbea81/benchmark-failed-runs.md`.
