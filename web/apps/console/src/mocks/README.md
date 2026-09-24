# Mock fixtures — shared source of truth

The `web/apps/console/src/mocks/` directory is the **single source of
truth for hand-curated mock data that feeds the contract-endpoint MSW
handlers** (`shared/api/mocks/handlers.ts`), the launcher SUMMARY
cards, and component tests.

Fixtures are grouped by **bounded context** so they slot into future
backend domain modules without rearrangement: `compute` (VMs, images,
LXC), `network` (VPCs/subnets), `storage` (volumes), `identity`
(auth/session), `catalog` (managed DBs, k8s), `fleet` (clusters), `audit`,
`billing`, `branding`. The `db/` folder holds MSW-handler plumbing
(their scenario switch, shared latency, the faker seed) and is **not**
part of the fixture catalog at this layer — it lives next to the
fixtures so the whole mock "database" stays in one tree.

> Endpoints **not yet in the OpenAPI contract** are still re-exported
> from `shared/api/mocks/handmade/` for back-compat, but the source
> fixture bodies now live in `mocks/`. Each handmade module carries a
> `TODO(contract)` header plus a one-line `export *` shim, so existing
> feature imports resolve unchanged.

## Why a dedicated `mocks/` directory?

The MSW handlers (`shared/api/mocks/handlers.ts`) and component tests
both need access to the same shape and the same numbers. Before this
directory existed:

- The launcher SUMMARY card showed `—` / `нет данных` because it had
  no way to read from the same data the handlers responded with.
- Component tests either inlined their own fake data (drifting from
  the API) or skipped the assertions that needed realistic numbers.

A shared `mocks/` directory means:

- **One edit, two consumers.** Edit a fixture here; the MSW handler
  returns the new value AND the launcher / a component test that
  reads from the same fixture updates together.
- **Shape parity with the OpenAPI contract.** Each fixture matches
  its kubb-generated type from `shared/api/src/types/*`, so the
  handler response body and the kubb hook's input both consume the
  same shape.
- **Deterministic mocks.** `resetMockRng()` seeds faker with a fixed
  seed at module load — same fleet on every reload, every test, every
  screenshot.

## Files

| File | What it exports | Consumers |
|------|-----------------|-----------|
| `db/seed-config.ts` | `MOCK_SEED`, `MOCK_TIMESTAMP`, `resetMockRng()` | Imported by every other fixture file in this directory; also handy for tests that need stable fake ids. |
| `db/scenario.ts` | `MockScenario`, `getMockScenario()` | **Internal MSW-handler plumbing** — not exported from the top barrel. Let a dev/demo/test force empty / error / slow via `?mock=` or `localStorage` without editing fixtures. |
| `db/latency.ts` | `mockDelay()` | **Internal MSW-handler plumbing** — not exported from the top barrel. Seeded artificial latency so the mock feels like a real network. |
| `identity/auth.ts` | `DEV_SESSION` | Tests that seed an authenticated session before rendering a protected route. |
| `compute/vms.ts` | `FLEET`, `FLEET_BY_ID`, `FLEET_PLACEMENT`, `makeVmList()`, `countByStatus()` | MSW handler for `GET /vms`, VM list page tests, launcher SUMMARY card. `FLEET_PLACEMENT` is the cross-reference table for the handler that synthesizes `VmDetail`. |
| `compute/nodes.ts` | `NODES`, `NODE_BY_ID` | Single source of truth for the physical node roster. Embedded by `fleet/clusters.ts` (the cluster owns its nodes) and consumed by `compute/vms.ts` via `FLEET_PLACEMENT` (which node each VM runs on). |
| `compute/images.ts` | `listImages()` | Image catalog page tests (LXC + VM create flows). Moved from `features/images/image-data.ts`; the old path is now a shim. |
| `compute/lxc.ts` | `listLxc()` | LXC page tests. Body moved from `shared/api/mocks/handmade/lxc.ts`; shim re-exports. |
| `identity/users.ts` | `USERS`, `USER_BY_ID`, `MockUser` | Small real roster so audit entries can reference an actual actor id. `identity/auth.ts`'s `DEV_SESSION` is the same operator as `USERS[0]`. |
| `network/networks.ts` | `Network`, `NetworkStatus`, `Subnet`, `SUBNET_BY_ID`, `listNetworks()`, `listSubnets()`, `hostInSubnet()` | Networks page tests. Body moved from `shared/api/mocks/handmade/networks.ts`; shim re-exports. `hostInSubnet()` lets `compute/vms.ts` place every VM at a real address inside a real subnet. |
| `storage/volumes.ts` | `MockVolume`, `VolumeKind`, `VOLUMES`, `volumesForVm()` | Not wired into any MSW handler yet — establishes the VM → root volume relation inside the mock database (one root volume per fleet VM, sized to its `diskGb`). Future detail/audit work reads from here. |
| `fleet/clusters.ts` | `CLUSTERS`, `CLUSTER_BY_ID`, `listClusters()`, `getCluster()`, `clusterSummary()`, `NodeStatus` | MSW handler for cluster list endpoints (when they land), cluster page tests, launcher SUMMARY card. Two cluster fixtures (this file + the old `catalog/clusters-install.ts`) were reconciled into this single source of truth. |
| `catalog/k8s.ts` | `K8sCluster`, `listK8s()` | k8s page tests. Body moved from `shared/api/mocks/handmade/k8s.ts`; shim re-exports. |
| `catalog/databases.ts` | `DbEngine`, `DbCluster`, `RuntimeHost`, `listRuntimeHosts()`, `listEngines()`, `getEngine()`, `listDbClusters()` | Managed-data-service page tests. Body moved from `shared/api/mocks/handmade/databases.ts`; shim re-exports. |
| `branding/branding.ts` | `GLOBAL_BRANDING`, `ORG_BRANDING`, `getGlobalBranding()`, `getOrgBranding()` | MSW handlers for branding endpoints, admin/branding page tests. |
| `audit/audit.ts` | `makeAuditEntries(count)`, `queryAuditEntries(filter)`, `appendAuditEntry(entry)`, `resetAuditLog()`, `AuditQueryFilter`, `LiveAuditInput` | MSW handler for `GET /audit`, admin/audit page tests, launcher SUMMARY card. Combines 24 deterministic historical rows with the live log `db/store.ts` appends to. |
| `billing/billing.ts` | `BillingEdition`, `BillingPlan`, `BillingUsageRow`, `InvoiceStatus`, `BillingInvoice`, `BillingPaymentMethod`, `BillingSnapshot`, `getBillingSnapshot()`, `formatAmount()` | Billing page tests. Body moved from `shared/api/mocks/handmade/billing.ts`; shim re-exports. |
| `db/store.ts` | `listVms()`, `getVm()`, `getVmPlacement()`, `getVmDetailExtras()`, `createVm()`, `startVm()`, `stopVm()`, `deleteVm()`, `resetStore()` | **Internal MSW-handler plumbing** — not exported from the top barrel. Mutable half of the mock "database": `db/store.ts` seeds a `Map<vmId, VmRecord>` from `FLEET` + `FLEET_PLACEMENT` and exposes the mutations the VM endpoints drive. |
| `launcher-summary.ts` | `makeLauncherSummary()` | The launcher SUMMARY cards (instead of the previous static `—` placeholder). Cross-cutting aggregator — sits at the top level rather than inside a bounded context folder. |
| `index.ts` | Barrel re-export | One import path for the whole convention. Re-exports every fixture module; intentionally **omits** `db/scenario`, `db/latency`, and `db/store` (handler plumbing). |

## Scenario switches

`shared/api/mocks/handlers.ts` honors a per-request scenario switch that
lets a dev / demo / test force a specific mock behavior without editing
fixtures. The active scenario is read once per call, so a URL change
takes effect on the next request without a reload.

- **URL query param** — `http://localhost:5173/vms?mock=empty` (works on
  any console page; the handler ignores paths it doesn't recognize).
- **Devtools localStorage key** — `localStorage.setItem('plexor:mock-scenario', 'slow')`.
  A query param wins over localStorage when both are set.
- **No flag** — falls back to `'default'` (normal seeded latency, real data).

| Value | Effect |
|---|---|
| `'default'` | Normal seeded latency, real fixture data. |
| `'empty'` | List/collection endpoints return zero rows — drives the empty-state UIs. |
| `'error'` | The endpoint returns a 500 `application/problem+json` response. |
| `'slow'` | `mockDelay()` multiplies its latency ~6x, so the UI sits in its loading state long enough to look real. |

`GET /vms` and `GET /audit` (the two MSW handlers with the wiring today)
honor the switch. Other list endpoints don't check it yet, so don't
assume every handler supports it — read the handler before reaching for
a scenario value.

Under Vitest there's no `window`, so `getMockScenario()` returns
`'default'` and the suite is unaffected.

## Mutations + status progression

`db/store.ts` is the mutable half of the mock "database" — every read
through `listVms()` / `getVm()` / `getVmDetailExtras()` walks the same
`Map<vmId, VmRecord>` that the mutation functions write to, so the MSW
handlers see the user's actions on the very next request.

- **Seeding.** At module load the store builds the `Map` once from
  `compute/vms.ts`'s `FLEET` + `FLEET_PLACEMENT` (skipping any fleet
  entry with no placement).
- **`createVm()`.** Allocates a new id, inserts a record with
  `status: 'provisioning'` and a `provisioningUntil` wall-clock deadline
  ~6 seconds out. The record's `resolve()` step flips the status to
  `'running'` lazily on the next read once that deadline passes — so a
  freshly created VM really does appear as `provisioning` and later as
  `running`, with no background timer keeping a Node event loop alive.
- **`startVm()` / `stopVm()` / `deleteVm()`.** Mutate the record
  immediately: start/stop overwrite `status`, delete removes the entry.
  All three report failure (`undefined` / `false`) on an unknown id.
- **Live audit.** Every mutation calls `appendAuditEntry()` — those
  prepended live rows, combined with the 24 deterministic historical
  rows, are what `GET /audit` returns.
- **Test isolation.** `resetStore()` re-seeds from the fixtures and
  zeroes the id sequence; `resetAuditLog()` clears live rows but keeps
  the historical seed. Call them in `afterEach` if a test creates or
  mutates VMs and needs a clean slate for the next one.

## Import patterns

```ts
// In an MSW handler
import { FLEET, FLEET_BY_ID } from '@/mocks';

// In a component test that seeds a session
import { DEV_SESSION } from '@/mocks';
writeSession(DEV_SESSION);

// In the launcher SUMMARY card
import { makeLauncherSummary } from '@/mocks';
const cards = makeLauncherSummary();

// In an MSW handler that wants the scenario switch, seeded latency, or the mutable VM store
// (NOT through the barrel — these are internal handler plumbing).
import { mockDelay } from '@/mocks/db/latency';
import { getMockScenario } from '@/mocks/db/scenario';
import { createVm, listVms } from '@/mocks/db/store';
```

## Anti-patterns — DON'T do these

- ❌ Inline a fake fleet in a component test ("vm-1, vm-2, vm-3").
  Read from `FLEET` so the test stays in sync with the API.
- ❌ Hand-craft a JSON body in an MSW handler without using the
  kubb-generated factory (`createVm`, `createVmList`). If the
  OpenAPI shape changes, the handler's inline body silently drifts.
- ❌ Add a fixture to a feature module (`features/vms/mock.ts`).
  Mocks live in `mocks/`; feature modules import from there.
- ❌ Bypass `resetMockRng()` and call `faker.seed()` with a per-file
  seed. One project seed → one fleet, every reload.
- ❌ Import `db/scenario` or `db/latency` from feature code. They
  exist for MSW handlers; feature modules shouldn't need them.
- ❌ Import `db/store` from feature code. The mutation API lives for
  MSW handlers; feature modules should drive state through the
  kubb-generated mutations the handlers expose.
- ❌ Add a new fixture under `mocks/utils.ts`-style loose files at
  the top level — every fixture belongs in its bounded-context
  folder so future backend modules can lift it as-is.
