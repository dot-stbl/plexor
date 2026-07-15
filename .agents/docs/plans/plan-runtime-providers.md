# Plan: Plexor runtime providers — workload deployment on cluster nodes

## Goal

Define the **runtime abstraction layer** that lets Plexor deploy
workloads (Postgres, Redis, nginx, app deploys) onto its cluster
nodes. Three concrete runtime implementations ship in v0.1:
**Docker Compose** (single-host), **k3s** (multi-node), and
**Podman / Quadlet** (RHEL / rootless). All share one interface so
app-providers don't need to know which runtime the cluster is
using.

## Why this plan is a separate worktree

`plan/runtime-providers` worktree is downstream of
`plan/clusters` (the join / heartbeat / mTLS wiring) and
`plan/k8s` (the K3s cluster provisioning). It depends on the
Cluster module for node discovery but is a separate axis:
- Clusters plan = identity / wire protocol between control plane
  and nodes
- k8s plan = provisioning a managed K3s cluster on a cluster
- **providers plan** = once a cluster exists, how do workloads
  get scheduled / deployed onto its nodes

A separate worktree keeps the document focused and lets the user
review each axis independently. Implementation lands after both
cluster + k8s PRs are merged.

## Architectural context (from .agents/docs)

- **Provider model** (`providers.md`): Plexor has two kinds —
  **install providers** (built-in, run on first install via
  `plx init`) and **app providers** (community-shipped, install via
  `plx provider install`). Workload deployment falls into the
  app-provider category — postgres, redis, nginx etc. all install
  on top of a chosen runtime.
- **Hard constraint** (`providers.md`): "❌ Stateful multi-instance
  orchestration (kubernetes operators) — keep it simple
  shell-based." → No K8s-style controller / reconciliation
  loop. Each runtime implementation is **one-shot** deployment
  (docker compose up, kubectl apply, podman play kube).
- **App-provider executor** (`architecture.md`): NodeAgent receives
  NATS event `provider.install` with shell-commands, runs them
  on the worker node, reports the result.
- **State model** (`ui-state-machines.md`): workloads move
  `Pending → Provisioning → Ready → Degraded → Offline`, mirroring
  the same lifecycle we use for cluster Nodes. Operators expect
  the same colour palette.

## The abstraction

```csharp
public interface IWorkloadRuntime
{
    /// <summary>Identifier of the runtime — "docker-compose",
    /// "k3s", "podman-quadlet", etc. Used by the cluster
    /// spec to declare which runtime the cluster runs.</summary>
    public string RuntimeId { get; }

    /// <summary>Render the provider's high-level intent into a
    /// native manifest (compose.yaml / kustomization.yaml /
    /// quadlet .container file). The result is stored on
    /// disk and re-applied on drift.</summary>
    public Task<string> RenderManifestAsync(
        WorkloadSpec spec,
        CancellationToken cancellationToken = default);

    /// <summary>One-shot deploy: writes the manifest, calls the
    /// runtime API (docker compose up / kubectl apply / podman
    /// quadlet reload), and returns the assigned manifest path
    /// on the target node.</summary>
    public Task<DeploymentResult> DeployAsync(
        WorkloadSpec spec,
        string targetNodeId,
        CancellationToken cancellationToken = default);

    /// <summary>Read the current deployment state from the runtime.
    /// Used by the health-check loop to drive status transitions
    /// (Pending → Provisioning → Ready → Degraded → Offline).</summary>
    public Task<WorkloadState> ReadStateAsync(
        WorkloadManifestReference reference,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Portable workload spec — app-provider-friendly shape that
/// each runtime translates into its native manifest format.
/// </summary>
public sealed record WorkloadSpec(
    string Name,
    string Image,
    IReadOnlyList<int> Ports,
    IReadOnlyDictionary<string, string> Environment,
    IReadOnlyList<string> Volumes,
    string? Namespace);
```

App-providers (Postgres app provider, Nginx app provider, custom
app deploys) write a `WorkloadSpec`. The runtime implementation
translates it to whatever its API expects. App-providers **never**
see docker-compose YAML, kustomization YAML, or quadlet files
directly.

## Runtime implementations (v0.1)

### Docker Compose (`DockerComposeWorkloadRuntime`)

- Native format: `docker-compose.yaml`.
- Deploy: writes to `/var/lib/plexor/workloads/<name>/compose.yaml`
  on the target node via NodeAgent's existing SSH / mTLS channel,
  then `docker compose -f <path> up -d`.
- State: `docker compose ps --format json` parsed into
  `WorkloadState`.
- Limits: single-host only (no swarm mode). Single-container
  workloads per stack; multi-container stacks require the
  app-provider to compose the spec correctly.

### k3s (`K3sWorkloadRuntime`)

- Native format: kustomize directory (kustomization.yaml +
  deployment.yaml + service.yaml + configmap.yaml).
- Deploy: writes the directory to `/var/lib/plexor/workloads/<ns>/<name>/`
  on the target node, then `kubectl --kubeconfig=/etc/rancher/k3s/k3s.yaml apply -k <path>`.
- State: `kubectl --kubeconfig=… get deploy/<name> -n <ns> -o json` parsed
  into `WorkloadState`.
- Limits: requires a node where `k3s` is installed (see
  `plan/k8s` worktree). Falls back to Docker Compose if the
  target node has no k3s — runtime is **per cluster**, not
  global.

### Podman Quadlet (`PodmanQuadletWorkloadRuntime`)

- Native format: `<name>.container` quadlet file (INI syntax).
- Deploy: writes the file to `/etc/containers/systemd/<name>.container`
  on the target node, then `systemctl daemon-reload && systemctl
  start <name>.service`.
- State: `systemctl is-active <name>` parsed into `WorkloadState`.
- Limits: requires systemd-managed podman; runs rootless by default.
  Single-container per quadlet — multi-container requires
  composing multiple `<name>.container` units.

## Plumbing

- **`Plexor.Shared.Workloads`** — shared kernel module that holds
  the `IWorkloadRuntime` interface + `WorkloadSpec` value object.
  Lives next to `Plexor.Shared.Authorization`, `Plexor.Shared.Persistence`,
  etc. — every module needs it; nothing module-specific.
- **`Plexor.NodeAgent.Plugins.ContainerRuntime`** — the NodeAgent
  side. Holds the three runtime implementations + an
  `IRuntimeSelector` that picks the runtime per cluster based on
  the cluster spec's `runtime: docker-compose | k3s | podman`
  field.
- **`Plexor.Modules.Providers.Runtime`** — control-plane side.
  Wraps the NATS event publication + status feedback loop. No
  business logic — just message routing.

## Cluster integration

`Cluster` aggregate grows one field:

```csharp
public string RuntimeId { get; init; } = "docker-compose";
```

`CreateClusterCommand` accepts `runtime: docker-compose | k3s | podman`
as an input. The selected runtime plugin is then the only one that
can deploy workloads on that cluster — there's no fallback chain.
An operator who wants k3s workloads provisions a cluster with
`runtime: k3s`; switching runtime requires tearing down the
cluster and recreating it.

## State machine (workload)

```
   ┌──────────┐
   │ Pending  │  manifest rendered, not yet on node
   └────┬─────┘
        │ DeployAsync
        ▼
  ┌──────────────┐
  │ Provisioning│  manifest written + runtime API called
  └────┬─────────┘
   success     failure
       │         │
       ▼         ▼
  ┌────────┐  ┌────────┐
  │ Ready │  │ Failed │
  └────┬─┘  └────────┘
       │ StopAsync / DeleteAsync
       ▼
  ┌─────────┐
  │ Offline│
  └─────────┘
```

`WorkloadState` is a discriminated result:
`Pending | Provisioning | Ready | Degraded | Offline` with
runtime-specific details (e.g. k3s reports `replicas: 3/3`,
docker-compose reports `Status: Up (healthy)`).

## Drift detection

Each `IWorkloadRuntime.ReadStateAsync` is polled every 60 s by a
background `ScheduledWorkerBase` in Plexor.NodeAgent (reusing the
Phase 4 `Plexor.Migrator` pattern — no new host infrastructure).
Drift (state changed from `Ready` → `Degraded` / `Offline`) emits
a `WorkloadDriftDetected` event to NATS, which the Plexor.Modules.Providers
control-plane handler turns into a 200 / 4xx response on
`GET /api/v1/workloads/{id}/status`.

## REST surface (control plane)

| Verb | Path | Permission | Notes |
|---|---|---|---|
| `POST`   | `/api/v1/clusters/{id}/workloads` | `workloads.deploy` | Body: `WorkloadSpec`. |
| `GET`    | `/api/v1/clusters/{id}/workloads` | `workloads.read` | Paged. |
| `GET`    | `/api/v1/clusters/{id}/workloads/{workloadId}` | `workloads.read` | Includes state + last drift event. |
| `DELETE` | `/api/v1/clusters/{id}/workloads/{workloadId}` | `workloads.delete` | `docker compose down` / `kubectl delete` / `systemctl stop`. |

Built-in app providers (Postgres, Redis, nginx) are not in this
plan — they're separate `plan/postgres-provider` / `plan/redis-provider`
worktrees that wrap `IWorkloadRuntime` with their app-specific
defaults.

## Build order

1. `Plexor.Shared.Workloads` — `IWorkloadRuntime` interface +
   `WorkloadSpec` value object + `WorkloadState` discriminated
   result.
2. `DockerComposeWorkloadRuntime` — first implementation; it has
   the simplest wire format and the NodeAgent already shells out
   via SSH.
3. NodeAgent plugin registry: `Plexor.NodeAgent.Plugins.ContainerRuntime`
   + `IRuntimeSelector` + the 60 s drift-detection background job.
4. `Plexor.Modules.Providers.Runtime` — control-plane side; wires
   NATS publication + status feedback.
5. REST endpoints under `/api/v1/clusters/{id}/workloads/*`.
6. `K3sWorkloadRuntime` + `PodmanQuadletWorkloadRuntime` —
   second/third implementations.
7. Tests:
   - Unit: `WorkloadSpec` translation tests for each runtime
     (snapshot the rendered YAML / quadlet / kustomize, assert
     shape).
   - Integration: `docker compose up` smoke in a test container
     against `k3s/k3s:latest`.
   - NodeAgent plugin: `IRuntimeSelector` round-trip via in-memory
     NATS stub.

## Acceptance

- `dotnet build plexor.slnx -c Debug` clean; 30+ new tests.
- `POST /api/v1/clusters/{id}/workloads` with a `WorkloadSpec` for
  an Nginx image successfully deploys via Docker Compose, k3s,
  and Podman Quadlet (smoke tested across all three against
  test containers).
- Drift detection flips `WorkloadState` from `Ready` →
  `Degraded` within 90 s when a container is killed outside
  Plexor's knowledge.
- OpenAPI document auto-emits 401/403/500; `[ProducesResponseType]`
  on each endpoint for 2xx shapes.

## Out of scope (Phase 7+ or later)

- **Docker Swarm** — deprecated; k3s superset. Skip.
- **Helm provider** — Phase 7+ (composes k3s + Helm chart rendering
  on top of `K3sWorkloadRuntime`).
- **Cross-cluster workload distribution** — Phase 7+ (single
  cluster only in v0.1).
- **Reconciliation loops** — explicitly **not** in scope per
  `providers.md`. Drift detection fires events; operators (or
  Phase 7+ controllers) decide whether to remediate.
