# Screen 01: VM List

## Purpose

Главная страница Compute. Показывает все VMs в текущем проекте с
возможностью фильтрации, bulk actions, и быстрого доступа к действиям.

## User goal

Dmitriy: быстро посмотреть какие VMs работают, найти failing одну,
bulk-restart десяток.

Maria: проверить что её VM ещё работает, увидеть IP для подключения.

## Entry points

- `/projects/{projectId}/compute` (default после логина)
- Sidebar nav: "Compute"
- Быстрый клавиатурный shortcut: `g → c`

## Layout

```
┌────────────────────────────────────────────────────────────────────────┐
│ Top bar (Project switcher, search, notifications, user menu)            │
├──────────┬─────────────────────────────────────────────────────────────┤
│ Sidebar  │ Page content                                                 │
│          │                                                             │
│  Compute │ Page header:                                                │
│  Storage │   Title "Virtual Machines"                                  │
│  Network │   Subtitle: "{N} VMs • {M} running"                          │
│  IAM     │                                                             │
│  Observ. │ Filters bar:                                               │
│          │   [Status ▾] [Zone ▾] [Flavor ▾] [Labels ▾] [Search] [⚙]    │
│  Billing │                                                             │
│          │ Bulk action toolbar (when rows selected):                   │
│          │   "X selected" [Start] [Stop] [Reboot] [Delete]             │
│          │                                                             │
│          │ DataTable:                                                  │
│          │   columns: ☐ Name Status IP Internal Flavor Node Age ⋯     │
│          │                                                             │
│          │ Footer pagination                                           │
└──────────┴─────────────────────────────────────────────────────────────┘
```

## Content elements

### Top header
- **Title**: "Virtual Machines" (text-xl)
- **Subtitle**: real-time counter (`{runningCount} running of {totalCount} total`)
- **"+ Create VM"** button (primary)

### Filter bar
- **Status**: dropdown (Running, Stopped, Error, Provisioning, Pending, All)
- **Zone**: dropdown (all zones in current region)
- **Flavor**: dropdown (small, medium, large, custom-flavors)
- **Labels**: tag selector (matches if VM has all selected)
- **Search**: text input, matches name, IP, ID
- **⚙ (settings)**: choose columns + density (compact / medium)

### DataTable
Default columns:

| Column | Width | Content | Style |
|--------|-------|---------|-------|
| ☐ (checkbox) | 40px | bulk select | - |
| Name | flex | name + ID (monospace, smaller) | bold name |
| Status | 100px | `<StatusBadge>` (color coded) | pill |
| External IP | 130px | IP or "—" | mono |
| Internal IP | 130px | IP or "—" | mono |
| Flavor | 100px | e.g. "small (2 vCPU)" | - |
| Node | 100px | node id | mono |
| Age | 80px | duration (3d, 5h) | - |
| ⋯ (actions) | 60px | dropdown menu | - |

**Row click** → opens VM detail.

**Row states** (visual):
- Running: green dot left of status
- Stopped: gray
- Error: red + pulse animation
- Provisioning: blue + spinner

### Bulk action toolbar (appears with selection)
- "X selected"
- Start, Stop, Reboot, Delete (with confirmation)
- Clear selection

### Footer
- Pagination: ◀ 1 2 3 ▶
- Total count: "{totalSize} VMs"

## States

### Empty (no VMs)
- **Icon**: large, faded VM icon
- **Title**: "No virtual machines yet"
- **Description**: "Create your first VM to get started. You can choose from Ubuntu, Debian, Alpine and other images."
- **CTA**: "+ Create VM"

### Loading
- Skeleton rows (5 placeholder rows with shimmering animation)

### Error
- Banner: "Failed to load virtual machines" + retry button

### Restricted
- (user without `compute.vms.list` perm): "You don't have access to view VMs. Contact your project admin."

## Interactions

- **Click row** → VM detail
- **Click checkbox** → select for bulk, show toolbar
- **Click status badge** → mini popover with state details + conditions
- **Click ⋯** → dropdown: Start/Stop/Reboot/Delete/Edit labels/Open console
- **Filter change** → query string updates, table re-renders
- **Search** → debounced 200ms

## OpenDesign prompt

```
OpenDesign session for Plexor Portal > Compute > VM List

Project: self-hosted cloud platform like Yandex Cloud
Style: minimal, technical, professional (Hetzner-style, NOT marketing-y)
Persona: senior DevOps engineer — terminal-style preferences

Required elements:
- Top bar with project switcher, search, notifications, user menu
- Left sidebar with primary nav (Compute/Storage/Network/IAM/Observability/Billing)
- Page title "Virtual Machines" with subtitle "{running}/{total} running"
- "+ Create VM" button (right)
- Filter bar with 5 controls: Status, Zone, Flavor, Labels, Search + ⚙ settings
- DataTable with columns: ☐ Name Status External IP Internal IP Flavor Node Age ⋯
- Status badges with color coding
- Bulk action toolbar on row selection
- Pagination footer

Variants needed:
- empty (no VMs) with CTA
- loading (skeleton rows)
- error
- compact density mode

Dark mode + Light mode.

File: VM-list.figma (or equivalent OpenDesign format)

Brand reference:
- Brand colors: #5E5BE8 (primary), #22D3EE (accent)
- Typography: Inter
- See /agents/docs/ui/brand.md for full tokens

Output 3 variants:
1. Desktop 1440px (primary)
2. Desktop 1024px (compact)
3. Empty state
```

## Open design decisions

Помечают дизайнер или обсуждение с командой:

- [ ] Compact mode toggle: yes/no
- [ ] Visual graph toggle (sunburst/grid view): yes/no
- [ ] Saved filters feature (как в Gmail): yes/no, Phase 2?
- [ ] Column visibility settings persistence: localStorage or per-user?
- [ ] Bulk action limit: сколько VM можно выбрать (100? 500? без лимита?)
- [ ] Cell actions (click IP → copy, etc)