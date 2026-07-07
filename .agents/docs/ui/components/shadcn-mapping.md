# Shadcn/OpenDesign → shadcn/ui mapping

Это карта между OpenDesign-компонентами (дизайнер нарисует) и shadcn/ui
реализациями. Когда рисуете компонент в OpenDesign — смотрите эту
таблицу и старайтесь максимально придерживаться shadcn API.

## Form elements

| OpenDesign | shadcn/ui | Notes |
|------------|-----------|-------|
| Text Input | `<Input>` | базовый |
| Textarea | `<Textarea>` | - |
| Select dropdown | `<Select>` | - |
| Multi-select | `<MultiSelect>` (custom on shadcn Command) | - |
| Combobox | `<Command>` + `<Popover>` | cmdk-based |
| RadioGroup | `<RadioGroup>` | - |
| Checkbox | `<Checkbox>` | - |
| Switch | `<Switch>` | - |
| Slider | `<Slider>` | - |
| Date picker | `<Calendar>` + `<Popover>` | - |
| Color picker | custom (Phase 3) | - |

## Layout

| OpenDesign | shadcn/ui | Notes |
|------------|-----------|-------|
| Card | `<Card>` + `<CardHeader>` + `<CardContent>` + `<CardFooter>` | - |
| Tabs | `<Tabs>` + `<TabsList>` + `<TabsTrigger>` + `<TabsContent>` | - |
| Accordion | `<Accordion>` | - |
| Separator | `<Separator>` | - |
| Aspect ratio | `<AspectRatio>` | - |
| Scroll area | `<ScrollArea>` | - |
| Resizable panels | custom (react-resizable-panels) | - |
| Sheet (side panel) | `<Sheet>` | - |

## Feedback

| OpenDesign | shadcn/ui | Notes |
|------------|-----------|-------|
| Alert | `<Alert>` | - |
| Toast | `<Sonner>` | - |
| Dialog | `<Dialog>` | - |
| Drawer | `<Drawer>` (Vaul) | - |
| Popover | `<Popover>` | - |
| Tooltip | `<Tooltip>` | - |
| Hover card | `<HoverCard>` | - |
| Progress | `<Progress>` | - |
| Skeleton | `<Skeleton>` | - |

## Navigation

| OpenDesign | shadcn/ui | Notes |
|------------|-----------|-------|
| Breadcrumb | `<Breadcrumb>` | - |
| Pagination | `<Pagination>` + TanStack | - |
| Menubar | `<Menubar>` | - |
| Navigation Menu | `<NavigationMenu>` | - |
| Sidebar | custom (`web/apps/console/src/app/shell/app-shell.tsx`) | - |
| Breadcrumb | `<Breadcrumb>` | - |
| Tabs (router) | TanStack Router | - |

## Data display

| OpenDesign | shadcn/ui | Notes |
|------------|-----------|-------|
| Table | shadcn `<Table>` + TanStack `<DataTable>` | Wrapper |
| DataTable (filterable) | custom (TanStack Table + filters) | Plexor-specific |
| StatusBadge | custom (`shared/ui/status-badge.tsx`) | Plexor-specific |
| Avatar | `<Avatar>` | - |
| Badge | `<Badge>` | - |
| ResourceCard | custom (`shared/ui/resource-card.tsx`) | Plexor-specific |
| ConsolePanel | xterm.js wrapper (custom) | Plexor-specific |
| Charts | Recharts | - |
| Code (syntax highlighted) | Shiki | - |
| Diff | custom (`shared/ui/diff-viewer.tsx`) | Plexor-specific |

## Date/time

| OpenDesign | shadcn/ui | Notes |
|------------|-----------|-------|
| Date picker | `<Calendar>` + `<Popover>` | - |
| Date range picker | custom (Phase 2 — for billing, audit) | - |
| Time | native `<input type="time">` | - |
| RelativeTime | custom (`shared/ui/relative-time.tsx`) | "3d ago" |

## Plexor-specific (нет в shadcn)

Создаём в `web/apps/console/src/shared/ui/`:

- `<DataTable>` — таблица с фильтрами, bulk, sort, columns visibility
- `<StatusBadge>` — VM/Volume/etc. status с color-coding
- `<ResourceCard>` — карточка ресурса в lists
- `<ConsolePanel>` — xterm.js wrapper
- `<EmptyState>` — стандартизированный empty state
- `<ErrorState>` — стандартизированный error state
- `<ConfirmDialog>` — destructive actions confirm
- `<RelativeTime>` — relative date rendering
- `<CopyableText>` — click-to-copy для IPs, IDs
- `<FilterChips>` — multi-select chips с удалением
- `<ResourceBreadcrumb>` — VM/VPC/etc. path breadcrumbs

См. [README.md](README.md) для общего подхода.

## Кастомизация shadcn

Когда shadcn не подходит — компонент **форкаем** в `shared/ui/` и
дорабатываем. Не правим `components/ui/` напрямую (это upstream shadcn).

Стандартный workflow:

```bash
# Добавить компонент из shadcn
pnpm dlx shadcn@latest add button

# → кладётся в apps/console/src/components/ui/button.tsx

# Если нужна модификация → копируем в shared/ui/
cp apps/console/src/components/ui/button.tsx \
   apps/console/src/shared/ui/primary-button.tsx
# ... и дорабатываем
```

## Иконки

**Library**: Lucide (https://lucide.dev).

Соответствие иконок:

| Концепт | Иконка |
|---------|--------|
| VM | `<Server />` |
| Volume | `<HardDrive />` |
| Bucket | `<Package />` |
| VPC | `<Network />` |
| Subnet | `<NetworkIcon />` |
| SG | `<Shield />` |
| LB | `<Equal />` |
| Floating IP | `<Globe />` |
| DNS | `<Globe2 />` |
| SSH key | `<KeyRound />` |
| User | `<User />` |
| Role | `<ShieldCheck />` |
| Billing | `<Receipt />` |
| Audit | `<ScrollText />` |
| Logs | `<FileText />` |
| Metrics | `<LineChart />` |
| Settings | `<Settings />` |
| Brand | `<Hexagon />` (custom) |

Кастомный логотип Plexor — отдельный SVG, экспортируется из OpenDesign.