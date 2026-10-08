# LOGER UI Design System

Premium + Clean Minimal desktop tool for CAN log processing.

**Stack:** AntdUI (chrome) + ThemePalette bridge + domain custom controls (CAN grid).

**Performance first:** no heavy animations; theme recolors in place; large lists stay virtualized (ListView/DataGridView).

## Atmosphere

- Light theme by default (`ui-theme-v2.txt`); Dark is a full second theme via header toggle.
- Yellow Primary accent only (CTA, brand mark, selection).
- Surface hierarchy: Canvas → Surface → Elevated.
- Density closer to Fluent / Windows 11 than Ant Design marketing demos.

## Color tokens

| Role | Light (default) | Dark |
|------|-----------------|------|
| Canvas | `#F7F8FA` | `#0B0E11` |
| Surface | `#FFFFFF` | `#1E2329` |
| Elevated | `#EEF0F3` | `#2B3139` |
| Text | `#181A20` | `#EAECEF` |
| Muted | `#707A8A` | `#848E9C` |
| Border | `#E5E7EB` | `#2B3139` |
| Primary | `#F0B90B` | `#FCD535` |
| OnPrimary | `#181A20` | `#181A20` |
| Success / Warning / Error / Info | semantic, not yellow fill | same |

## Typography

Segoe UI Variable (fallback Segoe UI): PageTitle 18, SectionTitle 11 bold, Body 9.5, Caption 8.5, Mono Consolas 9.

## Spacing / Radius

Spacing: 4 / 8 / 12 / 16 / 20 / 24 / 32. Radius: 4 / 8 / 12.

## Layout (Main)

Header + slim nav (Обработка / Справка / Конвертация) + card-stack pipeline + activity console + status bar.

## Do / Don't

- Do: colors from ThemePalette / AntdThemeBridge
- Do: keep handlers and pipeline intact
- Don't: animate resize/scroll of large grids
- Don't: mix multiple UI kits
