# Что сделать руками (FlaUI-MCP)

Инфраструктура агентов/UiRunner уже в репозитории. Без MCP «глаза» аудитора не работают.

## 1. Скачать FlaUI-MCP

Релизы: https://github.com/shanselman/FlaUI-MCP/releases

Для обычного PC: `FlaUI-MCP-win-x64-*-self-contained.zip`

## 2. Распаковать

Рекомендуемый путь в этом репо (уже в `.cursor/mcp.json`):

`tools/FlaUI-MCP/FlaUI.Mcp.exe`  
полный: `C:\Users\Re\source\repos\RebaLOM\logReader\tools\FlaUI-MCP\FlaUI.Mcp.exe`

Папка `tools/FlaUI-MCP/` в `.gitignore` (бинарник ~160MB).

Если путь другой — поправь `command` в `.cursor/mcp.json`.

## 3. Включить MCP в Cursor

1. Cursor → Settings → Tools & MCP  
2. Убедись, что сервер `windows` зелёный / connected  
3. При запросе разрешений на управление рабочим столом — подтверди (это ожидаемо)

Если сервер не поднялся — перезапусти Cursor после появления exe по пути.

## 4. Smoke-проверка

В Agent Mode:

> Используй MCP `windows`. Запусти Блокнот, сделай snapshot и screenshot, опиши найденные элементы. Файлы пользователя не меняй.

Затем (после сборки LOGER):

> Запусти LOGER.exe через windows MCP, snapshot главного окна, screenshot.

## Безопасность

FlaUI-MCP управляет UI Windows. Используй тестовые данные в `ui-audit/test-data/`, не боевые логи с секретами.
