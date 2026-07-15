# Plan: Plexor.Modules.Clusters — self-hosted control plane fleet

## Goal

Implement the self-hosted **Cluster** + **Node** aggregate that the
Plexor control plane (`Plexor.Host`) registers and the worker
runtime (`Plexor.NodeAgent`) joins into. Phase 5 in the parity
matrix; the foundation for every later runtime (Compute, Database,
Storage — all of them run *on* cluster nodes).

## Why this plan is a separate worktree

`plan/clusters` worktree is isolated from the in-flight Phase 4
work in `develop` so the planning document and any exploratory
doc edits don't pollute the auth/IAM code path. The plan file
itself is a planning artifact, not code — keep it in this branch
until the implementation plan is approved, then merge to develop
as a single docs commit before any code lands.

## Architectural context (from .agents/docs/)

- **Module split**: `Plexor.Modules.Clusters` (per
  `architecture.md` "Cluster = `Plexor.Host` + nodes") — Domain,
  Application, Infrastructure, plus an Api project.
- **Data model**: `sigil.clusters` + `sigil.nodes` (or in the
  cluster schema — separate from `sigil` identity schema; see
  `architecture.md` "schema-per-module" + the migration `modules.md`
  is currently `Plexor.Modules.Clusters` per schema conventions).
- **Wire protocol** between control plane and NodeAgent
  (`architecture.md` "NodeAgent control loop", `networking.md` "mTLS
  + WireGuard" + `policies/engineering-process.md` "VSTHRD200"):
  - `POST   /api/v1/compute/clusters/join` — NodeAgent → Plexor.Host
    (mutual TLS; join token is short-lived JWT signed by the
    control plane).
  - `POST   /api/v1/compute/clusters/{clusterId}/heartbeat` —
    keepalive every 30s, with hardware snapshot.
  - `POST   /api/v1/compute/clusters/{clusterId}/commands/poll` —
    long-poll for control-plane commands (VM start/stop, app
    provider install).
  - `POST   /api/v1/compute/clusters/{clusterId}/commands/{commandId}/result` —
    NodeAgent reports command outcome.
- **Networking** (`networking.md`): WireGuard mesh between Host
  and every node; mTLS for the application layer; VXLAN overlay for
  VM-to-VM traffic. The cluster's join-tokens are derived from the
  same mTLS CA the control plane uses.
- **Status semantics** (`ui-state-machines.md`): Cluster moves
  `Pending → Provisioning → Ready → Degraded → Offline`; Node mirrors
  the lifecycle as `Pending → Heartbeating → Ready → Draining → Gone`.
  3 missed heartbeats flips Node to `Offline`; 1+ node offline
  flips Cluster to `Degraded`.

## Aggregate shape

### `Cluster` (Plexor.Modules.Clusters.Domain.Entities.Cluster)

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` (UUID v7) | Sortable by creation. |
| `Name` | `string` | Unique per org. |
| `OrgId` | `Guid` | Tenant scope. |
| `Region` | `string` (e.g. "eu-central-1") | Operator-assigned; helps the dashboard group. |
| `Status` | enum `ClusterStatus` | `Pending / Provisioning / Ready / Degraded / Offline`. |
| `WireguardPublicKey` | `string` | Optional, populated once the host's WG key is set. |
| `JoinTokenExpiresAt` | `DateTimeOffset?` | Set on each new node admission; rotated when expired. |
| `CreatedAt` / `UpdatedAt` | `DateTimeOffset` | `ICreatedAt` / `IUpdatedAt`. |
| `Nodes` | `IReadOnlyCollection<Node>` | Children. |

Invariants:
- `Name` unique per `(OrgId, Name)`.
- `Status` transitions follow `ui-state-machines.md`; throw on illegal.
- `Offline` only from `Ready` or `Degraded` (cluster went away).

### `Node` (Plexor.Modules.Clusters.Domain.Entities.Node)

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` (UUID v7) | Surrogate. |
| `ClusterId` | `Guid` | FK to cluster. |
| `OrgId` | `Guid` | Denormalized for org-scoped queries. |
| `Hostname` | `string` | OS-reported, operator-verifiable. |
| `Role` | enum `NodeRole` | `ControlPlane / Worker / Storage / Mixed`. |
| `Status` | enum `NodeStatus` | `Pending / Heartbeating / Ready / Draining / Gone`. |
| `Hardware` | `NodeHardware` (value object) | CPU cores, RAM bytes, disk bytes, arch. |
| `LastHeartbeatAt` | `DateTimeOffset?` | Stamped on every `/heartbeat` call. |
| `CreatedAt` / `UpdatedAt` | `DateTimeOffset` | |
| `WireguardPublicKey` | `string` | Set during the WG handshake. |

Invariants:
- `Hostname` unique per `ClusterId`.
- `LastHeartbeatAt < now - 90s` ⇒ status `Offline` (background
  background-service job, not the request path).
- `Node` deletion is a soft-delete: `Status = Gone`, row retained
  for audit.

## Application services

| Service | Purpose |
|---|---|
| `IClusterQuery` | GET by id, list per org (paged), filter by status. |
| `ICreateClusterCommand` | Provision a new cluster: name + region + initial node role; emits `ClusterCreated` event. |
| `IUpdateClusterCommand` | Rename, change region, rotate join token. |
| `IDeleteClusterCommand` | Soft-delete: emits `ClusterDeleted` and cascades `Node.Status = Gone` on all children. |
| `INodeHeartbeatCommand` | Updates `Node.LastHeartbeatAt` + `Status = Heartbeating`; returns 401 + node-removal if cluster disabled. |
| `INodeJoinCommand` | First heartbeat from a new node; validates join token; creates the `Node` row. |
| `IPollCommandsQuery` | Long-poll for queued commands (VM start, app install). Uses `SignalR` or `Server-Sent Events` in v0.1 (separate decision). |

## Persistence

- `ClusterDbContext` (Identity-style: schema `clusters`, snake_case).
- `IEntityTypeConfiguration<Cluster>` + `IEntityTypeConfiguration<Node>`.
- Index: `(org_id, name) UNIQUE`, `(cluster_id, hostname) UNIQUE`,
  `(cluster_id, status)` for "list ready nodes per cluster".
- Migration: `tool ef migrations add Init` (per
  `ef-migrations-are-tool-generated` rule).

## REST surface (under `[Authorize]` + `[RequirePermission]`)

| Verb | Path | Permission | Notes |
|---|---|---|---|
| `POST`   | `/api/v1/compute/clusters` | `compute.clusters.create` | Body: `{name, region, initialNodeRole}`. |
| `GET`    | `/api/v1/compute/clusters` | `compute.clusters.read` | Paged; filter by `?orgId=`, `?status=`. |
| `GET`    | `/api/v1/compute/clusters/{clusterId}` | `compute.clusters.read` | Includes nodes. |
| `PATCH`  | `/api/v1/compute/clusters/{clusterId}` | `compute.clusters.update` | Rename / region. |
| `DELETE` | `/api/v1/compute/clusters/{clusterId}` | `compute.clusters.delete` | Soft-delete. |
| `GET`    | `/api/v1/compute/clusters/{clusterId}/nodes` | `compute.nodes.read` | List nodes per cluster. |
| `POST`   | `/api/v1/compute/clusters/join` | anonymous (mTLS) | NodeAgent first call. |
| `POST`   | `/api/v1/compute/clusters/{clusterId}/heartbeat` | node token | Keepalive. |
| `POST`   | `/api/v1/compute/clusters/{clusterId}/commands/poll` | node token | Long-poll for commands. |
| `POST`   | `/api/v1/compute/clusters/{clusterId}/commands/{commandId}/result` | node token | Report outcome. |

## Cross-cutting

- `IDomainEvent` + `INotificationHandler<T>` for `ClusterCreated`,
  `ClusterDeleted`, `NodeJoined`, `NodeLeft`, `NodeOffline` (via
  the existing kernel `IIntegrationEvent` port).
- All endpoints write to `atlas` audit log (out-of-scope for v0.1 —
  defer to Phase 5+ when `atlas` module lands; for now a
  structured `ILogger.LogInformation` substitute).
- OpenAPI document transformer (Phase 3.6) auto-emits 401/403/500
  PD responses; per-endpoint `[ProducesResponseType<T>]` only for 2xx.

## NodeAgent surface (consumer contract)

```
record NodeRegistrationRequest(
    string JoinToken,
    string Hostname,
    NodeRole Role,
    NodeHardware Hardware);

record NodeRegistrationResponse(
    Guid NodeId,
    Guid ClusterId,
    string ControlPlaneUrl,
    string WireguardConfig);
```

`ControlPlaneUrl` is the post-`/join` rendezvous point (mTLS +
WireGuard handshake URL); `WireguardConfig` is a base64-encoded
`wg-quick.conf` blob. Both are part of the response — the NodeAgent
onboards with these on first call.

## Build order

1. `Plexor.Modules.Clusters.Domain` — aggregates + value objects +
   domain events.
2. `ClusterDbContext` + `IEntityTypeConfiguration<>` + tool-generated
   migration `Init`.
3. `Plexor.Modules.Clusters.Application` — abstractions (commands /
   queries) + handlers as POCOs.
4. `Plexor.Modules.Clusters.Infrastructure` — EF handlers + DbContext
   + `IServiceCollection` extension.
5. `Plexor.Modules.Clusters.Api` — controllers + DI wiring.
6. `Plexor.Host` updates: `AddClustersCore()`, apply migration via
   `Plexor.Migrator` integration, expose the new endpoints under
   `/api/v1/compute/*`.
7. Tests: aggregate (domain invariants), handler (mocked DbContext
   in-memory), controller (WebApplicationFactory + PostgreSQL
   Testcontainer).
8. `Plexor.NodeAgent` updates: refactor existing
   `HeartbeatLoop` to call the new join + heartbeat endpoints
   (was: in-memory registry in `Plexor.Host`).

## Out of scope (Phase 5+ or later)

- WireGuard mesh automation (operators install `wg-quick` manually
  for v0.1; the join response only carries the config blob).
- Multi-region clusters (single region per cluster for v0.1).
- Cluster upgrade / migration flow (K3s upgrade, Talos image
  rotation — Phase 6+ with the `k8s` app provider).
- SLO dashboards (Prometheus + Grafana — handled by the
  observability app provider in Phase 7+).

## Acceptance

- `dotnet build plexor.slnx -c Debug` clean; 50+ new tests (handler
  + aggregate + controller); migration applied to local
  Postgres; new endpoints documented in `artifacts/openapi.json`.
- `plexor-migrator run` on a fresh DB: applies cluster migration,
  no errors.
- `Plexor.NodeAgent` (the existing in-progress worker) reaches
  `Ready` state against a real Plexor.Host in a smoke test.
- `[RequirePermission]` checks on every cluster endpoint; OpenAPI
  transformer shows 401/403/500 for unauthenticated/unauthorised
  callers.

## UI integration

The Plexor console (`web/apps/console`, TanStack Router SPA)
already has the shape for the cluster list screen — see
`.agents/docs/ui/screens/00-clusters.md` (designer brief) and
`.agents/docs/ui/ui-state-machines.md` (Cluster status badges:
`Pending / Provisioning / Ready / Degraded / Offline` — same
vocabulary as this plan's `ClusterStatus` enum, so the screen
maps 1:1 to the API).

### Routes to add / update

| Path | Purpose | Backed by |
|---|---|---|
| `web/apps/console/src/routes/clusters.tsx` | Cluster list (already drafted) | `GET /api/v1/compute/clusters?orgId=...` |
| `web/apps/console/src/routes/clusters.$clusterId.tsx` | Cluster detail with embedded node list | `GET /api/v1/compute/clusters/{id}` |
| `web/apps/console/src/routes/clusters.new.tsx` | Create-cluster wizard (name + region + initial node role) | `POST /api/v1/compute/clusters` |
| `web/apps/console/src/routes/clusters.$clusterId.join.tsx` | One-time landing page for NodeAgent's `plx node join` flow (renders the join token + WireGuard config blob as a copyable code block) | `POST /api/v1/compute/clusters/{id}/rotate-join-token` |

### Data hooks

One TanStack-Query-style hook per resource, all under
`web/apps/console/src/features/clusters/`:

- `useClusters(orgId)` — list, paged
- `useCluster(clusterId)` — single + child nodes
- `useCreateCluster()` — mutation
- `useJoinToken(clusterId)` — one-shot query, no caching

### Status badge mapping (one source of truth)

Reuse the existing `StatusPill` primitive
(`web/apps/console/src/shared/ui/primitives/status-pill.tsx`) with a
per-status colour map:

| `ClusterStatus` | Tone |
|---|---|
| `Pending` | `idle` |
| `Provisioning` | `info` (spinner overlay) |
| `Ready` | `success` |
| `Degraded` | `warn` |
| `Offline` | `err` |

The API → UI mapping is a const literal at the top of
`features/clusters/status.tsx`; both the Cluster list row and the
Cluster detail header read from it. This keeps badge colour
consistent across screens and is the only place a Cluster status
gets translated to a tone.

### Out of UI scope (Phase 5+)

- K8s console (`/k8s-clusters/*`) — separate `plan/k8s` worktree
  has its own UI integration section.
- Real-time cluster-event stream (WebSocket / SSE) — Phase 7+
  when the observability app provider lands; until then the
  cluster list refreshes on a 30 s polling interval.
- Dark-mode palette override for `Degraded` / `Offline` — covered
  by the design system tokens; no per-screen work.
