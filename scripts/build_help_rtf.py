# -*- coding: utf-8 -*-
"""Generate Help.rtf for LOGER with cp1251 Cyrillic escapes."""

from pathlib import Path


def esc(text: str) -> str:
    out = []
    for ch in text:
        if ch in "\\{}":
            out.append("\\" + ch)
            continue
        if ch == "\n":
            continue
        if ch == "\r":
            continue
        try:
            b = ch.encode("cp1251")
        except UnicodeEncodeError:
            out.append("?")
            continue
        if len(b) == 1:
            code = b[0]
            if code < 128:
                out.append(ch)
            else:
                out.append(f"\\'{code:02x}")
        else:
            for code in b:
                out.append(f"\\'{code:02x}")
    return "".join(out)


def p(text: str = "") -> str:
    return esc(text) + "\\par\n"


def h1(text: str) -> str:
    return "{\\b " + esc(text) + "}\\par\n"


def h2(text: str) -> str:
    return "\\par\n{\\b " + esc(text) + "}\\par\n"


def h3(text: str) -> str:
    return "{\\b " + esc(text) + "}\\par\n"


def bullet(text: str) -> str:
    return "\\bullet  " + esc(text) + "\\par\n"


def bi(label: str, body: str) -> str:
    return "{\\b " + esc(label) + "} " + esc(body) + "\\par\n"


def build_body() -> str:
    lines = []
    lines.append(h1("Инструкция по обработке CAN логов"))
    lines.append(p())

    lines.append(h2("1. Назначение документа"))
    lines.append(p(
        "Данная инструкция описывает порядок работы с программным модулем "
        "(далее - «Обработчик», программа LOGER) для дешифровки и преобразования "
        "сырых данных в структурированный формат на основе файла формата .dbc "
        "или пользовательской таблицы параметров (.xlsx)."
    ))
    lines.append(p("Поддерживаемые входные форматы логов:"))
    lines.append(bullet(".csv - step-CSV (построчная запись) или matrix CSV (широкий формат, шаг 20 мс);"))
    lines.append(bullet(".trc - pCAN Viewer;"))
    lines.append(bullet(".asc - ASC (Vector и совместимые);"))
    lines.append(bullet(".txt - CANfox / PCAN-View (колонки Date, Time, ID, Data)."))
    lines.append(p("Результат сохраняется в .xlsx или .csv."))

    lines.append(h2("2. Порядок работы"))
    lines.append(p("Для выполнения обработки последовательно выполните шаги ниже."))

    lines.append(h3("2.1. Выбор папки или файла с логами"))
    lines.append(p(
        "Укажите путь к исходному файлу лога или к папке с несколькими логами. "
        "Путь можно ввести вручную в поле «Файл или папка логов» или выбрать кнопками."
    ))
    lines.append(bi("Обзор", "диалог выбора источника: «Файл...» - один лог; «Папка...» - каталог с файлами .csv, .trc, .asc, .txt. При выборе одного файла путь результата подставляется автоматически (имя_result.xlsx в той же папке). При выборе папки - каталог result для пакетной обработки."))
    lines.append(bi("Посылки", "окно просмотра содержимого лога (раздел 3.1). Доступно после указания существующего файла или папки."))

    lines.append(h3("2.2. Файл посылок (описание параметров)"))
    lines.append(p("Укажите файл с описанием CAN-посылок и сигналов - .xlsx или .dbc."))
    lines.append(bi("Обзор", "выбор существующего файла .xlsx или .dbc."))
    lines.append(bi("Устройства и параметры", "окно фильтрации устройств и параметров для результата (раздел 3.2). Перед открытием выполняется сверка ID из лога с файлом посылок."))
    lines.append(bi("Создать... / Редактор", "если файл не выбран - создание нового .xlsx или .dbc и открытие редактора; если файл указан - редактор посылок (раздел 3.3)."))
    lines.append(bi("Строка состояния фильтров", "справа от кнопок: «Устройства: X/Y  Параметры: A/B». До загрузки файла: «Файл посылок не загружен»."))

    lines.append(h3("2.3. Файл составных параметров (опционально)"))
    lines.append(p("Составные параметры собираются из битовых фрагментов нескольких посылок."))
    lines.append(bi("Обзор", "выбор существующего .xlsx."))
    lines.append(bi("Создать .xlsx / Редактор", "создание шаблона или редактор составных параметров (раздел 3.4). Поле можно оставить пустым."))

    lines.append(h3("2.4. Сохранение результата"))
    lines.append(bi("Сохранить в", "для одного файла - полный путь к .xlsx или .csv; для папки логов - каталог результатов (не путь к одному файлу)."))
    lines.append(bi("Обзор", "выбор файла или папки назначения."))
    lines.append(bi("Параметры сохранения", "формат выходного файла и режим пакетной обработки (раздел 3.5)."))
    lines.append(bi("Открыть", "появляется после успешной обработки; открывает результат в программе по умолчанию ОС."))

    lines.append(h3("2.5. Запуск и служебные элементы"))
    lines.append(bi("Журнал", "нижняя область главного окна: ход обработки, формат входного файла, ошибки. Очищается при новом запуске «Обработать» или «Смена формата»."))
    lines.append(bi("Обработать", "запуск дешифровки. Требуются: источник логов, файл посылок, путь сохранения."))
    lines.append(bi("Помощь", "открывает данную справку (немодальное окно)."))
    lines.append(bi("Смена формата", "конвертация лога без дешифровки, например TRC -> ASC (раздел 3.6)."))

    lines.append(h2("3. Описание окон и диалогов"))

    lines.append(h3("3.1. Окно «Посылки лога»"))
    lines.append(p("Анализирует указанный файл или все логи в папке (файлы верхнего уровня)."))
    lines.append(bi("Поиск по ID посылки", "фильтрация списка по подстроке ID."))
    lines.append(bi("Счётчик", "«Уникальных ID: N   Всего посылок: M»; при поиске - количество найденных ID и посылок."))
    lines.append(bi("Таблица", "колонки ID посылки и Кол-во посылок - сколько раз каждый ID встретился в логе."))

    lines.append(h3("3.2. Окно «Устройства и параметры»"))
    lines.append(p("Вкладка «Устройства»:"))
    lines.append(bullet("Поиск - фильтрация списка по ID устройства."))
    lines.append(bullet("Группа устройства - кнопка Включено/Выключено исключает всё устройство из результата."))
    lines.append(bullet("Строки параметров - кнопки Вкл/Выкл для отдельных колонок."))
    lines.append(bullet("Включить все / Выключить все - массовое управление фильтрами."))
    lines.append(p("Составные блоки из файла составных параметров отображаются как отдельные устройства."))
    lines.append(p("Вкладка «Сверка с логом»:"))
    lines.append(bullet("В логе, но нет в файле посылок - ID без описания в .xlsx/.dbc."))
    lines.append(bullet("Есть и в логе, и в файле посылок - ID с описанием и данными в логе."))
    lines.append(p("Фильтры сохраняются до смены файла посылок и применяются при нажатии «Обработать»."))

    lines.append(h3("3.3. Редактор посылок (XLSX / DBC)"))
    lines.append(p("Список посылок: имя, ID (hex), формат, DLC, число сигналов. Кнопки Добавить, Изменить, Удалить; двойной щелчок - редактирование."))
    lines.append(p("DBC: имя, ID, DLC, Standard/Extended, таблица сигналов (бит, длина, порядок байт, масштаб, смещение)."))
    lines.append(p("XLSX: DeviceID, MessageName, поля типа NUM (числовой сигнал) или BIN (битовое поле в байте)."))
    lines.append(p("Перед редактированием закройте файл в Excel или другой программе."))

    lines.append(h3("3.4. Редактор составных параметров (XLSX)"))
    lines.append(p("Список: блок, параметр, длина в битах, триггер, источники (SourceID:Byte.BitStart+BitLen)."))
    lines.append(p("Куски склеиваются от старших бит к младшим. Триггер - посылка, по приходу которой фиксируется значение (один на параметр). Если триггер не задан - используется источник последнего куска."))

    lines.append(h3("3.5. Диалог «Параметры сохранения»"))
    lines.append(bullet("Формат выходного файла: XLSX или CSV."))
    lines.append(bullet("Режим при обработке папки: отдельный файл на каждый лог; все .trc в один файл; разбить .trc по датам из содержимого."))

    lines.append(h3("3.6. Диалог «Смена формата»"))
    lines.append(p("Укажите исходный файл (не папку), тип преобразования (TRC -> ASC) и путь результата. Прогресс - в журнале главного окна."))

    lines.append(h2("4. Структура файла параметров (.xlsx)"))
    lines.append(p("Лист Devices, одна строка - один сигнал. Основные колонки:"))
    lines.append(bullet("DeviceID - ID посылки (hex); MessageName - имя; Extended, DLC;"))
    lines.append(bullet("FieldIndex, Header - порядок и имя параметра в результате;"))
    lines.append(bullet("Type - NUM или BIN; StartBit, Length - для NUM; ByteOrder, Signed;"))
    lines.append(bullet("Scale, Offset, Unit, Min, Max; BitStart - для BIN."))
    lines.append(p())
    lines.append(bi("Формула расчёта (NUM):", "Value = (HighByte*256 + LowByte) * Scale + Offset"))
    lines.append(p("(для произвольной длины используется StartBit/Length из файла)."))

    lines.append(h2("5. Структура файла составных параметров (.xlsx)"))
    lines.append(p("Лист Composites, одна строка - один кусок составного значения:"))
    lines.append(bullet("Block, Param, Piece (0 - старшие биты);"))
    lines.append(bullet("SourceID, Byte, BitStart, BitLen; Trigger (1 - триггерная посылка);"))
    lines.append(bullet("Scale, Offset, Signed, Unit, Min, Max."))

    lines.append(h2("6. Типовой сценарий"))
    lines.append(bullet("Обзор - выбрать файл или папку с логами."))
    lines.append(bullet("Посылки - при необходимости проверить ID в логе."))
    lines.append(bullet("Обзор - выбрать .xlsx/.dbc или Создать... и заполнить в Редакторе."))
    lines.append(bullet("Устройства и параметры - настроить фильтры и сверку с логом."))
    lines.append(bullet("При необходимости указать файл составных параметров."))
    lines.append(bullet("Указать путь сохранения и Параметры сохранения."))
    lines.append(bullet("Обработать - дождаться успешного завершения."))
    lines.append(bullet("Открыть - просмотреть результат."))

    lines.append(h2("7. Частые ошибки"))
    lines.append(bi("Файл лога не найден", "проверьте путь в поле логов."))
    lines.append(bi("Файл посылок не найден", "укажите существующий .xlsx или .dbc."))
    lines.append(bi("Файл уже открыт в другой программе", "закройте Excel или редактор."))
    lines.append(bi("Файл вывода совпадает с логом или посылками", "укажите другой путь."))
    lines.append(bi("Для обработки папки укажите каталог", "в «Сохранить в» должна быть папка, не файл."))
    lines.append(bi("В папке не найдено файлов", "нет .csv, .trc, .asc или .txt в каталоге."))
    lines.append(bi("Текстовый файл не распознан как CANfox", "проверьте формат .txt (Date, Time, ID, Data)."))

    return "".join(lines)


def build_rtf() -> str:
    body = build_body()
    return (
        r"{\rtf1\ansi\ansicpg1251\uc1\deff0\deflang1049"
        r"{\fonttbl{\f0\fswiss\fcharset204 Calibri;}}"
        r"\viewkind4\uc1\pard\f0\fs22\lang1049 "
        + body
        + "}"
    )


def main() -> None:
    out = Path(__file__).resolve().parents[1] / "logReader.UI" / "Resources" / "Help.rtf"
    out.write_text(build_rtf(), encoding="ascii", errors="strict")
    print(f"Written {out} ({out.stat().st_size} bytes)")


if __name__ == "__main__":
    main()
