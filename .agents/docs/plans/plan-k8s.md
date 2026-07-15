# Plan: Plexor.Modules.Compute.K8s — managed Kubernetes as an app provider

## Goal

Implement **Managed Kubernetes** as a Plexor app provider —
self-hosted K3s + Talos OS + Cilium CNI, exposed at
`/api/v1/compute/k8s-clusters/*` for operators who want the
"Managed Kubernetes" button without a third-party cloud. Phase 6
in the parity matrix; closes the gap to YC's "Managed Kubernetes"
within Plexor's self-hosted story.

## Why this plan is a separate worktree

`plan/k8s` worktree keeps the K3s/Talos design isolated from the
Clusters plan (`plan/clusters`) and from the in-flight Phase 4
identity work in `develop`. K3s orchestration has a different
lifecycle (cluster bootstrap, image rotation, app deployment)
than the cluster/node registration work — different plan, different
test containers, different review context. Keeping them in separate
worktrees means neither plan blocks the other.

## Architectural context (from .agents/docs)

- **Provider model** (`providers.md`): Plexor has two provider
  kinds — **install providers** (built-in, run on first install
  via `plx init`) and **app providers** (community-shipped, install
  via `plx provider install`). Managed Kubernetes is the
  **k8s-cluster** app provider.
- **App provider executor** (`architecture.md`): "NodeAgent receives
  NATS event `provider.install` with shell-commands, runs them on
  its node. Each provider decides on which nodes to run (labels /
  affinity)."
- **Hard constraint** (`providers.md`): "❌ Stateful multi-instance
  orchestration (kubernetes operators) — keep it simple
  shell-based." → K3s bootstrap is a **shell** command (k3s
  install script), **not** a kubernetes operator.
- **YC parity target** (`yandex-cloud-parity.md`): 4 weeks, K3s +
  Talos OS.

## Component choice (and why)

| Layer | Choice | Rationale |
|---|---|---|
| **Distribution** | K3s | Single binary, easy install (`curl -sfL https://get.k3s.io`), 5-control-plane footprint for v0.1 single-node clusters, ARM64 support. Per `yandex-cloud-parity.md` "K3s + Talos OS". |
| **OS** | Talos Linux | Immutable OS, designed for K8s, declarative, can be PXE-installed later. v0.1 ships the Talos ISOs + the K3s install — full PXE bootstrap is Phase 7+. |
| **CNI** | Cilium | eBPF-based, replaces kube-proxy. Single binary, matches Talos's network model. |
| **Ingress** | Traefik (bundled with K3s) | v0.1 default; later swappable. |
| **Storage** | Longhorn (Rook-Ceph for multi-node) | Phase 7+; v0.1 ships hostPath only on the single-node profile. |

## Module shape

`Plexor.Modules.Compute.K8s` is a separate project, NOT a feature
inside `Plexor.Modules.Clusters`. Rationale:
- Different EF DbContext (`k8s` schema, snake_case) — K8s cluster
  + node-group + app-deployment tables.
- Different install story (app-provider, not core) — installed via
  `plx provider install k8s-cluster` on a worker node, not on
  Plexor.Host.
- Different runtime dependency (K3s binary on the worker node,
  Talos API client, Cilium status endpoint).

Layered as:

```
src/modules/Plexor.Modules.Compute.K8s/
├── Plexor.Modules.Compute.K8s.Domain/         (K8sCluster, K8sNodeGroup, K8sApp)
├── Plexor.Modules.Compute.K8s.Application/    (CreateK8sCluster, ScaleNodeGroup, etc.)
├── Plexor.Modules.Compute.K8s.Infrastructure/ (K3sInstaller, TalosProvisioner, K8sDbContext)
└── Plexor.Modules.Compute.K8s.Api/            (K8sController, K8sLongPoll endpoint)
```

## Aggregate shape

### `K8sCluster` (Plexor.Modules.Compute.K8s.Domain.Entities.K8sCluster)

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` (UUID v7) | Surrogate. |
| `OrgId` | `Guid` | Tenant scope. |
| `ClusterId` | `Guid` | FK to the parent Plexor.Modules.Clusters `Cluster`. |
| `Name` | `string` | Unique per org. |
| `Status` | enum `K8sClusterStatus` | `Pending / Provisioning / Ready / Upgrading / Failed / Deleted`. |
| `K8sVersion` | `string` (semver) | "v1.29.4+k3s1" — pinned. |
| `CniPlugin` | `string` | "cilium" (Phase 5+; default for v0.1). |
| `Ingress` | `string` | "traefik" (default for v0.1). |
| `ControlPlaneEndpoint` | `string` | "https://10.0.0.1:6443" — K3s server URL exposed to NodeAgents. |
| `ServiceCidr` | `string` | "10.42.0.0/16" — service network. |
| `PodCidr` | `string` | "10.42.0.0/16" (Flannel-style) — pod network. |
| `CreatedAt` / `UpdatedAt` | `DateTimeOffset` | `ICreatedAt` / `IUpdatedAt`. |

### `K8sNodeGroup` (Plexor.Modules.Compute.K8s.Domain.Entities.K8sNodeGroup)

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` (UUID v7) | Surrogate. |
| `K8sClusterId` | `Guid` | FK. |
| `Name` | `string` (e.g. "system", "workers", "gpu") | Maps to a K3s node-label. |
| `Role` | enum `NodeGroupRole` | `ControlPlane / Worker / Storage`. |
| `TalosSchematic` | `string` | Talos schematic ID; null for non-Talos clusters (v0.2+). |
| `DesiredReplicas` | `int` | K3s cluster size for this group. |
| `ActualReplicas` | `int` | Updated on each `kubelet` heartbeat. |
| `Architecture` | enum `CpuArch` | `X64 / Arm64`. |
| `Taints` | `IReadOnlyList<NodeTaint>` | K8s taints for affinity. |
| `Labels` | `IReadOnlyDictionary<string, string>` | K8s node labels. |

### `K8sApp` (Plexor.Modules.Compute.K8s.Domain.Entities.K8sApp)

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` (UUID v7) | Surrogate. |
| `K8sClusterId` | `Guid` | FK. |
| `Name` | `string` | DNS-1123 label. |
| `Namespace` | `string` | K8s namespace. |
| `Manifest` | `string` | YAML manifest (Deployment / StatefulSet / Helm chart). |
| `Status` | enum `K8sAppStatus` | `Pending / Applied / Healthy / Degraded / Failed`. |
| `Revision` | `int` | Increments per `kubectl apply`. |

## State machine

```
                ┌──────────┐
                │ Pending  │   initial state
                └────┬─────┘
                     │ CreateK8sClusterCommand
                     ▼
              ┌──────────────┐
              │ Provisioning │   k3s install.sh + talos apply
              └────┬─────────┘
        success   │     │  failure
        ┌────────┘     └────────┐
        ▼                      ▼
   ┌────────┐              ┌────────┐
   │ Ready │              │ Failed │
   └────┬─┘              └────────┘
        │ DeleteK8sClusterCommand
        ▼
   ┌─────────┐
   │ Deleted │  (soft-delete; manifest removed from cluster)
   └─────────┘
```

`Upgrading` is a future state for K3s version bumps (Phase 7+).

## Application services

| Service | Purpose |
|---|---|
| `CreateK8sClusterCommand` | Provisions a new K3s cluster on the parent Plexor cluster. Emits `K8sClusterCreated` event. |
| `ScaleNodeGroupCommand` | Adjusts `K8sNodeGroup.DesiredReplicas`; emits `K8sNodeGroupScaled`. |
| `DeployK8sAppCommand` | `kubectl apply` the manifest. Emits `K8sAppDeployed`. |
| `GetK8sClusterQuery` | Cluster summary + node groups + apps. |
| `ListK8sClustersQuery` | Paged per org. |
| `K3sHeartbeatQuery` | Polled by NodeAgent; returns the next pending command for the node. |

## App-provider install (shell-based)

`Plexor.NodeAgent` receives a NATS event `provider.install`
with payload:
```json
{
  "provider": "k8s-cluster",
  "version": "1.0.0",
  "shell_command": "curl -sfL https://get.k3s.io | INSTALL_K3S_EXEC='--tls-san=<control-plane-ip>' sh -s -",
  "talos_schematic": "<schematic-id>",
  "env": { "K3S_TOKEN": "<join-token>" }
}
```

The NodeAgent runs the shell command, captures stdout / stderr,
and reports the result via the existing `/commands/{id}/result`
endpoint (see `plan-clusters/PLAN.md`). The shell command is the
**only** orchestration — no operator, no controller-runtime; per
`providers.md` hard constraint.

## REST surface

| Verb | Path | Permission | Notes |
|---|---|---|---|
| `POST`   | `/api/v1/compute/k8s-clusters` | `k8s.create` | Body: `{name, clusterId, k8sVersion, nodeGroups: [...]}`. |
| `GET`    | `/api/v1/compute/k8s-clusters` | `k8s.read` | Paged. |
| `GET`    | `/api/v1/compute/k8s-clusters/{k8sClusterId}` | `k8s.read` | Includes node groups + apps. |
| `PATCH`  | `/api/v1/compute/k8s-clusters/{k8sClusterId}/node-groups/{groupId}` | `k8s.scale` | Body: `{desiredReplicas}`. |
| `POST`   | `/api/v1/compute/k8s-clusters/{k8sClusterId}/apps` | `k8s.apps.deploy` | Body: `{name, namespace, manifestYAML}`. |
| `DELETE` | `/api/v1/compute/k8s-clusters/{k8sClusterId}/apps/{appId}` | `k8s.apps.delete` | `kubectl delete`. |
| `DELETE` | `/api/v1/compute/k8s-clusters/{k8sClusterId}` | `k8s.delete` | Tears down the K3s cluster (calls `k3s-uninstall.sh`). |

## Persistence

- `K8sDbContext` (schema `k8s`, snake_case) — separate from the
  `clusters` schema. `AddPlexorModuleDbContexts` already auto-discovers
  all `PlexorDbContext` subclasses.
- Index: `(org_id, name) UNIQUE`, `(k8s_cluster_id, name) UNIQUE`
  on `k8s_node_groups`, `(k8s_cluster_id, namespace, name) UNIQUE`
  on `k8s_apps`.
- Migration: `tool ef migrations add Init` (per
  `ef-migrations-are-tool-generated` rule).

## Cross-cutting

- `K3sInstaller` — `Infrastructure/Kubernetes/K3sInstaller.cs` —
  builds the `k3s install.sh` shell command from the cluster spec,
  signs it with the control-plane mTLS CA, and produces the NATS
  event payload the NodeAgent will run.
- `TalosProvisioner` — `Infrastructure/Talos/TalosProvisioner.cs` —
  generates the Talos machine config (`talosctl gen config`) and
  applies it via `talosctl apply-config`. Out of scope for v0.1
  (single-node profile) — Phase 7+ multi-node PXE bootstrap.
- `K8sHealthProbe` — `Infrastructure/K8sHealthProbe.cs` —
  background-service job that polls `kube-apiserver /healthz` and
  flips `K8sCluster.Status = Ready | Failed` based on the response.
  Reuses the existing `ScheduledWorkerBase` from `Plexor.Migrator`.

## Build order

1. `Plexor.Modules.Compute.K8s.Domain` — aggregates + value objects
   + domain events.
2. `K8sDbContext` + `IEntityTypeConfiguration<>` + tool-generated
   migration.
3. `Plexor.Modules.Compute.K8s.Application` — command / query
   abstractions.
4. `Plexor.Modules.Compute.K8s.Infrastructure` — K3s install
   command builder, Talos config generator, EF handlers, background
   health-probe.
5. `Plexor.Modules.Compute.K8s.Api` — controllers + DI wiring.
6. App provider package: `plx provider install k8s-cluster` —
   NuGet-packaged app provider for community distribution.
7. `Plexor.Host` updates: register `K8sDbContext`, expose endpoints,
   add `K8sHealthProbe` to the worker.
8. Tests: aggregate (state-machine), handler (in-memory
   DbContext), installer (K3s shell-command builder tests), app
   provider (smoke test with `k3s/k3s` Docker image via testcontainer).

## Acceptance

- `dotnet build plexor.slnx -c Debug` clean; 40+ new tests.
- Migration applied to local Postgres; `k8s.k8s_clusters`,
  `k8s.k8s_node_groups`, `k8s.k8s_apps` tables present.
- End-to-end smoke: `plx provider install k8s-cluster` runs the
  K3s install.sh command in a test container, cluster reaches
  `Ready`, app deployment via the REST endpoint succeeds.
- OpenAPI document auto-emits 401/403/500; per-endpoint
  `[ProducesResponseType<T>]` for 2xx shapes.
- `[RequirePermission("k8s.create")]` blocks unauthenticated /
  unauthorised callers; `permission:"k8s"` rolled into a new built-in
  role `k8s-admin` seeded by the Migrator (alongside `admin` and
  `viewer`).

## UI integration

The K8s console is a child surface of the Cluster screen — when
the operator opens a Cluster, a new "Kubernetes" tab reveals the
K3s cluster state. Same TanStack-Query patterns as the parent
cluster surface (see `plan-clusters/PLAN.md` §UI integration for
the data-hook shape).

### Routes to add / update

| Path | Purpose | Backed by |
|---|---|---|
| `web/apps/console/src/routes/clusters.$clusterId.kubernetes.tsx` | K8s cluster status + node-group replicas (live polling 30 s) | `GET /api/v1/compute/k8s-clusters/{id}` |
| `web/apps/console/src/routes/clusters.$clusterId.kubernetes.apps.tsx` | Apps inside the K8s cluster (table with name / namespace / status) | `GET /api/v1/compute/k8s-clusters/{id}/apps` (per plan REST surface) |
| `web/apps/console/src/routes/clusters.$clusterId.kubernetes.apps.$appId.tsx` | Single-app detail (manifest YAML viewer + revision history) | `GET /api/v1/compute/k8s-clusters/{id}/apps/{appId}` |
| `web/apps/console/src/routes/clusters.$clusterId.kubernetes.new.tsx` | Manifest editor (Monaco editor + `kubectl diff` preview) | `POST /api/v1/compute/k8s-clusters/{id}/apps` |

### Data hooks

`web/apps/console/src/features/k8s/`:

- `useK8sCluster(k8sClusterId)` — status + node groups
- `useK8sApps(k8sClusterId)` — list of apps with revision count
- `useDeployK8sApp(k8sClusterId)` — mutation; surfaces the
  revision number on success
- `useK8sClusterEvents(k8sClusterId)` — long-poll endpoint (Phase 5+
  deferred to Phase 7+ when the observability stack lands; v0.1
  uses a 30 s `setInterval` re-fetch instead).

### Status badge mapping (K8s-specific)

`features/k8s/status.tsx`:

| `K8sClusterStatus` | Tone |
|---|---|
| `Pending` | `idle` |
| `Provisioning` | `info` (spinner overlay) |
| `Ready` | `success` |
| `Upgrading` | `info` (spinner) |
| `Failed` | `err` |
| `Deleted` | `idle` (greyed out) |

| `K8sAppStatus` | Tone |
|---|---|
| `Pending` | `idle` |
| `Applied` | `info` (briefly, then `success` once Healthy) |
| `Healthy` | `success` |
| `Degraded` | `warn` |
| `Failed` | `err` |

The Cluster detail tab + the App list use the same
`StatusPill` primitive; the colour map is the only place these
translations live.

### App-provider branding

The `k8s-cluster` app provider ships its own icon
(`K8sClusterIcon` in `web/apps/console/src/shared/ui/tech-icon-data.ts`)
that the cluster tab's header picks up automatically (the existing
tech-icon system resolves icons from the provider name in the
`Provider` enum).

### Out of UI scope (Phase 5+)

- Manifest-graph viewer (relationships between Deployments /
  Services / Ingresses) — defer to Phase 7+ when the observability
  app provider lands; for v0.1 the single-app detail page renders
  the raw YAML only.
- Real-time pod logs streaming — same Phase 7+ dependency.
- Helm chart editor — Phase 7+ once the Manifest editor matures.

## Out of scope (Phase 7+)

- Multi-region K3s HA clusters (Phase 7+; v0.1 is single-region,
  single control plane).
- K3s version upgrade flow (`K8sCluster.Status = Upgrading`).
- Longhorn / Rook-Ceph storage (Phase 7+; v0.1 is hostPath).
- GPU node groups (K8sDevicePlugin + nvidia runtime).
- Service mesh (Linkerd / Istio).
- Cert-manager integration (Phase 7+ when `Plexor.Modules.Network`
  has a cert authority).
