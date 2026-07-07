# Components — design system notes

Plexor UI использует **shadcn/ui** как реализационную базу (см.
[shadcn-mapping.md](shadcn-mapping.md)). OpenDesign компоненты
должны соответствовать shadcn API насколько возможно — это сэкономит
время разработчикам и обеспечит consistency.

## Когда использовать shadcn/ui as-is

Базовые компоненты (Button, Input, Card, Dialog, Table, etc.) —
берём as-is из shadcn. Дизайном не переделываем.

## Когда делать custom

Plexor-specific компоненты:
- DataTable (с фильтрами, bulk actions, sorting)
- StatusBadge (с condition rendering)
- ConsolePanel (xterm.js wrapper)
- ResourceCard (resource representation)
- WizardFrame (multi-step)
- Chart wrappers (если используем Recharts)

## Component inventory

| Need | Use |
|------|-----|
| Button | shadcn Button |
| Input | shadcn Input |
| Form | shadcn Form + react-hook-form + Zod |
| Dialog | shadcn Dialog |
| Drawer | shadcn Drawer (Vaul) |
| Toast | shadcn Sonner |
| Card | shadcn Card |
| Table | shadcn Table + TanStack Table |
| Tabs | shadcn Tabs |
| Select | shadcn Select |
| Multi-select | shadcn (built-in в новых версиях) |
| Combobox | shadcn Command + Popover |
| Date picker | shadcn Calendar + Popover |
| Tooltip | shadcn Tooltip |
| Dropdown menu | shadcn DropdownMenu |
| Accordion | shadcn Accordion |
| Avatar | shadcn Avatar |
| Badge | shadcn Badge |
| Progress | shadcn Progress |
| Skeleton | shadcn Skeleton |
| Switch | shadcn Switch |
| Checkbox | shadcn Checkbox |
| RadioGroup | shadcn RadioGroup |
| Slider | shadcn Slider |
| Breadcrumb | shadcn Breadcrumb |
| Pagination | shadcn Pagination + TanStack Pagination |
| Charts | Recharts |
| Icons | Lucide React |

См. [shadcn-mapping.md](shadcn-mapping.md) для детальной карты OpenDesign → shadcn.