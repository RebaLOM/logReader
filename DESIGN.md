# LOGER — чёрно-жёлтая desktop-система

Референсы: Miro (canary yellow как бренд-акцент) + плотность тёмных surfaces в духе VoltAgent.
Стек: WinForms. Токены живут в `logReader.UI/Theme/ThemePalette.cs` и применяются через `AppTheme`.

## Атмосфера

- Инструмент инженера для CAN-логов: строго, плотно, без «маркетинговой» воздушности.
- Жёлтый — только акцент (CTA, selected nav, focus, brand mark). Не заливать им фоны.
- Success / Warning / Error остаются отдельными семантическими цветами.
- Два режима: Light и Dark, переключатель сохраняется.

## Цвета (hex → ThemePalette)

### Dark

| Токен | Hex | Назначение |
|---|---|---|
| Background | `#0A0A0A` | Canvas окна |
| Surface | `#141414` | Sidebar, карточки, поля |
| SurfaceSecondary | `#1C1C1C` | Заголовки гридов, soft-fill |
| Border | `#2E2E2A` | Контуры карточек/кнопок |
| BorderHover | `#5A5A52` | Hover-контур |
| Primary | `#FFD60A` | CTA, nav accent, brand mark |
| PrimaryHover | `#FFE566` | Hover primary |
| PrimaryPressed | `#E6C008` | Pressed primary |
| PrimarySoft | `#2A2410` | Selected nav fill, selection |
| TextPrimary | `#F2F2F0` | Основной текст |
| TextSecondary | `#B8B8B0` | Вторичный |
| TextMuted | `#8A8A82` | Подписи, пустые состояния |
| TextOnPrimary | `#0A0A0A` | Текст на жёлтой кнопке |
| Success / Warning / Error | зелёный / янтарный / красный | Статусы (не путать с Primary) |

### Light

| Токен | Hex | Назначение |
|---|---|---|
| Background | `#F5F5F4` | Canvas (нейтральный, не крем `#F4F1EA`) |
| Surface | `#FFFFFF` | Карточки, sidebar |
| SurfaceSecondary | `#F0F0EE` | Вторичные поверхности |
| Border | `#E2E2DE` | Контуры |
| BorderHover | `#A8A8A0` | Hover-контур |
| Primary | `#8B6400` | CTA / акцент (тёмный gold — контраст Info на soft ≥ 4.5) |
| PrimaryHover | `#7A5800` | Hover |
| PrimaryPressed | `#6B4C00` | Pressed |
| PrimarySoft | `#FFF6CC` | Selected / soft wash |
| TextPrimary | `#141414` | Основной текст |
| TextSecondary | `#4A4A46` | Вторичный |
| TextMuted | `#7A7A74` | Подписи |
| TextOnPrimary | `#FFFFFF` | Текст на primary (светлая тема) |

## Типографика (WinForms)

- Семейство: Segoe UI Variable Text / Segoe UI (как в `Typography.cs`).
- Mono для payload/hex: Consolas.
- Иерархия: PageTitle → SectionTitle → CardTitle → Body → Caption.
- Не подключать web-шрифты и не имитировать CSS letter-spacing.

## Компоненты

- Primary button: заливка `Primary`, текст `TextOnPrimary`, скругление ~8–10 px.
- Secondary / Ghost: `Surface` + `Border`; hover — `SurfaceSecondary`.
- Cards: `Surface` + hairline `Border`, padding 16, radius ~12.
- Nav selected: fill `PrimarySoft`, ink `Primary`, левая полоска 3 px `Primary`.
- Brand mark: wordmark `TextPrimary` + тонкий жёлтый underline/marker — не весь title жёлтым.
- Grids: selection `PrimarySoft`, headers `SurfaceSecondary`.

## Плотность и отступы

- Sidebar ~196 px, padding 16–24.
- Workspace gutter 24.
- Высота кнопок/nav ~36–44.
- Не раздувать пустотами: это desktop tool, не landing.

## Guardrails

- Не синий primary, не purple gradients, не glow.
- Не кремовый canvas со serif display.
- Не анимации/blur/custom titlebar вне возможностей текущего WinForms-слоя.
- Любые новые цвета — только через `ThemePalette` / `AppTheme`, без хардкода hex в формах.
