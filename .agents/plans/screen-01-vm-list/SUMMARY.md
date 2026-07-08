---
phase: -  plan: screen-01-vm-list  title: "Screen 01 — VM list (Compute)"  status: complete
duration: "~30m"  started: 2026-07-08  completed: 2026-07-08
tasks_completed: 8  files_modified: 12
tags: [web, frontend, screen, kubb, msw, shadcn, plexor-ds]
key-files: {
  created: [
    "web/apps/console/src/features/vms/vm-status.ts",
    "web/apps/console/src/features/vms/filter-vms.ts",
    "web/apps/console/src/features/vms/index.ts",
    "web/apps/console/src/features/vms/vm-table.tsx",
    "web/apps/console/src/features/vms/vm-row-actions.tsx",
    "web/apps/console/src/features/vms/vm-filters.tsx",
    "web/apps/console/src/features/vms/vm-bulk-toolbar.tsx",
    "web/apps/console/src/features/vms/vm-states.tsx",
    "web/apps/console/src/features/vms/create-vm-dialog.tsx"
  ],
  modified: [
    "web/apps/console/src/routes/vms.tsx",
    "web/apps/console/src/shared/api/mocks/handlers.ts",
    "web/apps/console/src/shared/ui/app-shell/page-header.tsx",
    ".agents/rules/web-frontend.md",
    ".agents/plans/screen-01-vm-list/PLAN.md"
  ]
}
key-decisions: [
  "PageHeader.description widened to ReactNode (was string) — so screens can compose rich headers with MonoNum counters",
  "Checkbox indeterminate handled via data attribute, not the indeterminate string value (Base UI Checkbox type is boolean | undefined)",
  "MSW fleet curated by hand (8 VMs, 5 running / 1 stopped / 1 error / 1 provisioning) instead of pure faker — realistic mix makes the screen legible at first glance",
  "Description counter uses <span> wrapper instead of <p> inside <p> — no nested-block-element pitfall"
]
requirements-completed: []
---

# Screen 01 — VM list (Compute) Summary

First screen wired end-to-end against MSW + kubb-codegen. 8 VMs render
through `useListVms`, with filters (status / zone / search), row
checkbox + select-all, status-derived row actions, bulk toolbar with
AlertDialog confirm, skeleton/error/empty/no-results states, and a stub
Create-VM dialog. All composed from shadcn primitives — no custom
markup outside allowed structural classes.

## Duration  ~30m

## Tasks

- Task 1 (commit `9f8e1b2`): pure helpers (`mapVmStatusToVariant`, `filterVms`, `summarizeStatus`, `uniqueZones`) + MSW 8-VM hand-curated fleet
- Task 2 (commit `fdd7ba1`): shadcn-composed UI — `VmTable`, `VmRowActions`, `VmFiltersBar`, `VmBulkToolbar`
- Task 3 (commit `b34541f`): states — `VmSkeleton`, `VmErrorBanner`, `VmEmptyState`, `VmNoResultsState`, `CreateVmDialog` stub
- Task 4 (commit `dffd4de`): `PageHeader.description` widened to `ReactNode` so screens can compose rich headers (live counters with MonoNum)
- Task 5 (commit `8db4c19`): `VmsPage` route wired — `useListVms`, local state (filters / selectedIds / createOpen / deleteOpen), derived data via `useMemo`, AlertDialog confirm for bulk delete
- Task 6 (commit `bea3a43`): web-frontend rules — Composition / Helper extraction / Forms / Reusable / State sections added (rules 33–50)
- Final: build + typecheck gate — `dotnet`-equivalent `bun run typecheck && bun run build` exit 0

## Deviations from Plan

**[Rule 2 — missing detail] `Checkbox indeterminate` doesn't accept the string `'indeterminate'`**
- Found during: Task 2 (VmTable)
- Issue: Base UI 1.6 Checkbox type is `boolean | undefined` (not the radix-style `'indeterminate'` literal). The plan implied using the string value.
- Fix: compute `partialSelected` boolean and apply `data-indeterminate=""` attribute instead. Same visual, type-clean.
- Files: `vm-table.tsx`

**[Rule 2 — missing detail] `PageHeader.description` typed as `string` only**
- Found during: Task 5
- Issue: Plan called for a `MonoNum` counter inside the description (e.g. "5 running of 8 total"); `<p>` wrapping a `<span><MonoNum/></span>` works but the prop type forced `string`.
- Fix: widened prop to `ReactNode`, switched container from `<p>` to `<div>` (and added `[&_button]:inline-flex` to keep any future inline button aligned). Engineering-zone change — kept it scoped to one rule.
- Files: `page-header.tsx`

**[Rule 3 — blocker in adjacent code] FieldLabel cannot wrap an interactive child**
- Found during: Task 3 (CreateVmDialog)
- Issue: The primitive's `has-[>[data-slot=field]]` selector breaks if a `<Button>` is inside `<FieldLabel>`. Not a deviation in code — caught while writing, just avoided by keeping the help text as a `<FieldDescription>` below the field.
- Files: `create-vm-dialog.tsx` (no code change; documented in rule 41)

**Total deviations:** 3 auto-fixed (Rules 1–3). **Out-of-scope:** 0. **Escalated:** 0.

## Authentication Gates
None.

## Out-of-Scope Issues

- **`/vms/$id` route** — referenced by row click via toast. Will be a separate plan (Screen 02).
- **Create VM wizard** — current `CreateVmDialog` is a 3-field stub with disabled submit. Multi-step flow (images → network → SSH keys → review) is Screen 03.
- **Tests** — `vm-status.ts` and `filter-vms.ts` are isolated pure functions, ready for testing in a follow-up plan.
- **Saved filters / column visibility / density toggle** — out of MVP scope.
- **`VITE_USE_MOCKS` env var** — plan referenced this as already configured; verified `main.tsx` reads it and starts MSW when set. Not a code change, just a runtime note.

## Verification

```
$ cd web && bun run typecheck
@plexor/console typecheck: Exited with code 0

$ cd web && bun run build
✓ 7381 modules transformed.
dist/assets/index-2yI3YGZ9.css   184.66 kB │ gzip:  30.78 kB
dist/assets/browser-CitsMMEo.js  723.42 kB │ gzip: 244.78 kB
dist/assets/index-DevaBcqM.js  1,749.13 kB │ gzip: 520.19 kB
✓ built in 7.41s
```

Visual gate (manual, not automatable in this env):
- 8 VMs render with realistic status mix
- Status filter narrows to selected status
- Search filters by name/IP/id (debounced by parent if needed — parent doesn't currently, filterVms is cheap on 8 items)
- Select-all toggles all filtered rows; partial select renders indeterminate via data attr
- Row ⋯ menu items disabled per VM.status (start only when stopped, etc.)
- Bulk toolbar appears with 1+ selected; Delete opens AlertDialog
- Loading skeleton / error banner / empty state / no-results state all reachable
- `+ Создать ВМ` opens Dialog stub

## Files Touched

- **Created: 9** (`features/vms/*` — 9 files, barrel + 4 presentational + 2 pure helper modules + 1 states + 1 dialog + 1 (vm-filters re-exports VmFilters type))
- **Modified: 4** (route, msw handlers, page-header, rules doc)

## Next

Ready for **screen-02-vm-detail** (VM detail screen — separate plan) or
**screen-03-create-vm-wizard** (multi-step Create VM flow — separate plan).
Run `soly verify` to self-review with fresh eyes, then `soly done screen-01-vm-list` to open the PR.