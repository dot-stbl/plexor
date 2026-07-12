# Plan: screen-01-vm-list

> Screen 01 из `.agents/docs/ui/screens/01-vm-list.md` — главный экран
> Compute. Первая фича с реальным data flow поверх MSW-моков (kubb +
> faker, 8 VM, seed=1337). Заменяет PlaceholderPage на `/vms`.

## Goal

Полноценный экран списка VM: `useListVms` → Table с 8 колонками,
StatusPill, IP (MonoNum), фильтры (status/zone/search), чекбокс-селект +
bulk-toolbar (start/stop/reboot), row-actions dropdown, dialog-заглушка
«Создать ВМ». Все states: loading (skeleton), error (banner+retry),
empty (Empty primitive). Sub-route `/vms/:id` и wizard-флоу — **не** в
этом плане, отдельные экраны.

## Scope (что делаем)

| ✅ В scope | ❌ Вне scope (отдельные планы) |
|---|---|
| Заменить `PlaceholderPage` на реальный layout | `/vms/$id` — VM detail |
| `useListVms` hook + loading/error/empty | `03-create-vm-wizard` — multi-step |
| Table (8 columns, compact rows) | Compact density toggle / saved filters |
| StatusPill в колонке status | Cell actions на IP (copy-toast) |
| IP (MonoNum) в колонках IP | Колоночные настройки (visibility) |
| Checkbox col + select-all | Real-time chart метрик |
| DropdownMenu row actions (start/stop/reboot/delete/edit) | |
| BulkActionToolbar (start/stop/reboot/delete) | |
| Filter bar: status, zone, search (debounced 200ms) | |
| AlertDialog confirm для destructive | |
| **Dialog-заглушка** «+ Create VM» (2-3 поля, disabled submit) | |
| Skeleton/Error/Empty states | |
| Storybook `/components` не трогаем (отдельная задача) | |

## Files

| File | Change |
|---|---|
| `web/apps/console/src/routes/vms.tsx` | **rewrite** — реальный layout, `useListVms`, local state |
| `web/apps/console/src/shared/api/mocks/handlers.ts` | **tweak** — детерминированный seed (уже есть), добавить 2 статуса `provisioning`/`stopped` явнее + ensure `running` majority |
| `web/apps/console/src/shared/api/mocks/handlers.ts` | опционально — `provideVmHandler` (mutation) → возвращает `running` через 600ms (имитация) |
| `web/apps/console/src/features/vms/index.ts` | **new** — barrel для local VM helpers (status→StatusVariant mapping, filter reducer) |
| `web/apps/console/src/features/vms/vm-status.ts` | **new** — `mapVmStatusToVariant(VmStatus): StatusVariant` (pure) |
| `web/apps/console/src/features/vms/filter-vms.ts` | **new** — `filterVms(items, { status, zone, search }): Vm[]` (pure) |
| `web/apps/console/src/features/vms/vm-row-actions.tsx` | **new** — `<VmRowActions vm={...} />` (DropdownMenu) |
| `web/apps/console/src/features/vms/vm-table.tsx` | **new** — `<VmTable items, selectedIds, onToggle, onToggleAll, onRowClick />` (Table composition) |
| `web/apps/console/src/features/vms/vm-bulk-toolbar.tsx` | **new** — wraps `<BulkActionToolbar>` (start/stop/reboot/delete) |
| `web/apps/console/src/features/vms/vm-filters.tsx` | **new** — `<VmFilters value, onChange />` (status, zone, search) |
| `web/apps/console/src/features/vms/create-vm-dialog.tsx` | **new** — Dialog stub (Name, Zone, MachineType, disabled Create) |
| `web/apps/console/src/features/vms/vm-states.tsx` | **new** — `<VmSkeleton />`, `<VmErrorBanner error, onRetry />`, `<VmEmptyState />` |

> **Новый layer `features/vms/`.** В MVP это per-screen feature folder, не
> переиспользуемая. Когда появится 2+ экрана, использующих VM (detail,
> wizard, snapshot), выделим общие bits в `features/vms/_shared/`. Сейчас —
> плоско, без premature abstraction.

## Conventions (binding)

Из `.agents/rules/web-frontend.md`:
- `shadcn-ui` единственный источник компонентов, custom в `primitives/`
- Tailwind v4 utilities, semantic tokens, Plexor DS (`bg-card`,
  `text-muted-foreground`)
- Phosphor icons без `Icon` суффикса
- Buttons: ONE `default` per view, остальные `outline`/`ghost`/`destructive`
- Icon-only → `size="icon-sm"` + `aria-label`
- Form layout → `FieldGroup` + `Field` + `FieldLabel` (если есть формы)
- `cn()` для conditional, `flex` + `gap-*` (не `space-y-*`)
- `truncate` не `overflow-hidden text-ellipsis whitespace-nowrap`
- `var(--…)` для цветов, не raw hex

## Data flow

```
MSW handler (seed=1337, 8 VM)
  ↓ http GET /vms
kubb `useListVms` (TanStack Query, staleTime=30s)
  ↓ { data, isPending, isError, refetch }
VmsPage (local state: status, zone, search, selectedIds)
  ↓
  ├─ <VmFilters value onChange />        (controlled)
  ├─ <VmTable items onToggle onToggleAll onRowClick />
  │    └─ <StatusPill variant=mapVmStatusToVariant(vm.status)>
  │    └─ <IP value={vm.internalIp} />
  │    └─ <VmRowActions vm /> (DropdownMenu)
  ├─ <VmBulkToolbar count onClear actions />   (count>0)
  └─ <CreateVmDialog open onOpenChange />     (via PageHeader action)
```

## Pure helpers (выносятся в `features/vms/` для тестируемости)

### `vm-status.ts`

```ts
import type { VmStatus } from '@/shared/api';
import type { StatusVariant } from '@/shared/ui/primitives/status-pill';

export function mapVmStatusToVariant(status: VmStatus): StatusVariant {
  switch (status) {
    case 'running':      return 'running';
    case 'stopped':      return 'stopped';
    case 'error':        return 'err';
    case 'provisioning': return 'pending';
    case 'idle':         return 'idle';
  }
}
```

### `filter-vms.ts`

```ts
import type { Vm } from '@/shared/api';

export interface VmFilters {
  status: 'all' | Vm['status'];
  zone: 'all' | string;
  search: string;
}

export function filterVms(items: readonly Vm[], filters: VmFilters): Vm[] {
  const q = filters.search.trim().toLowerCase();
  return items.filter((vm) => {
    if (filters.status !== 'all' && vm.status !== filters.status) return false;
    if (filters.zone !== 'all' && vm.zone !== filters.zone) return false;
    if (!q) return true;
    return (
      vm.name.toLowerCase().includes(q) ||
      vm.internalIp.includes(q) ||
      vm.id.toLowerCase().includes(q)
    );
  });
}

export function uniqueZones(items: readonly Vm[]): string[] {
  return [...new Set(items.map((v) => v.zone))].sort();
}
```

## UI structure (VmsPage)

```
<main data-od-id="vms-list">
  <PageHeader
    title="Виртуальные машины"
    description="N инстансов · M running of N total"  ← live
    actions={
      <Button onClick={() => setCreateOpen(true)}>
        <Plus /> Создать ВМ
      </Button>
    }
  />
  <div className="mx-auto w-full max-w-6xl px-6 py-6 lg:px-8">
    <VmFilters value={filters} onChange={setFilters} zones={...} />
    <VmTable items={filtered} selectedIds={...} onToggle ... />
    <VmBulkToolbar count={selectedIds.length} ... />
  </div>
  <CreateVmDialog open={createOpen} onOpenChange={setCreateOpen} />
</main>
```

### Table columns

| Col | Width | Render |
|---|---|---|
| ☐ | 40px | Checkbox (header: select-all), row: toggle |
| Name | flex | name (bold) + ID (MonoNum muted) |
| Status | 100px | `<StatusPill variant={mapVmStatusToVariant(vm.status)}>` |
| Internal IP | 130px | `<IP value={vm.internalIp} />` |
| Zone | 100px | `<MonoNum muted>{vm.zone}</MonoNum>` |
| Flavor | 110px | `{vcpu} vCPU · {ramGb} GB` (MonoNum + unit) |
| Disk | 80px | `<MonoNum>{vm.diskGb}</MonoNum> GB` |
| ⋯ | 40px | `<VmRowActions vm />` |

Row click → toast (заглушка: «Переход на /vms/:id в плане 02»).

### Filter bar

`VmFilters` — flex row, gap-2:
- Status `Select` (All / Running / Stopped / Error / Provisioning)
- Zone `Select` (All + unique zones)
- Search `Input` с leading `MagnifyingGlass`, debounced 200ms (local state)
- (без Labels-мультиселекта — нет данных; без Settings-меню — compact density вне scope)

### Bulk toolbar

`VmBulkToolbar`:
```ts
const bulkActions: BulkActionAction[] = [
  { label: 'Start',   icon: <Play />,         onClick: () => toast('Запущено: N') },
  { label: 'Stop',    icon: <Stop />,         onClick: () => toast('Остановлено: N') },
  { label: 'Reboot',  icon: <ArrowsClockwise/>, onClick: () => toast('Перезагрузка: N') },
  { label: 'Delete',  icon: <Trash />,        variant: 'destructive',
    onClick: () => setDeleteConfirmOpen(true) },
];
```
Delete открывает `AlertDialog` confirm (2 кнопки: Cancel / Удалить).

### Row actions (DropdownMenu)

```ts
const items = [
  { label: 'Запустить',  icon: <Play />,         disabled: vm.status === 'running' },
  { label: 'Остановить', icon: <Stop />,         disabled: vm.status === 'stopped' },
  { label: 'Перезагрузить', icon: <ArrowsClockwise />, disabled: vm.status !== 'running' },
  { label: 'Открыть консоль', icon: <Terminal />, disabled: true }, // MVP
  null, // separator
  { label: 'Удалить', icon: <Trash />, variant: 'destructive' },
];
```

### States

- `isPending` → `<VmSkeleton />` (5 строк `<Skeleton h-12 />`)
- `isError` → `<VmErrorBanner error onRetry={refetch} />` (Alert variant=destructive + [Retry] Button)
- `data.items.length === 0` (после filter) → `<VmEmptyState />` (Empty primitive + CTA «Создать»)
- `data.items.length === 0` (до filter, raw) → другая empty «Нет VM в проекте» vs «Ничего не найдено по фильтру»

## Steps (порядок коммитов)

1. **Скелет + pure helpers** — `features/vms/` (vm-status, filter-vms), MSW seed tweak
2. **VmsPage layout** — PageHeader + VmFilters + VmTable (read-only), wired to `useListVms`
3. **Row actions + DropdownMenu** — `VmRowActions`
4. **Bulk select + BulkActionToolbar** — checkbox col, select-all, toolbar, delete confirm
5. **States** — Skeleton, Error banner, Empty (raw + filtered)
6. **CreateVmDialog stub** — Dialog + 2 поля + disabled submit
7. **Polish** — focus rings, hover states, debounce на search, `aria-label` на icon-only, `data-od-id` для дизайнера
8. **Self-audit** — `bun run typecheck && build`; ручная проверка на моках через MSW (`VITE_USE_MOCKS=true` уже в `.env` или флаге)

## Acceptance

- [ ] `cd web && bun run typecheck` — exit 0
- [ ] `cd web && bun run build` — exit 0 (chunk-size warning ОК, bundle не растёт критически)
- [ ] `cd web && bun run lint` — exit 0 (если @typescript-eslint восстановится, иначе skip — это pre-existing)
- [ ] Visual: `/vms` рендерит 8 VM из MSW (8 строк), StatusPill цвета правильные
- [ ] Status filter «Running» → только `running` строки
- [ ] Search «prod» → debounced, отфильтровано по name/IP/id
- [ ] Select-all checkbox (header) → toggles все row-чекбоксы
- [ ] Select 2 VM → появляется BulkActionToolbar внизу
- [ ] Bulk Delete → AlertDialog confirm → toast
- [ ] Row ⋯ menu → Stop на running VM → disabled, Stop на stopped → enabled (но no-op пока)
- [ ] Loading: hard-reload с throttling → 5 skeleton строк
- [ ] Error: выключить MSW (`VITE_USE_MOCKS=false`) → Error banner + Retry кнопка
- [ ] Empty: search «__nope__» → «Ничего не найдено»
- [ ] [+ Create VM] → Dialog открыт, поля disabled, Cancel закрывает
- [ ] No `.editorconfig` правок, no severity override, no `!important`
- [ ] Все icon-only кнопки с `aria-label` (`[aria-label="Действия"]`, etc)
- [ ] Hover/focus состояния читаемы (focus ring виден, hover bg-muted)
- [ ] `data-od-id` на ключевых областях (`vms-list`, `vms-table`, `vms-filters`, `vms-bulk`, `vms-empty`)

## Out of scope (фиксируем явно)

- **VM detail** `/vms/:id` — отдельный экран, свой план
- **Create VM wizard** — multi-step, свой план
- **Tests** — пока без unit-тестов; pure helpers (`mapVmStatusToVariant`, `filterVms`) изолированы так, что их легко покрыть в следующем раунде когда появится testing-infra
- **Persistence** (saved filters, column visibility, density) — localStorage / per-user
- **Real-time updates** — WebSocket / SSE
- **Permission states** (restricted) — нужна auth-интеграция
- **IP copy-to-clipboard** — cell action, Phase 2
- **Polling / refetch on focus** — auto, TanStack Query defaults покроют

## Files НЕ трогаем

- `.agents/rules/web-frontend.md` — без правок
- `web/apps/console/src/shared/api/**` — readonly (kubb-generated; tweak только `mocks/handlers.ts`)
- `web/apps/console/src/shared/ui/primitives/**` — не модифицируем
- `web/apps/console/src/shared/ui/app-shell/**` — не модифицируем
- `web/apps/console/src/routes/__root.tsx` — без правок
- `web/apps/console/src/routes/index.tsx` (home) — без правок
- Storybook `/components` — без правок в этом плане
