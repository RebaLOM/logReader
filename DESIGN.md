# LOGER UI Design System

Технический desktop-инструмент для разбора CAN-логов. Плотная, сканируемая поверхность; не маркетинговый лендинг.

**Референсы:** Binance (тёмный canvas + жёлтый CTA `#FCD535`) и ClickHouse (чёрный + electric yellow как редкий акцент). Адаптация под WinForms.

## Atmosphere

- Тёмная тема по умолчанию.
- Жёлтый — только CTA, brand mark, активный toggle. Не заливать экран.
- Success / Warning / Error отдельно от Primary.
- Иерархия поверхностей: Canvas → Surface → Elevated.

## Color tokens

| Role | Dark | Light |
|------|------|-------|
| Canvas | `#0B0E11` | `#F7F8FA` |
| Surface | `#1E2329` | `#FFFFFF` |
| Elevated | `#2B3139` | `#EEF0F3` |
| Text | `#EAECEF` | `#181A20` |
| Muted | `#848E9C` | `#707A8A` |
| Border | `#2B3139` | `#EAECEF` |
| Primary | `#FCD535` | `#F0B90B` |
| PrimaryHover | `#F0B90B` | `#D9A60A` |
| OnPrimary | `#181A20` | `#181A20` |
| Success | `#0ECB81` | `#0ECB81` |
| Warning | `#F0B90B` | `#D97706` |
| Error | `#F6465D` | `#DC2626` |
| Info | `#3B82F6` | `#2563EB` |
| Selection | `#3A3A1F` | `#FFF6CC` |
| ConsoleBg | `#0B0E11` | `#1E2329` |
| ConsoleFg | `#EAECEF` | `#EAECEF` |

Контраст текста к фону ≥ 4.5 (см. `ThemeRegressionTests`).

## Typography (WinForms)

- UI: Segoe UI
- Title: 16pt Semibold
- Body: 9–10pt Regular
- Caption / muted: 8.5–9pt
- Log console: Consolas 9pt

## Layout

1. Header — brand «LOGER» + жёлтый маркер + переключатель темы
2. Секции-поверхности: Логи / Устройства / Составные / Выход
3. Action bar — primary «Обработать», secondary-ряд, progress
4. Log console внизу (SplitContainer)

Spacing: 8 / 12 / 16 / 24 px. Hit area кнопок ≥ 28–32 px высоты.

## Components

- **Primary button** — жёлтый фон, OnPrimary текст, FlatStyle
- **Secondary** — Surface/Elevated + Border, Text
- **Ghost** — прозрачный фон, Muted/Text
- **Inputs** — Surface фон, Border, Text; focus через системный caret
- **DataGridView / TreeView** — цвета из палитры; сигнальная 16-цветная палитра CAN-сетки сохраняется

## Do / Don't

- Do: цвета только из `ThemePalette` / `AppTheme`
- Do: применять `AppTheme.Apply` ко всем формам
- Don't: хардкод старого синего primary
- Don't: web-only паттерны без адаптации
- Don't: менять стек на WPF/Avalonia без явного запроса
