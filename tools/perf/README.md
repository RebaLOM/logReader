# Воспроизводимый baseline ядра

`Loger.Perf` запускает скомпилированные алгоритмы из `logReader.dll`; производственные исходники, форматы, видимость классов и настройки сборки ядра не меняются. Внутренние точки входа вызываются через заранее найденные `MethodInfo`. Один вызов reflection на целую операцию — постоянная малая накладная стоимость и одинаковая в обеих версиях.

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

Этот harness не доказывает скорость UI, число кадров анимации, плавность прокрутки, холодный запуск и отсутствие многочасовых утечек; такие показатели здесь **NOT TESTED**. 100 000 кадров — воспроизводимый большой синтетический случай, а не гарантия для любого размера/вида реального лога.
