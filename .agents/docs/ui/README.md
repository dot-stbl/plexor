# Plexor UI — Designer Onboarding

> Этот каталог — всё что нужно дизайнеру (OpenDesign) для работы над Plexor Portal.
> Не нужно читать весь код. Достаточно пройти эти документы в порядке.

## Где что лежит

| Документ | О чём | Прочитай |
|----------|-------|----------|
| [brand.md](brand.md) | Plexor identity: logo, colors, typography, voice | первым |
| [personas.md](personas.md) | 4 типа пользователей и их цели | вторым |
| [information-architecture.md](information-architecture.md) | sitemap, navigation, routing | третьим |
| [user-flows.md](user-flows.md) | 5 критичных путей | четвёртым |
| [screens/](screens/) | брифы для каждого экрана | при работе над конкретным экраном |
| [components/](components/) | component library | при работе над компонентами |

## Как использовать этот каталог

1. **Начинай с брифа экрана** (`screens/0X-*.md`). Там:
   - purpose экрана
   - user goal
   - entry / exit points
   - layout structure
   - content elements
   - states (empty/loading/error)
   - open design decisions для тебя

2. **Если нужно понять контекст** — открой [information-architecture.md](information-architecture.md) или [user-flows.md](user-flows.md).

3. **Если нужны constraints** (цвета, размеры, типографика) — [brand.md](brand.md).

## Что вне scope дизайна

- Реализация компонентов в коде — это для разработчиков (используют
  shadcn/ui как реализационную базу, см. [components/shadcn-mapping.md](components/shadcn-mapping.md))
- API-контракты — описаны в [../api-contracts.md](../api-contracts.md),
  но дизайнеру обычно достаточно user-flows.md

## Готовые промты для OpenDesign

В каждом экране есть секция **OpenDesign prompt** — это готовая инструкция
для дизайн-тула, чтобы загрузить её и получить starting point экрана.

## Reviews

- Reviews дизайна: 1 раз в 2 недели по понедельникам
- Все экраны проверяются на:
  - Mobile (320–768px), tablet (768–1024px), desktop (1024+)
  - Тёмная и светлая темы
  - Loading / empty / error states
  - Keyboard navigation
  - ARIA / screen reader (базовая проверка)

## Зависимости от кода

Когда дизайн готов — фронт реализует через:
- `web/apps/console/src/design/` — Plexor-specific компоненты
- `web/apps/console/src/shared/ui/` — shadcn/ui + Plexor variants
- `web/apps/console/src/domains/*/ui/` — feature-специфичные компоненты

Дизайнер НЕ должен проектировать компоненты которые уже есть в shadcn/ui
(Button, Dialog, Table, и т.д.) — их можно переиспользовать как есть.
См. [components/shadcn-mapping.md](components/shadcn-mapping.md).

## Feedback

Если нужна дополнительная информация для дизайна — спроси в #design
канал или оставь issue в `.planning/design-questions.md`.