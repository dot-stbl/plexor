---
phase: 6
plan: runtime-providers
title: "Plexor runtime providers — app workloads via Docker / Podman / k3s"
status: ready
started: 2026-07-17T00:00:00Z
duration_estimate: ~12h
tags: [workloads, runtime, docker-compose, podman-quadlet, k3s, postgres-listen]
key-decisions:
  Q1_workload_model: "single-workload-discriminated"
  Q2_transport: "postgres-listen-notify"
  Q3_scope: "all-three-runtimes"
  Q4_runtime_selection: "cluster-level-runtime-id"
supersedes: ".agents/docs/plans/plan-runtime-providers.md"
---

# Plexor runtime providers — app workloads via Docker / Podman / k3s

## Goal

Extend the existing `Workload` aggregate (Phase D, Tiers 1-5) with a new
**kind** of workload: app workloads deployed via a chosen **runtime provider**.
Three runtime implementations ship in v0.1: Docker Compose, Podman Quadlet, and
k3s. All share one `IWorkloadRuntime` interface so app providers don't need to
know which runtime the cluster runs.

The existing Phase D Workload (VM/LXC/QEMU via libvirt) stays intact —
`Workload.Kind` becomes a discriminated union and app workloads are a new
subclass of the same aggregate. **One table, one controller, one URL**
namespace, discriminated by `Kind`.

## Decisions (locked in pre-plan discussion)

| # | Question | Decision |
|---|---|---|
| Q1 | Workload model | **Single Workload table (discriminated)** — Kind = Vm \| Lxc \| Qemu \| DockerCompose \| K3s \| Podman; one controller, one endpoint. |
| Q2 | Command transport | **Postgres LISTEN/NOTIFY** + 30s polling safety net. Sub-100ms latency, no new infra. |
| Q3 | v0.1 scope | **All three runtimes**: Docker Compose + Podman Quadlet + k3s. |
| Q4 | Runtime selection | **Cluster-level `RuntimeId`** field. Immutable at create-time. |

## Architectural context

- **Phase D Tier 1-5** is complete: `Workload` aggregate, CRUD endpoints,
  heartbeat-driven drift detection, action endpoints with per-node command
  queue (`forge.commands`). Tier 4-5 polling is at 5s latency today.
- **Compute stack (Tiers 3.2-3.5)** is in place: `IImageRegistry`,
  `IVolumeBackend`, `INetworkBackend`, `LibvirtKvmProvider`,
  `LibvirtLxcProvider`, `LibvirtQemuProvider`. App workloads reuse
  these abstractions — `DockerComposeWorkloadRuntime` doesn't call libvirt,
  but the underlying compute provider (libvirt) still runs the host
  kernel that docker-compose / podman / k3s runs on.
- **State machine**: app workloads reuse Phase D's
  `Pending → Provisioning → Ready → Degraded → Offline`. Drift detection
  is the same `WorkloadReport` pattern.

## Files this plan touches

| Layer | New / changed |
|---|---|
| `Plexor.Modules.Clusters.Domain` | + `WorkloadKind` enum (extended) |
| `Plexor.Modules.Clusters.Application` | + `WorkloadSpec` tagged-union + `IWorkloadRuntime` interface + `WorkloadSpecMapper` |
| `Plexor.Modules.Clusters.Infrastructure` | + `Cluster.RuntimeId` field + migration + 3 runtime implementations |
| `Plexor.Modules.Clusters.Api` | + `WorkloadsController` accepts tagged-union spec; + 3 new action endpoints (`compose-up`, `compose-down`, `quadlet-start`, ...) |
| `Plexor.NodeAgent` | + `PostgresListenCommandSource` (LISTEN/NOTIFY wake-up) + `RuntimeDispatch` (3 runtime impls as `ICommandExecutor`) + drift loop reuses Tier 4 |
| `Plexor.Modules.Clusters.Unit` | + unit tests for all 3 runtime impls (snapshot rendering) |
| `Plexor.NodeAgent.Unit` | + tests for `PostgresListenCommandSource` + 3 executor impls |

## Tasks

### Tier 1 — Domain extensions (no infra)

**1.1** Extend `WorkloadKind` enum:
```csharp
public enum WorkloadKind {
    Vm = 0, Lxc = 1, Qemu = 2,         // existing (libvirt)
    DockerCompose = 3, K3s = 4, Podman = 5  // new (runtime providers)
}
```

**1.2** Add `WorkloadSpec` tagged-union to `Plexor.Modules.Clusters.Application`:
```csharp
public abstract record WorkloadSpec {
    public abstract WorkloadKind Kind { get; }
    public abstract string Name { get; }
    public abstract string ToJson();
}

public sealed record VmSpec(string Name, string Image, int Vcpu, int RamGb, int DiskGb) : WorkloadSpec {
    public override WorkloadKind Kind => WorkloadKind.Vm;
    public override string ToJson() => JsonSerializer.Serialize(this);
}

public sealed record DockerComposeSpec(
    string Name,
    string Image,
    IReadOnlyList<int> Ports,
    IReadOnlyDictionary<string,string> Env,
    IReadOnlyList<VolumeMount> Volumes) : WorkloadSpec {
    public override WorkloadKind Kind => WorkloadKind.DockerCompose;
    public override string ToJson() => JsonSerializer.Serialize(this);
}

public sealed record K3sSpec(
    string Name, string Image, int Replicas,
    IReadOnlyList<int> Ports, string Namespace) : WorkloadSpec {
    public override WorkloadKind Kind => WorkloadKind.K3s;
    public override string ToJson() => JsonSerializer.Serialize(this);
}

public sealed record PodmanSpec(
    string Name, string Image,
    IReadOnlyList<int> Ports,
    IReadOnlyDictionary<string,string> Env) : WorkloadSpec {
    public override WorkloadKind Kind => WorkloadKind.Podman;
    public override string ToJson() => JsonSerializer.Serialize(this);
}
```

**1.3** `IWorkloadRuntime` interface in `Plexor.Shared.Kernel`:
```csharp
public interface IWorkloadRuntime {
    string RuntimeId { get; }   // "docker-compose" | "podman" | "k3s"
    WorkloadKind SupportedKind { get; }
    Task<string> RenderManifestAsync(WorkloadSpec spec, CancellationToken ct);
    Task<DeploymentResult> DeployAsync(WorkloadSpec spec, string targetNodeId, CancellationToken ct);
    Task<WorkloadState> ReadStateAsync(string manifestPath, CancellationToken ct);
    Task DeleteAsync(string manifestPath, CancellationToken ct);
}
```

### Tier 2 — Cluster.RuntimeId field

**2.1** Add `RuntimeId` to `Cluster` entity:
```csharp
public sealed class Cluster {
    // existing fields...
    public string RuntimeId { get; init; } = "docker-compose";  // default
}
```

**2.2** Migration `ClusterRuntimeId_Add` adds `runtime_id` column (varchar(64),
default `docker-compose`, NOT NULL).

**2.3** Extend `CreateClusterCommand` with optional `RuntimeId`:
```csharp
public sealed record CreateClusterCommand(
    Guid OrgId, string Name, string Region, string Endpoint,
    string RuntimeId = "docker-compose");
```

**2.4** Validation: `RuntimeId` must be one of `docker-compose | podman | k3s`.
Runtime cannot be changed after creation (immutable).

### Tier 3 — Single Workload aggregate (Kind discriminator)

**3.1** `Workload` entity adds `Kind` field (default `Vm`):
```csharp
public sealed class Workload {
    // existing fields...
    public WorkloadKind Kind { get; init; } = WorkloadKind.Vm;
    // SpecJson becomes polymorphic JSON of WorkloadSpec per Kind
}
```

**3.2** Migration `Workloads_AddKind` adds `kind` column (smallint, default 0).

**3.3** Existing workloads stay Kind=Vm, SpecJson unchanged. New workloads
specify Kind via the request DTO.

**3.4** `WorkloadsController.CreateAsync` accepts `WorkloadSpec` (tagged union)
in body, validates Kind is supported by cluster.RuntimeId, persists.

### Tier 4 — Postgres LISTEN/NOTIFY channel

**4.1** New shared kernel module `Plexor.Shared.PostgresTransport`:
```csharp
public sealed class PostgresCommandChannel : ICommandSource {
    public async Task<IReadOnlyList<NodeCommand>> WaitForCommandsAsync(
        Guid nodeId, CancellationToken ct)
    {
        // LISTEN plexor_commands_node_<nodeId>
        // Block until NOTIFY arrives OR 30s timeout (safety net)
        // Return fetched commands
    }
}
```

**4.2** Control plane publishes NOTIFY on INSERT:
```csharp
// In WorkloadActionCommandHandler after SaveChangesAsync:
await db.Database.ExecuteSqlRawAsync(
    $"NOTIFY plexor_commands_node_{nodeId}, '{commandId}'", ct);
```

**4.3** Migration: enable `pg_notify` (default, no-op).

### Tier 5 — Docker Compose runtime

**5.1** `DockerComposeWorkloadRuntime.cs`:
- `RenderManifestAsync` → emits `docker-compose.yaml` with services block
- `DeployAsync` → writes to `/var/lib/plexor/workloads/<name>/compose.yaml`, runs `docker compose up -d`
- `ReadStateAsync` → `docker compose ps --format json`, parse to `WorkloadState`
- `DeleteAsync` → `docker compose down`, rm directory

**5.2** `DockerComposeSpecMapper.cs` (extension of `IWorkloadMapper`):
- `ToSpec(DockerComposeRequest) → DockerComposeSpec`
- `ToSummary(Workload) → DockerComposeSummary` (uses base `WorkloadSummary`)

**5.3** Unit tests:
- `DockerComposeWorkloadRuntimeShould` — snapshot rendering tests
- Round-trip spec → manifest → spec (parse back, verify)

### Tier 6 — Podman Quadlet runtime

**6.1** `PodmanQuadletWorkloadRuntime.cs`:
- `RenderManifestAsync` → emits `<name>.container` quadlet INI
- `DeployAsync` → writes to `/etc/containers/systemd/<name>.container`, runs `systemctl daemon-reload && systemctl start <name>.service`
- `ReadStateAsync` → `systemctl is-active <name>`, parse to `WorkloadState`
- `DeleteAsync` → `systemctl stop`, rm unit file, daemon-reload

**6.2** Unit tests — snapshot rendering of quadlet INI.

### Tier 7 — k3s runtime

**7.1** `K3sWorkloadRuntime.cs`:
- `RenderManifestAsync` → emits kustomize directory (`kustomization.yaml`, `deployment.yaml`, `service.yaml`)
- `DeployAsync` → `kubectl --kubeconfig=/etc/rancher/k3s/k3s.yaml apply -k <path>`
- `ReadStateAsync` → `kubectl get deploy/<name> -n <ns> -o json`, parse replicas
- `DeleteAsync` → `kubectl delete -k <path>`

**7.2** Note: k3s provisioning (cluster has k3s installed) is **out of scope**
for this plan. `K3sWorkloadRuntime` assumes target node already has k3s.
The provisioning layer lives in `plan-k8s` (separate).

**7.3** Unit tests — snapshot rendering of kustomize YAML.

### Tier 8 — NodeAgent command source + executor registry

**8.1** `PostgresListenCommandSource` (Tier 4) replaces Tier 5 polling loop:
```csharp
public sealed class NodeAgentWorker : BackgroundService {
    protected override async Task ExecuteAsync(CancellationToken ct) {
        var listenTask = ListenForCommandsAsync(ct);   // LISTEN/NOTIFY
        var heartbeatTask = HeartbeatLoopAsync(ct);    // Tier 4 (unchanged)
        await Task.WhenAll(listenTask, heartbeatTask);
    }
}
```

**8.2** `IRuntimeDispatch` resolves runtime from cluster.RuntimeId:
```csharp
public sealed class ClusterRuntimeDispatch(IWorkloadRuntime docker, IWorkloadRuntime podman, IWorkloadRuntime k3s) {
    public IWorkloadRuntime Resolve(string runtimeId) => runtimeId switch {
        "docker-compose" => docker,
        "podman" => podman,
        "k3s" => k3s,
        _ => throw new NotSupportedException(...)
    };
}
```

**8.3** `RuntimeCommandExecutor` implements `ICommandExecutor`:
- `Type = "runtime.compose-up"`, `Type = "runtime.compose-down"`, etc.
- Resolves cluster.RuntimeId via `INodeRepository`
- Resolves runtime via `ClusterRuntimeDispatch`
- Calls `RenderManifestAsync` + `DeployAsync`

### Tier 9 — Drift detection reuses Tier 4

App workloads reuse `WorkloadReport` from Tier 4. NodeAgent's
`HeartbeatLoopAsync` adds app workloads' states to `WorkloadReport.Reports`.
Control plane's `NodeHeartbeatCommandHandler.ReconcileWorkloadReportsAsync`
matches by `(cluster, node, name)` and updates `State`. No changes needed
beyond runtime-specific `ReadStateAsync` polling.

### Tier 10 — Tests + acceptance

**10.1** Unit tests (target: 30+ new tests):
- WorkloadSpec tagged-union serialization round-trips
- DockerComposeWorkloadRuntime snapshot rendering
- PodmanQuadletWorkloadRuntime snapshot rendering
- K3sWorkloadRuntime snapshot rendering
- ClusterRuntimeDispatch resolve logic
- PostgresListenCommandSource event delivery (using Testcontainers)

**10.2** Acceptance:
- `dotnet build plexor.slnx -c Debug` clean (0/0)
- `dotnet format plexor.slnx --verify-no-changes --severity hidden` clean
- All tests pass (300+ total)
- One end-to-end smoke test: create cluster with `docker-compose` runtime,
  deploy nginx workload, verify it responds on port 80.

## Out of scope (Phase 7+ or later)

- **Runtime switch for existing clusters** — `Cluster.RuntimeId` is
  immutable; operator must recreate cluster to switch.
- **k3s cluster provisioning** — `K3sWorkloadRuntime` assumes k3s is
  already installed on the target node. Lives in `plan-k8s`.
- **Helm provider** — Phase 7+ (composes k3s + Helm chart rendering).
- **Cross-cluster workload distribution** — Phase 7+ (single cluster).
- **Auto-scaling** — Phase 7+ (single-replica workloads only in v0.1).
- **NATS event bus** — explicitly NOT introduced (per Q2 decision).

## Risks

1. **Postgres NOTIFY payload limit (8 KB)** — small workloads are fine,
   large multi-container compose files might exceed. Mitigation: store
   full manifest in `NodeCommand.PayloadJson` (jsonb), NOTIFY just carries
   `commandId`.
2. **LISTEN connection lifetime** — Postgres connections are not free;
   one per agent. At 100 agents × 1 connection = 100 connections.
   Acceptable but worth monitoring.
3. **k3s on every cluster node** — runtime impl exists but provisioning
   is `plan-k8s`. Users with k3s clusters can use it; users without
   can use docker-compose/podman.

## Build order

1. Tier 1 (Domain) → Tier 2 (Cluster.RuntimeId) → Tier 3 (Workload.Kind)
2. Tier 4 (Postgres LISTEN/NOTIFY) — independent of runtimes
3. Tier 5 (Docker Compose) → Tier 6 (Podman) → Tier 7 (k3s)
4. Tier 8 (NodeAgent command source + dispatch)
5. Tier 9 (drift detection — no code changes, just runtime impl hooks)
6. Tier 10 (tests + acceptance)

## Acceptance criteria

- `dotnet build plexor.slnx -c Debug` — 0 errors, 0 warnings.
- `dotnet format plexor.slnx --verify-no-changes --severity hidden` — 0 errors.
- All unit tests pass (target: 330+ total tests, 30+ new).
- Smoke test: docker-compose runtime end-to-end works (deploy + drift
  detection + delete).
- OpenAPI document auto-emits 401/403/500; new endpoints have
  `[ProducesResponseType]` for 2xx shapes.
- All new code conforms to `.agents/rules/` (sealed, primary ctor,
  file-scope namespace, snake_case, etc.).
- All new files ≤ 200 lines OR decomposed (per class-decomposition.md).