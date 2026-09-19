# Mock fixtures — shared source of truth

The `web/apps/console/src/mocks/` directory is the **single source of
truth for mock data** that the MSW handlers, the launcher SUMMARY
cards, and component tests all consume.

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
| `utils.ts` | `MOCK_SEED`, `MOCK_TIMESTAMP`, `resetMockRng()` | Imported by every other file in this directory; also handy for tests that need stable fake ids. |
| `vms.ts` | `FLEET`, `FLEET_BY_ID`, `makeVmList()`, `countByStatus()` | MSW handler for `GET /vms`, VM list page tests, launcher SUMMARY card. |
| `clusters.ts` | `CLUSTERS`, `CLUSTER_BY_ID`, `clusterSummary()` | MSW handler for cluster list endpoints (when they land), cluster page tests, launcher SUMMARY card. |
| `branding.ts` | `GLOBAL_BRANDING`, `ORG_BRANDING`, `getGlobalBranding()`, `getOrgBranding()` | MSW handlers for branding endpoints, admin/branding page tests. |
| `auth.ts` | `DEV_SESSION` | Tests that seed an authenticated session before rendering a protected route. |
| `audit.ts` | `makeAuditEntries(count)` | MSW handler for `GET /audit`, admin/audit page tests, launcher SUMMARY card. |
| `launcher-summary.ts` | `makeLauncherSummary()` | The launcher SUMMARY cards (instead of the previous static `—` placeholder). |
| `index.ts` | Barrel re-export | One import path for the whole convention. |

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
