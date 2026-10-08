# LOGER 2.0 — Design System (Forge)

Premium industrial desktop tool for CAN log processing.

**Stack:** WinForms + custom chrome controls + `ThemePalette` / `AntdThemeBridge` (toasts only). Domain editors stay WinForms.

**Performance:** no heavy shadows/animations; theme recolors in place; large lists stay virtualized.

---

## Concepts explored

### A — Precision Workspace
Dense Linear/Notion-like tool. Monochrome neutrals, yellow rail only, stepper workflow, high table density.  
**Pros:** focus, speed. **Cons:** less brand drama; easy to feel “another IDE skin”.

### B — Premium Industrial (selected → **Forge**)
Reference language from modern SaaS + dark analytics dashboards: icon rail, elevated surfaces, yellow identity, dual-quality Light/Dark, mission/CTA panel.  
**Pros:** distinct product identity, clear hierarchy, matches engineering brand. **Cons:** more chrome to maintain.

### C — Command Studio
Raycast-like centered command stage, minimal persistent chrome.  
**Pros:** bold. **Cons:** poor fit for multi-file path + filter workflows; weak discoverability for devices/export.

**Decision:** Concept B (**Forge**), borrowing A’s density cues and C’s strong primary action focus (mission panel).

Visual references (inspiration only, not LOGER mockups): dark glass analytics (NEXUS-like) + light yellow SaaS workspace (Cloudio-like).

---

## Atmosphere

- Light and Dark are **equal** first-class themes (not inverted clones).
- Yellow Primary is the product accent (CTA, selected nav, brand mark, selection tint) — never full-screen fills, never semantic Warning/Error.
- Surface hierarchy: Canvas → Surface → Elevated → (optional) Overlay.
- Density: Fluent / Win11 tool, not marketing Ant Design demos.
- Shape: rounded work surfaces (`Radius.Lg` / `Xl`), soft borders, no multi-layer drop shadows.

---

## Color tokens

| Role | Light | Dark |
|------|-------|------|
| Canvas | `#F3F4F6` | `#0C0D10` |
| Surface | `#FFFFFF` | `#16181D` |
| SurfaceSecondary | `#EBEDF0` | `#12141A` |
| Elevated | `#F0F1F4` | `#1E2128` |
| Text | `#111827` | `#F3F4F6` |
| TextSecondary | `#4B5563` | `#A1A8B3` |
| Muted | `#6B7280` | `#8B929E` |
| Border | `#D1D5DB` | `#2A2E36` |
| BorderHover | `#9CA3AF` | `#3D4450` |
| Primary | `#EAB308` | `#FACC15` |
| PrimaryHover | `#CA8A04` | `#EAB308` |
| PrimaryPressed | `#A16207` | `#CA8A04` |
| OnPrimary | `#111827` | `#111827` |
| Success | `#059669` | `#34D399` |
| Warning | `#C2410C` | `#FB923C` |
| Error | `#B91C1C` | `#F87171` |
| Info | `#1D4ED8` | `#60A5FA` |
| Selection | `#FEF3C7` | `#3F3A1E` |
| FocusRing | `#EAB308` | `#FACC15` |
| ConsoleBg | `#EEF0F3` | `#0A0B0E` |
| ConsoleFg | `#111827` | `#E5E7EB` |
| GridLine | `#D1D5DB` | `#2A2E36` |
| EmptyCell | `#F0F1F4` | `#12141A` |

Contrast: text/primary/error/console pairs ≥ 4.5 (`ThemeRegressionTests`).

---

## Typography

Segoe UI Variable (fallback Segoe UI); mono Consolas.

| Token | Size | Weight |
|-------|------|--------|
| Display / Brand | 20 | Bold |
| PageTitle | 16 | Semibold/Bold |
| Section | 11 | Bold |
| Body | 9.5 | Regular |
| Caption | 8.5 | Regular |
| Mono | 9 | Regular |

---

## Spacing / Radius

Spacing: 4 / 8 / 12 / 16 / 20 / 24 / 32 / 40.  
Radius: Sm 6 / Md 10 / Lg 14 / Xl 18.

---

## Layout (Main — Forge shell)

```
┌ Header: brand diamond + LOGER + page title .............. theme toggle ┐
├ Rail (72) ┬ Workspace (inputs + output) ┬ Mission panel (ready + CTA) ┤
│ icons     │ two-column surfaces          │ checklist + Обработать      │
├───────────┴──────────────────────────────┴─────────────────────────────┤
│ Activity console (collapsible height via splitter)                     │
├ Status badge ──────────────────────────────────────────────────────────┤
```

Nav rail: Обработка / Конвертация / Справка (icon + tooltip; selected = yellow pill).  
Help and Convert remain overlays (modeless / modal) but selection state is visual.  
Theme toggle in header; Light/Dark persist via `ui-theme-v2.txt`.

---

## Component rules

- Colors only via `ThemePalette` / `AppTheme` — no random hex in forms.
- Yellow = Primary accent only; Success/Warning/Error stay distinct.
- Prefer reusable controls (`NavigationItem`, `ModernCard`, `ModernButton`, mission panel).
- Keep processing handlers and file pipelines intact.
- Preserve UIA: `Name` = AutomationId, `AccessibleName` = visible label.
- Don't: animate large grids; mix extra UI kits; migrate to WPF/Avalonia without explicit ask.

---

## Do / Don't

- Do: redesign composition freely; keep algorithms and formats.
- Do: verify Light and Dark on a live window with screenshots.
- Don't: ship “same card stack, new colors” as done.
- Don't: yellow warning semantics or huge yellow panels.
