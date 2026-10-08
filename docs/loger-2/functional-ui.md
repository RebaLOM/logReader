# Совместимость дополнительных окон LOGER 2.0

Инвентаризация выполнена по исходникам UI до изменения production-кода. Это часть общей матрицы: обработчики главной формы и вычислительное ядро описываются отдельно. Итоговая колонка обновлена по реальным проверкам показанных WinForms-окон в обеих темах при 96 DPI. `UI PASS` относится только к перечисленному сценарию; `PARTIAL PASS / NOT TESTED` отделяет проверенную часть от непроверенных веток. `CODE PRESERVED / NOT TESTED` означает сохранённый путь исходного кода без выполненного поведенческого UI-теста. Чтение кода и снимок окна не доказывают все его команды.

| Функция | Реализация и зависимости | Доступ в исходном UI | Доступ в LOGER 2.0 | Как проверить | Итоговый статус и предел доказательства |
|---|---|---|---|---|---|
| Выбор одного файла или каталога | `MainForm.ShowPickLogSourceDialog`; OpenFileDialog, FolderBrowserDialog | «Обзор» источника, Enter выбирает файл, Escape отменяет | Страница «Обработка», карточка источника | Выбор/отмена обоих вариантов, автопуть результата |
| Статистика CAN ID | `CanLogViewForm.LoadAsync`; `UnknownDevicesScanner.CollectLogIds` | «Посылки» | Страница «Обработка», «Просмотр пакетов» | Проверить число ID/кадров для каждого формата и папки |
| Поиск CAN ID и счётчики | `CanLogViewForm.ApplyFilter`; виртуальный ListView | Поле поиска | То же окно, поле поиска | Подстрока без учёта регистра, сброс, 0 совпадений |
| Фоновое чтение и отмена при закрытии | `CanLogViewForm`, Task.Run, CancellationTokenSource | Закрытие окна во время сканирования | То же | Большой файл; закрытие во время чтения, повторное открытие |
| Ошибки чтения/пустой каталог | `CanLogViewForm.LoadAsync` | Сообщение с причиной | Тематический диалог | Недоступный путь, пустая папка; приложение остаётся работоспособным |
| Включение устройств и параметров | `Devices_ParametrsForm`; OutputFilter | «Устройства и параметры», дерево флажков | Страница «Декодирование», «Устройства и параметры» | Независимое включение устройства и параметра; счётчики; дерево клавиатурой |
| Изменения фильтров на копии | `ApplyToTarget`; cloned bool arrays | OK применяет, Cancel не меняет | То же | Поменять флажки → Cancel, затем OK; compare dictionary |
| Поиск устройств/параметров | `BuildTree` | Поле поиска вкладки «Устройства» | То же | ID, имя параметра, фильтр не уничтожает скрытые состояния |
| Включить/выключить всё | `SetAll` обходит всю модель | Кнопки, включая скрытые после поиска узлы | То же | Включить при активном фильтре; сбросить поиск; все состояния изменены |
| Сверка описания с логом | `RefreshLogDeviceLists`, missing/matched lists | Вкладка «Сверка с логом», отдельный поиск | То же | Отсутствующий, совпавший ID, нет источника, пустой поиск |
| Создание XLSX/DBC/DBF описания | `FileKindPromptForm.SelectedKind` | «Создать...», три кнопки типа | «Декодирование», «Создать…» | Все три типа, Escape, закрытие без выбора |
| Редактор описаний XLSX/DBC/DBF | `DevicesEditorForm.LoadFromFile`; DeviceExcelFile, DbcFile, DbfFile | «Редактор» | «Декодирование», «Редактор» | Загрузить/сохранить все три формата; сверить round trip |
| Поиск и диапазонные фильтры описаний | `RefreshGrid`; MakeFormatFilterCombo, InOptionalRange | Имя/ID, Standard/Extended, DLC от/до, сигналы от/до | Сохраняются в редакторе | Поиск с 0x, регистр, каждый диапазон, неверный ввод |
| CRUD сообщений | `AddNew/EditSelected/DeleteSelected` | Добавить/Изменить/Удалить, double click | То же | Добавление, дубли ID/имени, отмена удаления, edit filtered row |
| Сохранение неизвестных строк DBC/DBF | DbcDatabase.PreservedLineCount; WriteDatabase | Информация над таблицей | То же | Файл с комментариями/unsupported directives после записи |
| Защита CAN FD от усечения редактором | `ConfirmClassicDlc` | Открытие DLC > 8 сообщает об ограничении | То же | Посылка >8 байт не изменяется, обработка сохраняется |
| Ошибка чтения не открывает пустой редактор | `LoadFailed` | MessageBox, caller checks LoadFailed | То же | Повреждённый файл не перезаписывается |
| Безопасное сохранение и dirty close | `TrySaveAll`, EnsureFileNotLocked; MessageEditFormHelpers | «Сохранить изменения»; закрытие Yes/No/Cancel | То же | Заблокированный файл, invalid path; правки остаются в памяти |
| Детали сообщения DBC | `DbcMessageEditForm`, Clone | Имя, hex ID, Standard/Extended, DLC 1..8 | То же окно в новой теме | DBC symbol rules, ID 11/29 bit, пустые сигналы подтверждение |
| Детали сообщения XLSX | `XlsxMessageEditForm`, CloneDefinition | MessageName, ID, Standard/Extended, DLC; ID readonly при редактировании | То же | ID readonly; переиндексация FieldIndex, проверка DLC |
| Список и CRUD сигналов | DbcMessageEditForm/XlsxMessageEditForm | Add/Edit/Delete, double click | То же | Дубли без учёта регистра, удаление подтверждается |
| Фильтры сигналов | RefreshGrid; Type/Order/Length filters | Name, int/unsigned/BIN, Intel/Motorola, длина от/до | То же | BIN показывается только при «Все» в Order; invalid length status |
| Синхронизация списка и карты битов | `WirePayloadSelectionSync`, `CanPayloadGridFactory` | Щелчок строки или цветного битового участка | То же | Выбор в обе стороны; пустое место снимает выделение |
| Сохранение DBC метаданных | TryCommitChanges; OriginId/ExtraLines, signal OriginName/TrailingLines/MultiplexIndicator/ValueType | Через редактор DBC | То же | Round trip с multiplex, receiver, trailing directives |
| Редактор DBC сигнала | `DbcSignalEditForm` | Type, byte index, start bit, length 1..64, factor, offset, unit, endian | То же | Symbol name/dedup; finite factor/offset; Motorola fit; factor=0 → 1 |
| Raw/physical границы сигнала | ComputeRawRange, DbcPhysicalValue.PhysicalBoundsFromRaw | Readonly Min/Max hex | То же | signed/unsigned 1,8,64 bits; negative factor/offset |
| Редактор XLSX NUM/BIN | `DeviceFieldRowEditForm` | NUM и BIN; раздельные панели | То же | NUM: имя без пробелов, DLC fit; BIN: один байт, BitStart+Length ≤ 8 |
| Drag selection битов | `CanPayloadGridControl`, BitMath | Mouse drag Intel/Motorola, BIN одной строки | То же | Направления drag, недопустимый диапазон, conflicts, DPI |
| Цветовые соответствия сигналов | `CanPayloadGridPalette.AssignColors` | Swatch в таблице и те же цвета на битах | Сохраняются; нейтральный фон адаптируется к теме | Список/карта сохраняют соответствие, conflict маркировка |
| Редактор составных параметров | `CompositeEditorForm`; CompositeExcelFile | Добавить/Изменить/Удалить/Сохранить, double click, Enter/Delete, Alt мнемоники | «Декодирование», «Составные параметры» | Все команды, duplicate block+param; dirty close |
| Пропущенные invalid composite rows | ReadAll skippedRows, TrySaveAll | Число ошибок и подтверждение удаления при сохранении | То же | Ответ No сохраняет оригинал и правки; Yes удаляет только после записи |
| Детали составного параметра | `CompositeParamEditForm`, CompositeSignal/Piece | Block, Param, Scale, Offset, Unit, signed, Min/Max | То же | Пустой block default; min≤max; неверные числа блокируют OK |
| Составные куски и порядок | MoveRow, SourceID, Byte, BitStart, BitLen, Trigger | Add/delete piece, Up/Down, editable grid | То же | 0..7 byte/start, 1..8 length, hex ID≤1FFFFFFF, минимум один кусок |
| Единственный trigger | CellValueChanged, CurrentCellDirtyStateChanged | Флажок в grid; default последний источник | То же | Включение второго выключает первый; default correct |
| Выходные форматы | `SaveOptionsForm` SelectedOutputFormat | XLSX, CSV, CSV ДСТ Коннект | Постоянный inspector экспорта + диалог настройки | Каждый формат и синхронизация пути расширения |
| Параметры ДСТ Коннект | Numeric block period 1..3600000; anchor 0..10000000 | Условная панель при CSV ДСТ Коннект | То же | Period, anchor=0 авто, ненулевой якорь; visibility |
| Дополнительная строка ID | IncludeDeviceIdHeaderRow | Флажок над именами параметров | То же | OFF сохраняет прежний формат, ON добавляет строку |
| Режим пакета | BatchOutputMode | PerInputFile/MergeToSingleFile/SplitTrcByDate | То же | Все режимы; режим задаётся и при одиночном источнике |
| Фильтр форматов каталога | LogFolderScanner, checked format list | Все/TRC/ASC/CSV/Legacy CSV/CANfox, counts | То же | Only-present items; all state, none disables OK; no folder disabled |
| Конвертация без декодирования | `FormatConversionDialog`, TrcToAscConverter, MatrixCsvToAscConverter | «Смена формата», пары TRC→ASC, CSV→ASC | Навигация «Конвертация» | Обе пары, Task.Run busy, повторный запуск |
| Пути конвертации и validation | TryPrepareConversion, UpdateDefaultOutputPath | Browse input/output, manual paths | То же | Folder rejected, wrong ext, same source/output, nonexistent folder, locked output |
| Открытие результата конвертации | Set/ClearConversionResult, Process.Start UseShellExecute | «Открыть» после успеха | То же | Editing path invalidates result; missing result logs error |
| Справка и поиск | `HelpForm`, HelpContent, HelpRenderer | Немодальная «Помощь», дерево тем и поиск | «Справка» в навигации; сохраняется отдельное окно | Search title/body, ancestors retained, highlight, no matches |
| DPI и ресайз | `UiScaling.Apply`, ScaleControl; designer font autoscale | Все формы/resize | Сохраняется без перехода UI-технологии | 100/125/150/200% DPI; minimum bounds; texts and buttons |
| Светлая/тёмная темы (новая функция) | ThemeManager, centralized semantic palette | Отсутствовало | Переключатель в rail, сохраняется на диск | Все формы/таблицы/диалоги; открытые окна; restart |

## Замечания к источнику истины

* В проекте нет дополнительных context menu: поиск по UI не обнаружил `ContextMenuStrip`. Нельзя заявлять о переносе несуществующих функций.
* Сортировка grid редакторов сообщений/сигналов намеренно выключена: индекс строки хранится в `Tag` и должен соответствовать модели. Статистика CAN ID сортируется при загрузке без клика заголовка. Составная таблица — стандартная сортировка столбцов исходной версии; её поведение следует проверять отдельно.
* Выбор фильтров, экспортных параметров и путей исходная версия хранит в сессии; новый сохранённый выбор темы не заменяет эти значения.
* Редактор сообщений ограничен DLC 1..8, тогда как ядро читает CAN FD до 64 байт. Ограничение было явным и не должно превращаться в усечение данных.
* Защитные Yes/No/Cancel, проверки locked file, skipped composite rows, LoadFailed и сохранение неизвестных строк DBC/DBF являются полноценными функциями.
* Нативные системные OpenFileDialog/SaveFileDialog/FolderBrowserDialog используют оформление Windows. Тематические окна предупреждений и все собственные формы входят в дизайн-систему.

## Постредизайновая проверка

Полный harness выполнил по **49/49 PASS** в Dark и Light; дополнительный набор — по **3/3 PASS**. Артефакты: `artifacts/redesign/final-dark/report.json`, `final-light/report.json` и `extra-report.json` в тех же каталогах. Исходная UI-сборка проверена тем же harness: 43 основных и 3 дополнительных сценария PASS; новые проверки навигации/темы/тематических подтверждений к исходному UI неприменимы. Совпадение CSV исходного и нового UI проверено байт в байт.

`tools/Loger.UiAudit/Program.cs` вызывает реальные обработчики кнопок и модальные подтверждения на STA-потоке. Снимки `*-window.png` получены через `PrintWindow` показанного HWND; `*.png` — через `DrawToBitmap`. Инвентари controls и SHA256 проверенной сборки записаны в JSON. Проверка логического обработчика клавиатуры через reflection не считается физическим нажатием клавиш.

Отдельное приложение проверено встроенной Windows UI Automation без reflection: 8/8 PASS в `artifacts/redesign/uia-final/uia-report.json`. Проверены навигация, переключатель темы и файл предпочтений, ввод через ValuePattern, обработка через InvokePattern, отмена 2 000 000 кадров на странице декодирования, последующий пакет из двух файлов и ошибка отсутствующего источника. Перезагрузка приложения с сохранённой темой проверяется отдельным завершающим сценарием; до его результата имеет статус NOT TESTED.

Автоматические проверки ядра описаны в [functional-core.md](functional-core.md); их PASS не заменяет UI-тест редактора. Нативные системные file/folder pickers, открытие результата shell-командой, физическая клавиатура/мышь, screen reader, реальные 125/150/200% DPI и многочасовые сессии **NOT TESTED**. В обеих темах проверено отображение всех собственных типизированных форм и диалога выбора источника; это не равнозначно выполнению каждого варианта validation/CRUD.
