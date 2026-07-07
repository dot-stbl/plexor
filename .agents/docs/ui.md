# UI

> Каноничные UI-документы переехали в [`.agents/docs/ui/`](ui/). Этот файл
> оставлен как redirect для обратной совместимости.

## Где что лежит

| Раздел | Ссылка |
|--------|--------|
| Designer entrypoint | [ui/README.md](ui/README.md) |
| Brand | [ui/brand.md](ui/brand.md) |
| Personas | [ui/personas.md](ui/personas.md) |
| Information architecture | [ui/information-architecture.md](ui/information-architecture.md) |
| User flows | [ui/user-flows.md](ui/user-flows.md) |
| Screen briefs | [ui/screens/](ui/screens/) |
| Components | [ui/components/](ui/components/) |

## Краткое summary

Plexor Portal:

- **Stack**: Vite + React + shadcn/ui + TanStack Router + TanStack Query + Zustand
- **Design system**: Plexor brand (purple `#5E5BE8` accent), Inter typography, 4px grid
- **Темы**: light + dark (обязательно)
- **Responsive**: desktop-first, mobile = read-only
- **Personas**: Dmitriy (DevOps), Maria (junior dev), Andrey (manager), Vasya (OSS)
- **Critical screens (MVP)**: VM list, VM detail, Create VM wizard, Network/VPC, Billing, Audit log

Полная документация — в каталоге `ui/`.
