# UI Component Index — the decision table

One UI need → exactly ONE component. If two rows seem to fit, the "Never"
column tells you which sibling is the trap. Humans and agents: **exhaust this
index before creating any new primitive.**

- Primitives live in `src/shared/ui/primitives/` (react-aria-components + cva + cn).
- Composite shells live in `src/shared/ui/app-shell/` and `src/shared/ui/data-table/`.
- Full consumer audit (2026-09-20) is in the [Appendix](#appendix-audit-2026-09-20).

## Inputs

| You need | Use | Never | Notes |
|---|---|---|---|
| A button | `button` → `Button` | `toggle`, `button-group` (deleted) | Variants via cva. Only button primitive. |
| Single-line text | `input` → `Input` | `password-input` (for plain text) | |
| Secret / password text | `password-input` → `PasswordInput` | `input` + manual eye icon | Wraps `Input`, adds show/hide toggle. |
| Multi-line text | `textarea` → `Textarea` | `input` | |
| A number + unit of size (GB/MB…) | `size-field` → `SizeField` | `input` + manual unit | Pairs with `size` for display. |
| A labeled form field (label + control + error) | `field` → `Field` | hand-rolled label + input | Used inside dialogs and forms. |
| A read-only labeled row (label: value) | `field-row` → `FieldRow` | `marker` | Field-row = key/value display; marker = group caption. |
| A caption / group marker inside a form section | `marker` → `Marker` | `field-row`, `label` | Part of the field-help pattern contract. Zero consumers today — kept by decision, use it for section captions. |
| A plain label | `label` → `Label` | `marker` | `marker` is not a form label. |
| Inline help icon with tooltip | `help-tooltip` → `HelpTooltip` | `tooltip` + hand-rolled icon | HelpTooltip = icon + tooltip combo over `tooltip`. |
| A checkbox | `checkbox` → `Checkbox` | `switch` | Checkbox = multi-choice / opt-in. Switch = live setting. |
| A toggle for a setting | `switch` → `Switch` | `checkbox` | See checkbox row. |
| A numeric range (min–max) | `slider` → `Slider` | two `input`s | |

## Selection

| You need | Use | Never | Notes |
|---|---|---|---|
| A dropdown from a list of strings | `simple-select` → `SimpleSelect` | `select` | Thin wrapper; kills Trigger/Content/Item boilerplate. |
| A dropdown with rich items (icons, badges, sections) | `select` → `Select` family | `simple-select` | `simple-select` only renders strings. |
| Radio buttons | `radio-group` → `RadioGroup` family | `select`, `segmented-control` | |
| Segmented toggle between 2–4 modes | `segmented-control` → `SegmentedControl` | `radio-group`, `tabs` | Compact mode switcher, not navigation. |
| Pick one card from a grid of cards | `selectable-card-grid` → `SelectableCardGrid` | `radio-group` + custom cards | Built on RadioGroup. |

## Overlays

| You need | Use | Never | Notes |
|---|---|---|---|
| A modal dialog | `dialog` → `Dialog` family | `alert-dialog`, `sheet` | |
| A confirm / destructive action modal | `alert-dialog` → `AlertDialog` family | `dialog` + manual buttons | AlertDialog = semantic confirm (cancel/accept). |
| A side panel sliding from the edge | `sheet` → `Sheet` family | `dialog` | Used by `sidebar` for mobile rail. |
| A floating anchored panel (picker, custom popup) | `popover` → `Popover` family | `dialog`, `tooltip` | |
| Hover hint | `tooltip` → `Tooltip` family | `help-tooltip` (that's for field help), `popover` | |
| App-wide notifications / toasts | `sonner` (Toaster in `main.tsx`) + `toast` | `alert` | Toasts = transient; alert = persistent inline. |
| The user settings dialog | `preferences-dialog` → `PreferencesDialog` | `dialog` + custom body | Single instance, settings page contract. |

## Layout

| You need | Use | Never | Notes |
|---|---|---|---|
| A card / panel container | `card` → `Card` family | raw `<div>` + border classes | |
| A visual divider | `separator` → `Separator` | `<hr>` | |
| Scrollable region with styled scrollbar | `scroll-area` → `ScrollArea` | `overflow-auto` div | |
| Expand/collapse "Advanced" section in a form | `disclosure` → `Disclosure` | `collapsible` (raw), building an accordion | Disclosure = opinionated Collapsible (inline/card variants). |
| Raw collapsible primitive | `collapsible` → `Collapsible` family | — | Only inside other primitives (disclosure uses it). Prefer `Disclosure`. |
| Repeatable key/value row editor (+ / − rows) | `repeatable-rows` → `RepeatableRows` | hand-rolled map + add/remove | |
| Summary sidebar of a create-wizard form | `summary-panel` → `SummaryPanel` | `card` + manual rows | |

## Feedback

| You need | Use | Never | Notes |
|---|---|---|---|
| Inline status message (persistent) | `alert` → `Alert` family | `sonner` toast, `status-pill` | |
| Loading spinner | `spinner` → `Spinner` | `progress` | Indeterminate wait. |
| Determinate progress (%, steps) | `progress` → `Progress` | `spinner` | |
| Loading placeholder block | `skeleton` → `Skeleton` | `spinner` (for content layout) | |
| Empty list / no data state | `empty-state` → `EmptyState` | `status-pill`, blank page | Standard empty state with icon + action. |

## Data display

| You need | Use | Never | Notes |
|---|---|---|---|
| A data table / list view with sorting, selection | `shared/ui/data-table` (`DataTable`) | `table` + manual wiring | DataTable composes `table`, `checkbox`, `popover`, `select`. |
| A static semantic table (non-interactive) | `table` → `Table` family | `data-table` (overkill) | |
| Monospaced tabular number | `mono-num` → `MonoNum` | raw `font-mono` span | Durations, timestamps, IDs. |
| Human-readable byte size | `size` → `Size` | `mono-num` + manual format | |
| Click-to-copy value | `copyable-text` → `CopyableText` | `mono-num` + manual button | Uses `sonner` toast on copy. |
| Status / state chip (running, stopped…) | `status-pill` → `StatusPill` | `badge` | StatusPill = semantic state; badge = neutral label. |
| Neutral label / tag / count | `badge` → `Badge` | `status-pill` | See above. |
| A single metric (label + value + trend) | `stat` → `Stat` | `card` + manual layout | Dashboards, KPIs. |
| A chart (bar / donut / line) | `chart` → chart helpers | raw recharts imports | Themed wrappers over recharts. |
| A technology / OS / runtime icon | `tech-icon` → `TechIcon` | inline `<img>` | Data in `tech-icon-data.ts`. |
| A user avatar | `avatar` → `Avatar` family | `img` rounded | |

## Navigation

| You need | Use | Never | Notes |
|---|---|---|---|
| Tab navigation within a page | `tabs` → `Tabs` family | `segmented-control` | Tabs = navigation; segmented = mode switch. |
| Breadcrumb trail | `breadcrumb` → `Breadcrumb` family | manual links | Used by app header. |
| Multi-step wizard steps | `stepper` → `Stepper` | `progress` | Stepper = discrete steps with labels. |
| App sidebar / rail navigation | `shared/ui/app-shell` (`AppSidebar`) | `sidebar` primitives directly | `sidebar` primitives are the building blocks; use the shell. |

## App shell

| You need | Use | Never | Notes |
|---|---|---|---|
| The page frame (header + sidebar + content) | `app-shell` → `AppShell` | manual layout | |
| Standard page scaffold (title, actions, content) | `app-shell` → `PageTemplate` | manual header divs | |
| Org/team/folder scope switcher | `app-shell` → `ScopeSwitcher` | custom popover | |
| Global "+" create menu | `app-shell` → `GlobalCreateMenu` | custom dropdown | |
| App launcher grid | `app-shell` → `AppLauncher` | custom dialog | |
| A placeholder route page | `app-shell` → `PlaceholderPage` | empty div | |
| Bulk actions bar over a selected table rows | `bulk-action-toolbar` → `BulkActionToolbar` | manual toolbar | Composed with DataTable row selection. |
| Row actions menu (⋯ on a table row) | `dropdown-menu` → `DropdownMenu` family | `popover` + manual list | |
| Plexor logo mark | `app-shell` → `PlexorMark` | inline svg | |

## How to add a new primitive

1. **Exhaust this INDEX first.** If a row almost fits, extend that component
   (a variant, a prop) instead of adding a file. Zero-consumer primitives get
   deleted at the next audit — new files must earn consumers.
2. Check the deleted list in the appendix — if your need matches something we
   deleted (drawer, calendar, pagination…), restore it from git history rather
   than rewriting it.
3. Follow the `button.tsx` pattern:
   - react-aria-components primitives, **no** @base-ui/@shadcn imports;
   - `cva` for variants, `cn()` (from `@/lib/utils`) for merges;
   - named exports only (no default exports);
   - co-located `<name>.test.tsx` (vitest + testing-library);
   - a `<name>.stories.tsx` for the Storybook catalog when the component is
     part of the core showcase.
4. One component (family) per file. File name = export name.
5. Add the row to this INDEX — a primitive missing from the INDEX does not
   exist.

## Appendix: audit 2026-09-20

Consumer = an import in `src/features/**`, `src/routes/**`, or `src/shared/ui/**`
outside the component's own file/test/story; imports from other primitives
count. Audit run before deletion; counts for kept components reflect the tree
**after** the wave-1 deletions.

### Deleted (31) — zero consumers

| Component | Consumers | Disposition |
|---|---|---|
| accordion | 0 | deleted — disclosure/collapsible cover our needs |
| aspect-ratio | 0 | deleted |
| attachment | 0 | deleted |
| bubble | 0 | deleted — chat family unused |
| button-group | 0 | deleted |
| calendar | 0 | deleted |
| carousel | 0 | deleted |
| combobox | 0 | deleted |
| command | 0 | deleted — command palette unused |
| console | 0 | deleted — APM console unused |
| context-menu | 0 | deleted |
| direction | 0 | deleted |
| drawer | 0 | deleted — sheet covers side panels |
| empty | 0 | deleted — duplicate of empty-state |
| filter-sidebar | 0 | deleted |
| hover-card | 0 | deleted |
| input-group | 0* | deleted — only consumers were combobox + command (also deleted) |
| input-otp | 0 | deleted |
| ip | 0 | deleted |
| item | 0 | deleted |
| kbd | 0 | deleted — only reference was a tooltip test fixture |
| menubar | 0 | deleted |
| message | 0 | deleted — chat family unused |
| message-scroller | 0 | deleted — chat family unused |
| native-select | 0 | deleted — duplicate of select |
| navigation-menu | 0 | deleted |
| pagination | 0 | deleted — tables paginate via data-table |
| resizable | 0 | deleted |
| toggle | 0* | deleted — only consumer was toggle-group (also deleted) |
| toggle-group | 0 | deleted |
| toolbar | 0 | deleted — bulk-action-toolbar covers the real case |

### Kept (54)

| Component | Consumers | Notes |
|---|---|---|
| button | 42 | |
| mono-num | 19 | |
| status-pill | 17 | |
| card | 14 | |
| input | 13 | |
| empty-state | 11 | |
| badge | 10 | |
| size | 9 | |
| tech-icon | 5 | |
| select | 5 | rich dropdowns |
| sidebar | 5 | building block for app-shell |
| stepper | 5 | |
| label | 5 | |
| switch | 4 | |
| summary-panel | 4 | |
| size-field | 4 | |
| segmented-control | 4 | |
| repeatable-rows | 4 | |
| polymorphic | 4 | internal: badge/breadcrumb/marker/sidebar |
| password-input | 4 | |
| field-row | 4 | |
| dropdown-menu | 4 | |
| copyable-text | 4 | |
| bulk-action-toolbar | 4 | |
| tooltip | 3 | tooltip provider in main.tsx |
| simple-select | 3 | string-options wrapper over select |
| separator | 3 | |
| disclosure | 3 | |
| dialog | 3 | |
| table | 2 | |
| stat | 2 | |
| sonner | 2 | toaster in main.tsx |
| skeleton | 2 | |
| slider | 2 | |
| scroll-area | 2 | |
| radio-group | 2 | |
| progress | 2 | |
| popover | 2 | |
| help-tooltip | 2 | |
| field | 2 | |
| checkbox | 2 | |
| chart | 2 | |
| avatar | 2 | |
| alert-dialog | 2 | |
| alert | 2 | |
| textarea | 1 | |
| tabs | 1 | |
| spinner | 1 | |
| sheet | 1 | internal: sidebar mobile rail |
| selectable-card-grid | 1 | |
| preferences-dialog | 1 | |
| collapsible | 1 | internal: disclosure builds on it |
| breadcrumb | 1 | app-header |
| marker | 0 | kept by decision — field-help pattern contract |

### Merge candidates evaluated

| Pair | Decision |
|---|---|
| `select` vs `simple-select` vs `native-select` | `native-select` deleted (0 consumers). Kept both others: `simple-select` is a documented thin wrapper for string options, not a duplicate; deleting it would re-add Trigger/Content/Item boilerplate to 3 create-wizard forms. |
| `empty` vs `empty-state` | `empty` deleted (0 consumers). `empty-state` is the one true empty state. |
| `console` vs `message` vs `bubble` | All three zero-consumer (unused APM/chat family). All deleted. |
