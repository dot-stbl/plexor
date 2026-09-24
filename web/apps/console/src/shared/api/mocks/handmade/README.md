# Handmade mocks — endpoints not yet in the OpenAPI contract

This directory holds **page data for endpoints that do NOT exist in
`contracts/plexor.openapi.yaml` yet**. Each module is a clearly-marked
stopgap with a `TODO(contract)` header naming the endpoint(s) to add.

## The two mock homes

| Location | What lives there | Backed by |
|---|---|---|
| `../handlers.ts` + `../../../api/src` (kubb) | Mock data for endpoints **in the contract** — generated handler factories + faker fixtures, composed in the single wiring file | `contracts/plexor.openapi.yaml` |
| `src/mocks/` | Hand-curated data **feeding contract-endpoint handlers** (the VM fleet, audit entries), the launcher SUMMARY cards, and the shared dev test session | the contract (via handlers.ts) |
| **this directory** | Page data for endpoints **missing from the contract** (clusters, k8s, lxc, databases today) | nothing — awaiting contract growth |

## Convention

1. Every module starts with a `TODO(contract): add <method path> to
   contracts/plexor.openapi.yaml` header.
2. Data is synchronous module-level state (no fetch, no MSW) — features
   import it through their usual hooks, so dev:mock behavior is identical
   to before the move.
3. Types stay in the owning domain (`@/domains/<context>` barrel) — the
   module imports them, same as `src/mocks/` does.
4. When the endpoint lands in the contract: regenerate kubb output, add a
   wiring line in `../handlers.ts`, add a smoke-test line in
   `../../../api/src/msw/msw.test.ts`, migrate the feature to the
   generated TanStack Query hooks, and delete the module here. The
   module here is usually just an `export *` shim pointing at the real
   fixture body in `mocks/<context>/`, so the contract-migration work
   is to delete BOTH the shim here and the fixture body there — or to
   move the fixture body to feed the new kubb handler instead, per the
   `mocks/README.md` convention.

## Current modules

| Module | Feeds | Pending contract endpoints |
|---|---|---|
| `clusters.ts` | clusters list/detail pages (`domains/fleet` → `use-clusters.ts`) | `GET /api/v1/compute/clusters` (+ detail, nodes, join tokens) |
| `k8s.ts` | k8s page (`domains/catalog` barrel) | `GET /api/v1/k8s/clusters` |
| `lxc.ts` | lxc page (`domains/compute` barrel) | `GET /api/v1/lxc/containers` |
| `networks.ts` | networks page (`domains/network` → `Network` / `NetworkStatus` / `listNetworks()`) | `GET /api/v1/networks` |
| `databases.ts` | managed data services (`domains/catalog` → `use-databases.ts`) | `GET /api/v1/data/engines`, `GET /api/v1/data/clusters`, `GET /api/v1/data/runtime-hosts` (paths TBD) |
| `billing.ts` | billing page (`domains/billing` → billing snapshot types, `getBillingSnapshot()`) | none — read-only usage projection, see `billing.ts`'s own header comment |
