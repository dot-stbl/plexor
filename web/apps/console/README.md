# Plexor Portal (Console)

Vite + React 19 + TanStack (Router + Query) SPA — the operator-facing UI for
Plexor. Lives at `web/apps/console` in the monorepo.

## Setup

From the monorepo root (`web/`):

```bash
bun install
bun run dev                # real backend at VITE_API_BASE_URL
```

## Mock mode

When the Plexor.Host backend is not yet available (or you want to demo UI
without a running host), boot the SPA with MSW intercepting every API call.

```bash
bun run dev:mock           # alias: VITE_USE_MOCKS=true vite
bun run build:mock         # production build with mocks baked in
bun run test:mocks         # run vitest with VITE_USE_MOCKS=true
```

`main.tsx` reads `import.meta.env.VITE_USE_MOCKS` and starts the MSW worker
from `src/shared/api/mocks/browser.ts` before first render. The worker
serves canned responses from `src/shared/api/src/fixtures/` (Faker-generated,
deterministic via `faker.seed(1337)`) and falls through to the real network
for non-API requests (Vite dev server, HMR, asset loads, route navigation).

**Coverage:** all 32 operations in `contracts/plexor.openapi.yaml` are
handled — 29 from kubb-generated factories + 3 hand-mirrored for
`/branding/theme` (kubb 4.39.2 silently skipped these in the MSW + faker
pass; see `msw/getBrandingThemeHandler.ts` for the kz note). The
`msw.test.ts` smoke test guards against missing handlers — add a line
there when you add an admin page that hits a new endpoint.

**Default fixture data:**

| Endpoint | What you see |
|----------|---------------|
| `GET /vms` | 8 hand-curated VMs with realistic mix of statuses (running, stopped, error, provisioning) |
| `GET /vms/:vmId` | The hand-curated entry matching that id; kubb's random uuid fallback if id is unknown |
| `GET /quotas/definitions` | 6 kubb-generated definition rows |
| `GET /quotas/{assignments,usage,effective}` | 6–8 kubb-generated rows |
| `GET /branding/{global,boot,org/{orgId},theme}` | kubb-generated defaults |
| `GET /audit` | 12 kubb-generated audit entries |
| `GET /iam/orgs/{orgId}/auth-provider` | kubb-generated provider config |

**Smoke test:**

```bash
cd web
bun run test -- src/shared/api/src/msw
# 15 tests, ~100ms — exercises the admin-page mount surface
```

The test boots `setupServer` from `msw/node` and fires real `fetch()`
calls against each endpoint, asserting 200 + the response shape. Adding
a new admin page? Add a test line here that hits its mount endpoint —
the assertion is the canary that the dev:mock worker covers the page.

## Scripts

| Script | What it does |
|--------|--------------|
| `bun run dev` | Vite dev server, real backend at `VITE_API_BASE_URL` |
| `bun run dev:mock` | Vite dev server, MSW intercepts all 32 endpoints |
| `bun run build` | Production build (real backend) |
| `bun run build:mock` | Production build with mocks baked in |
| `bun run test` | `vitest run` — all unit + smoke tests |
| `bun run test:mocks` | `vitest run` with `VITE_USE_MOCKS=true` (forces mock-aware tests) |
| `bun run test:e2e` | Playwright end-to-end tests |
| `bun run lint` | `eslint . --max-warnings 0` |
| `bun run typecheck` | `tsc --noEmit` |

## Adding a new endpoint

1. Add the path + operation to `contracts/plexor.openapi.yaml`.
2. From `web/tooling/codegen/`: `bun run generate` — this regenerates
   the kubb factories (types, client, hooks, MSW handlers, fixtures).
3. Add a wiring line in `src/shared/api/mocks/handlers.ts` for the new
   handler + fixture (e.g. `getNewThingHandler(createNewThing200())`).
4. Add a smoke-test line in `src/shared/api/src/msw/msw.test.ts`.
5. If kubb skipped the endpoint in the MSW + faker pass (see the
   `/branding/theme` kz note), hand-mirror the handler + fixture and
   document the gap.

The OpenAPI contract is the single source of truth for both the kubb
regen and the MSW handlers — adding an endpoint requires updating both.
