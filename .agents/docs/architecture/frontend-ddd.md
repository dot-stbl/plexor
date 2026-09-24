# Frontend DDD — bounded contexts for `web/apps/console`

> Design-only. No code moves in this doc. Read `web/apps/console/AGENTS.md`
> first — this doc extends it, doesn't replace it. Everything about
> shadcn-only UI, i18n, icons, `bun`-only tooling still applies inside
> `domains/`.

## TL;DR

- **DDD here = folder discipline + one dependency rule**, not repositories,
  aggregates, or a mapping layer. `src/features/<name>/` (flat, per-screen)
  becomes `src/domains/<context>/{model,api,ui,mocks}` (per bounded context,
  layered). Most of today's `features/*` folders already **are** bounded
  contexts in miniature — this is mostly a rename + reinforcement, not a
  rewrite.
- **11 contexts**, mapped 1:1 or N:1 onto backend modules (`realm`/`sigil`/
  `atlas`/planned `ledger`/`forge`/`outpost`/`shard`) and product surfaces.
  Two (`storage`, `scope`) are reserved — no FE files own them yet.
- **One dependency rule, enforced by ESLint (`no-restricted-imports`,
  already the pattern in this repo — no new plugin needed):** a domain's
  `model/api/ui/mocks` internals are reachable only via its own `index.ts`;
  `shared/**` may never import `@/domains/*` at all, full stop, no
  exceptions.
- The inventory below (produced mechanically — `wc -l` + `grep` over every
  file, not summarized by a model) found **3 existing violations** of that
  exact rule (`shared/ui/app-shell` and `shared/lib` importing
  `@/features/auth/session-storage`, and `@/mocks/launcher-summary`). Fixing
  them is folded into the migration as step 0 and step 10 — see §4.
- **12 migration steps**, one context per step, each independently green
  (`bun --filter '@plexor/console' typecheck lint test build`).

---

## 1. What DDD means for this frontend

Plexor's backend is a modular monolith: schema-per-module (`sigil`/`realm`/
`atlas`/…), one `DbContext` per module, cross-module calls only through
`.Application` (see `.agents/docs/modules.md`, root `AGENTS.md`). The
frontend has no persistence and no aggregates — "DDD" here means exactly
one thing: **draw the same module boundaries the backend already has (plus
the product surfaces the backend doesn't have yet, like `catalog`/`fleet`),
and make the dependency direction between them mechanically checkable.**

Concretely:

- A **bounded context** = one product area a user thinks of as one thing
  (Virtual Machines, Networks, Audit Log) **and** usually one backend module
  or module-group.
- Each context owns its **types** (narrowed from kubb, never re-declared),
  its **derived/business logic** (status→variant mappers, invariants,
  formatters — pure functions, unit-testable, no framework), its **data
  hooks** (TanStack Query wrapping kubb, or a same-shaped mock stand-in
  where the contract doesn't exist yet), its **UI** (columns, cards, empty
  states, row actions — anything that only that screen needs), and its
  **mocks** (fixtures + the MSW handler slice for its endpoints).
- Routes stay thin: a route file registers the path + breadcrumb + title and
  renders a component from a domain's `ui/`. Routes are the one place
  allowed to compose **multiple** contexts (a create-VM wizard reads
  `compute` + `fleet`; the home page reads `compute` + `fleet` + `audit` +
  `billing`).

### What this explicitly is NOT

See §6 — no repository classes, no DTO↔entity mapping layer, no
`IVmService` interfaces, no barrel-of-barrels ceremony. A `model/` file is
often 20 lines (see `vm-status.ts` today — a single exhaustive `switch`).
That's the correct size. Don't inflate it to look "more DDD."

### The 11 contexts

| Context | Owns (today) | Backend module(s) | Status |
|---|---|---|---|
| `compute` | VMs, LXC containers, OS image catalog | `Plexor.Modules.Compute` | Active — biggest context |
| `network` | VPCs/subnets (read-only inventory today) | `Plexor.Modules.Network` | Active, small |
| `storage` | — | `Plexor.Modules.Storage` | **Reserved** — no FE page yet |
| `identity` | Login, session-adjacent UI (not the session primitive itself — see §4) | `Plexor.Modules.Identity` (`sigil`) | Active, small |
| `catalog` | Managed services (Postgres/Redis/Garnet/ClickHouse/Kafka), Managed Kubernetes | `Plexor.Modules.Marketplace` (app providers) | Active |
| `audit` | Audit log list + admin audit page | `Plexor.Modules.Audit` (`atlas`) | Active, self-contained |
| `billing` | Invoices, usage snapshot | `Plexor.Modules.Billing` (`ledger`, planned) | Active, small |
| `branding` | Org/global branding, theme marketplace, community theme registry | (no dedicated backend module yet — branding endpoints live off `realm`) | Active |
| `fleet` | Node fleet, join tokens, cluster cards | `forge` + `outpost` (planned cluster-fleet / node-registry modules) | Active — cross-context dependency of `compute`/`catalog` create-wizards |
| `scope` | Org → Team → Folder switcher | `realm` (`Plexor.Modules.Organizations`) | **Reserved** — `ScopeSwitcher` lives in `shared/ui/app-shell` today with no domain logic (no `@/features/*` imports, no API calls); promote to `domains/scope` only once real Org/Team/Folder API + business rules (permission-gated visibility, per-scope quotas) land |
| `dashboard` | Home-page widgets (fleet totals, VM status donut, quota bars, audit timeline) | cross-cutting | Active — see §3 "aggregator contexts" |

`images` sits inside `compute` (not `catalog`): its only two real
consumers, `routes/vms/new.tsx` and `routes/lxc/new.tsx`, are both compute
create-wizards; `k8s` never imports it. Putting it in `compute` means zero
cross-context imports for the highest-traffic files.

---

## 2. Per-context module layout

```
src/domains/<context>/
  model/        # types narrowed/re-exported from @/shared/api (kubb) +
                #   pure domain logic: status-machines, invariants,
                #   derived values, formatters. No JSX, no fetch.
  api/          # TanStack Query hooks. Wraps a kubb hook 1:1 when the
                #   contract exists, or a same-shaped handmade-mock hook
                #   when it doesn't yet (today's features/networks,
                #   features/databases, features/clusters pattern —
                #   keep it, it's exactly right). Query-key factories live
                #   here too (see features/audit/use-audit.ts today).
  ui/           # Context-specific components: columns, row actions, empty
                #   states, cards, list/detail page bodies. Not routes.
  mocks/        # Fixtures + this context's MSW handler slice + any nock
                #   helpers (today's src/test-utils/nock-*-api.ts).
  index.ts      # Public barrel. The ONLY import path anything outside the
                #   domain is allowed to use.
```

### Where things live outside `domains/`

| Lives in | Because |
|---|---|
| `src/routes/**` | Thin route registration (path, `staticData.crumb`, `routeHead`) + rendering a domain's `ui/` component. Routes are the one place allowed to import from **multiple** domains (a wizard, the home page). |
| `src/shared/ui/primitives/**`, `app-shell/**`, `data-table/**` | Cross-cutting UI. **Never** imports a domain (see §4's one hard exception path). |
| `src/shared/lib/**` | Cross-cutting non-UI: `cn()`, i18n bootstrap, theme/preferences provider, feature flags, **and the new `session.ts`** (§4 step 0). |
| `src/shared/api/**` | kubb-generated client/types/hooks (`src/`, untouched, never hand-edited) + the barrel (`index.ts`) + the MSW composition root (`mocks/handlers.ts`, `mocks/browser.ts`) that concatenates each domain's handler slice. |
| `src/test-utils/**` | Generic test harness (`render-with-providers`). Domain-specific nock helpers move into that domain's `mocks/` (see mapping table). |

### kubb type flow (no duplicated DTOs)

```
shared/api/src/**  (kubb-generated: types, schemas, client, hooks, fixtures, msw)
   ↓ (only via the barrel)
shared/api/index.ts   →  `export * from './src'`
   ↓
domains/<context>/model/*.ts   — imports TYPES from '@/shared/api', adds
                                  behavior (mapVmStatusToVariant, formatters).
                                  Never redeclares a shape kubb already
                                  generates. Needs a subset? `Pick<VmSummary,
                                  'id' | 'name'>`, not a new interface.
domains/<context>/api/*.ts     — imports HOOKS from '@/shared/api' (or a
                                  same-shaped handmade-mock hook while the
                                  contract doesn't exist) and re-exports
                                  under the domain's own name. This is the
                                  ONLY place a route gets domain data —
                                  routes never import '@/shared/api' hooks
                                  directly (see flagged fix, §4).
```

---

## 3. Dependency rules (enforceable)

```
routes/**        → domains/<context>  (any number of contexts — routes compose)
domains/<ctx>/**  → shared/**          (always allowed)
domains/<ctx>/**  → domains/<other>    (ONLY via <other>/index.ts — never
                                         .../model/*, .../api/*, .../ui/*, .../mocks/*)
shared/**         → domains/*          (NEVER — no exceptions)
```

The "no exceptions" line is deliberate and is why §4's session fix and
§4/step 10's `AppLauncher` fix both **invert control** (push the
domain-fetching call up into a route/composition-root and pass data down as
props) rather than carving out a "shell is special" loophole. A rule with
one documented exception rots into a rule with ten undocumented ones.

### Aggregator contexts (`dashboard`)

`dashboard`'s widgets (`FleetTotalsStats`, `VmStatusDonut`, `QuotaUsageBars`,
`AuditTimelineList`) are — and should stay — **presentational**: they take
typed props and import `@/shared/api` only for enum types (e.g. `VmStatus`
for chart legend colors), never a data hook. The actual cross-context
fetch (`compute` + `fleet` + `billing` + `audit`) happens in
`routes/index.tsx` (`HomePage`), which is allowed to import all four
domains' barrels because it's a route. No special-cased "aggregator can
reach into any domain" rule is needed — `dashboard` never violates the
`shared → domains` rule because it isn't `shared/`, it's a domain like any
other, consumed only by one route.

### Enforcement — ESLint, no new dependency

`web/tooling/eslint-config` + `web/apps/console/eslint.config.js` already
use `no-restricted-imports` with glob `group` patterns for exactly this
shape of rule (see the existing `@base-ui/*` / `react-aria-components`
bans). No `eslint-plugin-boundaries` is installed anywhere in the `web/`
workspace today (checked — zero hits outside a built `dist/` bundle), and
none is needed for two rules this small. Add:

```js
// eslint.config.js — new blocks, alongside the existing no-restricted-imports ones
{
  // Rule 1: a domain's internals are only reachable through its barrel.
  // Applies repo-wide. Legitimate intra-domain code uses relative imports
  // ('./model/vm-status'), never the '@/domains/x/...' alias form, so this
  // never false-positives on same-domain files.
  files: ['src/**/*.{ts,tsx}'],
  rules: {
    'no-restricted-imports': ['error', {
      patterns: [{
        group: ['@/domains/*/model/*', '@/domains/*/api/*', '@/domains/*/ui/*', '@/domains/*/mocks/*'],
        message: 'Import another domain only via its public barrel (@/domains/<context>), never a deep path.',
      }],
    }],
  },
},
{
  // Rule 2: shared/** never depends on a domain, not even the barrel.
  files: ['src/shared/**/*.{ts,tsx}'],
  rules: {
    'no-restricted-imports': ['error', {
      patterns: [{
        group: ['@/domains/*'],
        message: "shared/ must not depend on a domain. Invert control: the route/composition-root fetches domain data and passes it down as a prop.",
      }],
    }],
  },
},
```

(`no-restricted-imports` does not merge across flat-config blocks — this
repo's config already carries that exact warning as a code comment. Both
new blocks must repeat any patterns they need, same as the existing
react-aria block does.)

**Implemented 2026-09-24 (step 1) — not as two new trailing blocks.** Two
literal new blocks appended at the end of the array would clobber each
other for any file both match (`src/shared/**` matches both "Rule 1"'s
`src/**/*.{ts,tsx}` and "Rule 2"'s `src/shared/**/*.{ts,tsx}`, and
whichever block is last in the array wins *entirely* for that file — not
a merge). It would also silently reintroduce the react-aria/iconify bans
gap the existing 3-block structure already solved once. Instead the
existing "src except shared/ui" block was split into two — **shared/lib +
shared/api** and **everything else under src/ except shared/**
(routes/domains/mocks/test-utils/…) — and the domain-boundary pattern
each half actually needs was appended into that block's own `patterns`
array (deep-import-only ban for the "everything else" half; full
`@/domains/*` + `@/domains/*/**` ban for both shared halves, since a bare
`@/domains/*` glob's single `*` doesn't cross `/` and would otherwise miss
a deep path like `@/domains/compute/model/x` — the extra `**` pattern
closes that gap). Net effect is identical to the rule intent in §3's
table; see `eslint.config.js` for the actual four `files`-scoped blocks.

### Supplementary check script

A lint rule catches *file-level* violations but not the shape of a domain's
`index.ts` itself (is it a re-export-only barrel, or did someone sneak
logic into it?). Add `scripts/agent/check-domain-boundaries.ts` next to the
existing `scripts/agent/check.ts` / `lint-rules.ts`, in the same
self-audit-grep style as `.agents/rules/*/*.md`:

```bash
# Every domains/*/index.ts should be export-only (no function/const bodies
# beyond re-exports) — flag anything else for manual review.
rg -n '^(?!export \{|export \*|export type|/\*|\s*\*|\s*$)' src/domains/*/index.ts

# Deep cross-domain imports that slipped past eslint (belt + suspenders,
# e.g. in a .stories.tsx the lint globs don't cover yet):
rg -n "from '@/domains/[a-z-]+/(model|api|ui|mocks)/" src/
```

Wire it into `bun run agent:check` (or a new `bun run check:domains`) so it
runs in the same loop as the existing rules/format checks — no separate CI
job.

---

## 4. Mapping table — current → target

Produced from a mechanical file-by-file inventory (path, LOC, exports,
import specifiers) run via `opencode`/MiniMax workers over
`src/{features,routes,shared/ui,shared/lib,lib,mocks,shared/api/mocks}`,
spot-checked by hand against the files quoted below. Full inventory:
`scratchpad/ddd/{features-routes,shared,mocks}-inventory.md` (this
conversation's scratchpad — not part of the repo).

### `compute` (vms + lxc + images) — 22 files, ~2,500 LOC across features + ~1,800 in 4 routes

| Current | Target |
|---|---|
| `features/vms/{vm-status.ts,vm-states.tsx}` | `domains/compute/model/` |
| `features/vms/{vm-columns.tsx,vm-row-actions.tsx,vm-bulk-toolbar.tsx,vm-console-card.tsx,vm-console-placeholder.tsx,vm-terminal-placeholder.tsx}` | `domains/compute/ui/` |
| `features/lxc/lxc-types.ts` | `domains/compute/model/` |
| `features/lxc/lxc-columns.tsx` | `domains/compute/ui/` |
| `features/images/{image-types.ts,image-data.ts}` | `domains/compute/model/` (static OS catalog — not backend-fetched, not MSW-mocked; keep as model, not `mocks/`) |
| `features/images/image-columns.tsx` | `domains/compute/ui/` |
| `shared/api/mocks/handmade/lxc.ts` | `domains/compute/mocks/lxc-fixtures.ts` |
| `features/{vms,lxc,images}/index.ts` | merge into `domains/compute/index.ts` |
| `routes/vms/{route,index,new,$id,$id.stories}.tsx`, `routes/lxc/{route,index,new}.tsx`, `routes/images.tsx` | stay in `routes/`, update imports to `@/domains/compute` (+ `@/domains/fleet` for placement — cross-context, expected) |

**Flagged:** `routes/vms/index.tsx` imports `useListVms` from `@/shared/api`
directly, bypassing the feature layer entirely (every other domain's route
goes through its `use-*` hook). Normalize during this step: add
`export { useListVms, ... } from '@/shared/api';` to
`domains/compute/api/index.ts` and repoint the route. Zero behavior change,
just a consistent seam for when compute's other endpoints (LXC, images)
grow real contracts.

### `fleet` (clusters) — 7 files, ~730 LOC + 3 routes

| Current | Target |
|---|---|
| `features/clusters/cluster-types.ts` | `domains/fleet/model/` |
| `features/clusters/{cluster-card,node-row,token-row,add-node-dialog}.tsx` | `domains/fleet/ui/` |
| `features/clusters/use-clusters.ts` | `domains/fleet/api/` |
| `mocks/clusters.ts` **+** `shared/api/mocks/handmade/clusters.ts` | **consolidate** into `domains/fleet/mocks/fixtures.ts` — today these two files overlap (`listClusters`/`getCluster` in both, one set consumed by `use-clusters.ts`, the other by dashboard/tests) and both import `@/features/clusters/cluster-types` back across the boundary. Once co-located this becomes an intra-domain import and the duplication is visible enough to actually delete one copy. **Coordinate with the concurrent mocks-reorg agent (see §5) before touching this file** — it may already be mid-move. |
| `routes/clusters/{route,index,$id}.tsx` | stay in `routes/`, repoint to `@/domains/fleet` |

### `catalog` (k8s + managed databases) — 12 files, ~1,000 LOC + 8 routes

| Current | Target |
|---|---|
| `features/k8s/{k8s-types.ts}` | `domains/catalog/model/` |
| `features/k8s/k8s-columns.tsx` | `domains/catalog/ui/` |
| `shared/api/mocks/handmade/k8s.ts` | `domains/catalog/mocks/` |
| `features/databases/{database-types.ts,managed-routes.ts}` | `domains/catalog/model/` |
| `features/databases/{database-columns.tsx,runtime-badge.tsx,runtime-picker.tsx,managed-service-empty.tsx,managed-service-page.tsx}` | `domains/catalog/ui/` |
| `features/databases/use-databases.ts` | `domains/catalog/api/` |
| `shared/api/mocks/handmade/databases.ts` | `domains/catalog/mocks/` |
| `routes/k8s/{route,index,new}.tsx`, `routes/managed/{route,index,new,postgres,redis,garnet,clickhouse,kafka}.tsx` | stay in `routes/`, repoint (note: `k8s/new.tsx` and `managed/new.tsx` also import `@/features/clusters` — migrate `fleet` first, see §5 ordering) |

### `network` — 4 files + 1 route (small, self-contained)

`features/networks/**` → `domains/network/{model:—, api:use-networks.ts,
ui:network-columns.tsx+networks-empty.tsx}`; `shared/api/mocks/handmade/
networks.ts` → `domains/network/mocks/`; `routes/networks.tsx` +
`routes/networks.stories.tsx` stay, repoint.

### `audit` — 5 files + 3 routes (smallest active context — do this first)

`features/audit/**` → `domains/audit/{model:audit-types.ts, api:use-audit.ts,
ui:audit-columns.tsx+audit-empty.tsx}`; `mocks/audit.ts` →
`domains/audit/mocks/`; `routes/audit.tsx`, `routes/audit.stories.tsx`,
`routes/admin/audit.tsx` (+ its test) stay, repoint.

### `billing` — 3 files + 2 routes

`features/billing/**` → `domains/billing/{api:use-billing.ts,
ui:billing-columns.tsx}`; `shared/api/mocks/handmade/billing.ts` →
`domains/billing/mocks/`; `routes/billing.tsx` +
`routes/billing.stories.tsx` stay, repoint.

### `branding` — 2 files + 3 routes

**Reconciled 2026-09-24 (step 4) — `shared/lib/themes/**` does NOT move.**
Overridden from the line below: it stays exactly where it is, for the same
reason `session-storage.ts` became a shared kernel in step 0, not a domain
file. `shared/lib/preferences-provider.tsx` (applied on every app load,
not branding-admin-specific) imports `getPreset`/`DEFAULT_PRESET_ID` from
it directly, and `main.tsx`'s pre-render boot script imports it too. If
`themes/**` moved under `domains/branding/model/`, `preferences-provider`
(which lives under `shared/**`) would have to import a domain — exactly
the edge §3 declares to have no exceptions. Only the branding-*domain*
hooks move; the theme *registry/resolution* mechanism stays shared
kernel, same split as identity's session mechanism vs. its login UI.

`features/branding/use-branding.ts` → `domains/branding/api/` (already a
clean kubb-hook wrapper — no change to its shape, just its address);
`features/themes/use-community-themes.ts` → `domains/branding/api/`
(imports from `@/shared/lib/themes` unchanged — see reconciliation note
above);
`routes/admin/{branding, theme-marketplace}.tsx` (+ tests) stay, repoint
— both currently deep-import their hook file directly (`features/branding`
and `features/themes` never had an `index.ts` barrel), so this step is
also the first time either route goes through a barrel.

### `identity` — 4 files + 2 routes (session primitive carved out first — see step 0)

`features/auth/{login.schema.ts,login-error.ts,login-page.tsx,
provider-icons.tsx}` → `domains/identity/{model:login.schema.ts+
login-error.ts, ui:login-page.tsx+provider-icons.tsx}`; `routes/login.tsx`
stays, repoints to `@/domains/identity`. `routes/settings/profile.tsx`
stays as-is (already thin-ish; it reads `@/shared/lib/session` +
`@/shared/lib/preferences-provider`, no domain import needed).

### `dashboard` — 5 files (widgets only; no route move — `routes/index.tsx` already composes)

`features/dashboard/**` → `domains/dashboard/ui/` verbatim (all four
widgets are already pure-presentational, per §3). `index.ts` moves as-is.

### `scope`, `storage` — no files to move (reserved, see §1 table)

### Flagged: the 3 `shared → feature` violations found by the inventory

| File | Imports (violation) | Fix |
|---|---|---|
| `shared/ui/app-shell/app-sidebar.tsx` + its `.test.tsx` | `@/features/auth/session-storage` | Repoint to new `@/shared/lib/session` (step 0 below) |
| `shared/lib/preferences-provider.tsx` + its `.test.tsx` | `@/features/auth/session-storage` | Same |
| `shared/ui/app-shell/app-launcher.tsx` | `@/mocks/launcher-summary` | Invert control (step 10 below) |

`session-storage.ts`'s **mechanism** (read/write the bearer-token+user blob
in `localStorage`) is cross-cutting in the same way the backend's
`ICurrentUser` is a `Plexor.Shared.Authorization` abstraction every module
depends on (see `.agents/docs/architecture/identity.md` §"`ICurrentUser`"),
not an Identity-module-only concern. It becomes `shared/lib/session.ts` —
a shared kernel, not a domain. What stays in `domains/identity` is the
**login-domain-specific** logic: the form, its schema, its error-code
mapping, the OAuth provider icons. Nothing in `shared/` needs those.

---

## 5. Migration plan

Every step: `bun --filter '@plexor/console' typecheck lint test build`
must exit 0. Additionally, `bun run test:visual` (the committed Storybook
baselines) must stay green — a failure here after a pure file-move means an
import got rewritten wrong or a behavior changed, **not** a baseline to
regenerate (`web/apps/console/AGENTS.md`: never regenerate/commit visual
baselines). Optionally spot-check a touched route with the disposable
`bun run shot page <path> --theme both`.

**Reconciled 2026-09-24 — mocks reorg landed first.** The concurrent mock
reorg referenced above landed before this migration started executing:
`src/mocks/{db,identity,compute,network,storage,catalog,audit,billing,
branding}/` now exists, and `shared/api/mocks/handmade/*` are one-line
`export *` shims pointing at the matching `mocks/<context>/*` fixture
body (see `web/apps/console/src/mocks/README.md`). This changes two
things from the original plan text:

1. **Decision: `mocks/` stays where it is — domains do NOT get their own
   `mocks/` subfolder.** The per-context module layout in §2 collapses
   from `{model,api,ui,mocks}` to `{model,api,ui}` for every context.
   Reasoning: `shared/api/mocks/handlers.ts` (the MSW composition root)
   lives under `shared/**` and must read fixture data to answer
   contract-endpoint requests (`FLEET`, `queryAuditEntries`, …). If
   fixtures moved into `domains/<context>/mocks/`, `handlers.ts` would
   have to import a domain — exactly the `shared/** → domains/*` edge
   §3 declares to have **no exceptions**. Keeping fixtures in the
   top-level `src/mocks/<context>/` tree (a sibling of `domains/`, not
   inside `shared/`) sidesteps the conflict entirely: `handlers.ts`
   importing `@/mocks/<context>` is not a domain import, and a domain's
   `api/*.ts` hook importing `@/mocks/<context>` (directly, or via the
   `shared/api/mocks/handmade/*` shim, unchanged) is likewise not a
   `shared/**` import. No migration step below moves a fixture file;
   each step's `api/` hooks keep their existing
   `@/shared/api/mocks/handmade/*` (or `@/shared/api`) import lines
   verbatim — this is a pure relocation of feature code, zero import
   churn on the mock side.
2. **Reconciled: `mocks/catalog/clusters.ts` → `mocks/fleet/clusters.ts`.**
   The mocks reorg grouped physical-node-fleet fixtures (`PlexorCluster`/
   `PlexorNode`/`JoinToken` — join tokens, node roster, cluster cards)
   under `catalog/` alongside k8s/databases, but per §1's table those are
   two different bounded contexts: `fleet` (`forge`+`outpost`, node
   fleet/join tokens) vs. `catalog` (`Marketplace`, managed k8s/DB
   services). Step 6 below renames the one file
   (`git mv mocks/catalog/clusters.ts mocks/fleet/clusters.ts`) and
   updates its ~3 importers (`mocks/index.ts`, `mocks/README.md`,
   `shared/api/mocks/handmade/clusters.ts`) so the mock tree's context
   names match the domain names 1:1. `mocks/catalog/{k8s,databases}.ts`
   are correctly named already and don't move.

| # | Scope | Depends on | Risk |
|---|---|---|---|
| 0 | Extract `shared/lib/session.ts` from `features/auth/session-storage.ts`; repoint its ~7 current consumers (`app-sidebar.tsx`+test, `preferences-provider.tsx`+test, `routes/index.tsx`, `routes/settings/profile.tsx`, `features/auth/login-page.tsx`+test). No `domains/` yet. | — | Low — pure move, mechanical, small blast radius |
| 1 | Scaffold `src/domains/` (empty `.gitkeep`-style placeholder is unnecessary — skip until step 2 has content). Land the two ESLint blocks (§3) + `check-domain-boundaries.ts`. Everything still passes because nothing violates yet. | 0 | Low — additive config only |
| 2 | Migrate `audit` (smallest, zero cross-context imports). Proves the recipe end to end. | 1 | Low |
| 3 | Migrate `billing` (same shape, slightly smaller). | 2 (pattern proven, not a hard dep) | Low |
| 4 | Migrate `branding` (includes theme registry data + 3 admin routes). | 2 | Low-medium — more consumers (3 routes + tests) |
| 5 | Migrate `network` (small, self-contained). | 2 | Low |
| 6 | Migrate `fleet`. Consolidate the `mocks/clusters.ts` / `shared/api/mocks/handmade/clusters.ts` duplication (coordinate per note above). | 2 | Medium — the mock consolidation is the one non-mechanical part of this whole plan |
| 7 | Migrate `catalog` (k8s + managed databases). Its `k8s/new.tsx` and `managed/new.tsx` routes import `fleet` — must land after step 6. | 6 | Medium — most routes touched (8) |
| 8 | Migrate `compute` (vms + lxc + images) — largest context. Its create-wizard routes import `fleet` — must land after step 6. Also normalize `routes/vms/index.tsx`'s direct `@/shared/api` hook import (flagged in §4). | 6 | Medium-high — most LOC, most routes (7), the one intentional non-pure-move edit |
| 9 | Migrate `identity` (login domain logic only — session already moved in step 0). | 0 | Low |
| 10 | Migrate `dashboard`; fix the `AppLauncher` violation by extracting a `useLauncherSummary` hook into `domains/dashboard/api/`, calling it from wherever `<AppShell>`/`<AppLauncher>` is composed (likely `__root.tsx` or `main.tsx`), and changing `AppLauncher` to take `summary` as a prop instead of importing `@/mocks/launcher-summary` itself. Depends on `compute`, `fleet`, `audit` all being migrated (the summary aggregates all three). | 2, 6, 8 | Medium — the one other non-pure-move edit; verify `AppLauncher`'s existing story/test still covers the props-driven shape |
| 11 | Cleanup: delete emptied `src/features/`, `src/mocks/`; confirm `check-domain-boundaries.ts` reports zero violations repo-wide; update `web/apps/console/AGENTS.md`'s "Where things are" table (`src/features/<name>/` → `src/domains/<context>/`) in a small follow-up PR. | all above | Low |

Steps 2–5, 7, 9 can run in any order relative to each other (no
cross-dependencies) — only 6→{7,8} and {2,6,8}→10 are hard-ordered. If
parallelizing across agents/sessions, that's the graph to respect.

---

## 6. What NOT to do

- **No `IVmRepository` / `IVmService` interfaces.** There's no persistence
  to abstract on the frontend — `api/` hooks call kubb-generated functions
  directly. An interface with exactly one implementation is ceremony.
- **No DTO→entity mapping layer.** kubb's generated types ARE the domain
  types wherever no extra behavior is needed. `model/` adds behavior
  (mappers, formatters, invariants) on top of the kubb type — it doesn't
  wrap it in a second parallel type.
- **No class-based domain model.** `VmStatus` stays a string union +
  exhaustive `switch`, not a `VmStatusValueObject` class. This matches the
  backend's own `Plexor.Shared.Filtering` style (flat, DSL-based, not
  hand-rolled OOP) and the existing `vm-status.ts`.
- **No barrel-of-barrels.** `domains/index.ts` re-exporting every context
  doesn't exist — routes import each context's barrel directly
  (`@/domains/compute`, `@/domains/fleet`). A god-barrel just hides which
  routes actually depend on which contexts.
- **No forcing `storage`/`scope` into existence early.** They're reserved
  because there's no FE surface to move yet — inventing a `domains/storage/`
  folder with nothing in it to "complete the set" adds a maintenance
  target for zero present value. Add it the day the first Volume/Bucket
  page is planned.
- **No big-bang PR.** One context per commit/PR per step 5's table. A
  failing step 8 (compute) should never block steps 2–7 having already
  landed.

---

## 7. Status (execution log)

Updated live during execution. `done` means the step's gate
(`typecheck lint test`, plus `build` + `test:visual` at milestones 3/7/11)
was green when the step landed, not just that files moved.

| # | Scope | Status | Notes |
|---|---|---|---|
| 0 | Extract `shared/lib/session.ts` | done | `git mv features/auth/session-storage.ts -> shared/lib/session.ts`; repointed 10 consumers (login-page +test, routes/index +test, settings/profile +test, preferences-provider +test, app-sidebar +test) + `mocks/identity/auth.ts`. typecheck/lint/test all green (44 files / 305 tests, same as baseline). |
| 1 | Scaffold `domains/`, ESLint boundary rules, `check-domain-boundaries.ts` | done | Done natively (not delegated — flat-config non-merge semantics need care, see §3 note). `eslint.config.js` split the old "src except shared/ui" block into shared/lib+api / everything-else, each carrying the domain-boundary pattern it needs; shared/ui block also got the full ban. New `scripts/agent/check-domain-boundaries.ts` (barrel-shape + deep-import grep, TS port of the plan's `rg` one-liners) wired as `bun run check:domains` and as a step in `agent:check`. `src/domains/` not created yet (no content until step 2). typecheck/lint/check:domains/test all green. |
| 2 | `audit` | done | opencode worker ok (no retry needed). `features/audit/*` -> `domains/audit/{model,api,ui}/` + new barrel; repointed `routes/audit.tsx`, `routes/audit.stories.tsx`, `routes/admin/audit.tsx` (the last one now goes through the barrel instead of a deep `use-audit` import). Orchestrator fixed 2 nits post-worker: barrel JSDoc indentation, and a `check-domain-boundaries.ts` regex bug (didn't tolerate ` * ` continuation-line indent) exposed by this step's own barrel — script fixed, not the barrel. All gates green. |
| 3 | `billing` | done | opencode worker ok, no retry, no orchestrator fixes needed. `features/billing/*` -> `domains/billing/{api,ui}/` + new barrel (no `model/` — no domain type of its own); repointed `routes/billing.tsx` + `.stories.tsx`. `BillingSnapshot`/`getBillingSnapshot` mock imports left untouched per the mocks decision. All gates green. |
| 4 | `branding` | done | 1 retry: first opencode run burned its whole budget diagnosing a Windows `git mv` quirk (destination dir must exist first — `git mv` doesn't create it) and hit the 560s wall with zero files touched; `--session` continuation with the diagnosis handed to it finished in one pass. `features/branding/use-branding.ts` + `features/themes/use-community-themes.ts` -> `domains/branding/api/` (single domain absorbs both), new barrel; repointed `routes/admin/branding.tsx` + `routes/admin/theme-marketplace.tsx` (both now go through a barrel for the first time — neither `features/branding` nor `features/themes` had one). `shared/lib/themes/**` correctly left untouched (see step 4 reconciliation note above §4). All gates green. |
| 5 | `network` | done | opencode worker ok, no retry (applied the Windows `git mv` destination-dir lesson from step 4 proactively). `features/networks/*` -> `domains/network/{api,ui}/` (singular folder name) + new barrel; repointed `routes/networks.tsx` + `.stories.tsx`. All gates green. |
| 6 | `fleet` (+ `mocks/catalog/clusters.ts` → `mocks/fleet/clusters.ts` rename) | done | Biggest step so far — ran as 2 worker turns (PART 1 mocks rename, PART 2 domain move), both via the same `--session` (not a failure-retry, planned split given size). PART 1 finished clean but the worker's own grep-generated TODO list (2 leftover relative imports in `mocks/audit/audit.ts` + `mocks/launcher-summary.ts` still pointing at `../catalog/clusters`) went unaddressed when it hit the 560s wall before starting PART 2 — orchestrator fixed those 2 imports + 1 header-comment path reference directly (each a 1-line change) before continuing. PART 2 continuation then did the full `features/clusters/*` -> `domains/fleet/{model,api,ui}/` move + barrel + repointed 5 routes (`clusters/index`, `clusters/$id`, `k8s/new`, `lxc/new`, `vms/new` — the last two left their unrelated `images`/other imports untouched) in one pass. All gates green, 305 tests. |
| 7 | `catalog` (k8s + databases) | done | 2 planned worker runs (moves+barrel, then 7 route repoints), both clean — no retries. `features/k8s/*` (2 files) + `features/databases/*` (8 files) -> `domains/catalog/{model,api,ui}/`, one combined barrel; repointed `routes/k8s/index.tsx` + 6 `routes/managed/*.tsx`. **Milestone gate (post-step-7): `bun run build` clean (0 errors, only pre-existing chunk-size warnings), `bun run test:visual` 14/14 suites, 62/62 snapshots green.** |
| 8 | `compute` (vms + lxc + images) | done | Largest step — planned 3 worker runs (images+lxc moves / vms moves / barrel+7-route-repoint). Run 1 and run 2 clean. Orchestrator caught and fixed 1 miss after run 2: `vm-console-card.tsx` still used the old `@/features/vms/vm-console-placeholder` alias-form import instead of the specified relative form — 2-line fix. **Run 3 hit a persistent opencode/hapy gateway outage (3 consecutive "Cannot connect to API" failures, confirmed not a worker mistake — `opencode models` itself worked)** — per the cost/escalation rule, orchestrator completed run 3 natively: wrote the barrel, repointed all 7 routes, then grep caught 2 more leftover deep imports the brief hadn't anticipated (`mocks/compute/images.ts` and `mocks/compute/lxc.ts` importing `OsImage`/`LxcContainer` from the old `@/features/{images,lxc}/*-types` path) and fixed both to import from the `@/domains/compute` barrel instead (type-only, so the resulting mocks→domain circular reference erases at compile time — safe). Also found and fixed a real bug in `check-domain-boundaries.ts` itself: its barrel-shape checker didn't tolerate multi-line named-export lists or `//` section comments — every earlier barrel had been auto-compacted onto single lines by its worker, masking the bug until this domain's hand-written multi-line barrel tripped it. Fixed with a small state-machine (track "inside a multi-line export list") instead of pure line-by-line matching. All gates green after the fix: typecheck/lint/check:domains/test (305 tests). |
| 9 | `identity` | done | Done natively — the opencode/hapy gateway was still down after 3 fresh connection-failure retries (`Cannot connect to API`, confirmed not a worker/brief issue). `features/auth/{login.schema.ts,login-error.ts,login-page.tsx,provider-icons.tsx,login-page.test.tsx}` -> `domains/identity/{model,ui}/` (colocated test travels with its subject); new barrel; repointed `routes/login.tsx` (deep-imported before, no barrel existed) and fixed a stale doc-comment in that file referencing the old `@/features/auth` path. All gates green, 305 tests (login-page.test.tsx runs fine from its new path). |
| 10 | `dashboard` + `AppLauncher` prop-drilled summary fix | done | Done natively (behavior-sensitive prop-threading across 4 files + an 18-assertion test — too risky to hand to a flaky worker, and the gateway was still down). **Correction to the plan's own text:** `routes/index.tsx` (the home page) does NOT currently compose any dashboard widget — grepped, zero consumers — so the widget move (`features/dashboard/*` -> `domains/dashboard/ui/`, verbatim, + barrel) had no route to repoint, contrary to the plan's "no route move — routes/index.tsx already composes" note. **AppLauncher fix — real composition chain was 3 levels, not 1:** `routes/__root.tsx` → `AppShell` → `AppSidebar` → `AppLauncher` (the plan guessed `AppLauncher` might be composed directly in `__root.tsx`/`main.tsx`; it's actually nested two shared/ui layers deep). Added `domains/dashboard/api/use-launcher-summary.ts` (wraps `makeLauncherSummary()` in `useMemo`); `__root.tsx` now calls it and passes `launcherSummary` down through `AppShell` → `AppSidebar` (both take it as an **optional** prop defaulting to `[]`, so `<AppSidebar />`'s existing zero-prop test usage still compiles/passes unchanged) → `AppLauncher` (**required** prop, since that's the one component whose job actually depends on it). `AppLauncher`/`AppShell`/`AppSidebar` all still import `type LauncherSummaryCard` from `@/mocks/launcher-summary` (type-only — not a domain, not banned) since the domain barrel itself is off-limits to `shared/**`. Updated `app-launcher.test.tsx`'s `renderLauncher()` helper to pass `summary={makeLauncherSummary()}` explicitly. All 18 app-launcher assertions + all 12 app-sidebar assertions still pass unchanged, full suite 305/305. `src/features/` is now fully empty/gone as a side effect (nothing left for step 11's cleanup on that front). |
| 11 | Cleanup: delete `src/features/`, update `AGENTS.md`, zero boundary violations | done | `src/features/` already gone (each step's own moves emptied it — nothing left to delete). **`src/mocks/` is explicitly NOT deleted** — overrides the plan's original "delete emptied src/features/, src/mocks/" line, superseded by this doc's own mocks-reconciliation decision (§5 intro): mocks stays a live, independent fixture tree every domain's `api/` still imports from (via `@/shared/api/mocks/handmade/*` or `@/mocks/<context>` directly), not something the migration empties. `check:domains` reports 0 violations repo-wide. Updated `web/apps/console/AGENTS.md` ("Where things are" table + new "Domains" section + hard rule 14) and 4 other stale docs found by a repo-wide `@/features/` grep: `docs/agent/new-page.md`, `docs/agent/rules-cheatsheet.md`, `shared/api/mocks/handmade/README.md`, `scripts/agent/lib/browser-prep.ts` (comment). Left `src/shared/ui/INDEX.md`'s one hit alone — it's inside a dated "Appendix: audit 2026-09-20" section, an accurate historical record, not live guidance. **Final milestone gate: typecheck/lint/check:domains/test all green (305 tests), `bun run build` clean, `bun run test:visual` 14/14 suites, 62/62 snapshots.** |
