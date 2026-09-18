# Change: fe-vm-lifecycle

## Why

The `Workload` lifecycle today is partially implemented:

- `WorkloadEntity` carries `State : WorkloadState` and
  `LastReportedAt`, and the agent polls the
  `forge.commands` queue to drive transitions.
- The control plane records the operator's `SpecJson` and
  the assigned `NodeId`.
- But there is **no abstraction over the compute backend**:
  every code path that wants to ask "what's the actual VM
  state on the hypervisor?" has to know about libvirt,
  QEMU, k3s, etc. Today that's a hard-coded libvirt path
  in `Plexor.NodeAgent` — there's no seam to swap
  providers.

Phase "fe-vm-lifecycle" (the FE-side VM lifecycle work,
mirrored by the compute-module seam) ships:

1. A `WorkloadLifecycleState` state machine
   (`Pending` → `Provisioning` → `Running` → `Stopped` /
   `Failed`) with explicit transitions on the `Workload`
   row.
2. An `IComputeProvider` seam in
   `Plexor.Shared.Kernel.Compute` so the NodeAgent can
   swap between `LibvirtComputeProvider` /
   `K3sComputeProvider` / `NoOpComputeProvider` behind
   one interface.
3. A `NoOpComputeProvider` default — no VM is ever
   actually created; the workload moves through the state
   machine but no `virsh` / `kubectl` calls are made.
   This is the v0.1 default for self-hosted deploys that
   haven't wired a real backend yet.

The real libvirt provider ships separately under
`openspec/changes/libvirt-provider/`.

## What

### WorkloadLifecycleState state machine

The `Workload` row carries a `State` column
(`WorkloadState` enum):

| State | Meaning | Transition triggers |
|-------|---------|---------------------|
| `Pending` | Created but not yet scheduled | Operator create |
| `Provisioning` | NodeAgent accepted the work; runtime allocating | `Start` command acked |
| `Running` | Active on the runtime | Provisioning acked |
| `Stopped` | Stopped (preserved) | `Stop` command acked |
| `Failed` | Provisioning or runtime error | `ErrorMessage` non-null |

Transitions:

- `Pending` → `Provisioning` — NodeAgent acks the
  `Start` command.
- `Provisioning` → `Running` — NodeAgent posts the
  runtime handle (`LocalId`) and success result.
- `Provisioning` → `Failed` — NodeAgent posts a failure.
- `Running` → `Stopped` — NodeAgent acks the `Stop`
  command.
- `Running` → `Failed` — NodeAgent posts a runtime error.
- `Stopped` → `Pending` — operator restarts.

The `WorkloadStateMachine` (in
`Plexor.Modules.Clusters.Application.Workloads.WorkloadStateMachine`)
centralizes the transition rules. Every state change goes
through the machine; the machine emits a `WorkloadStateChanged`
domain event consumed by the audit module.

### IComputeProvider seam

`Plexor.Shared.Kernel.Compute.IComputeProvider`:

```csharp
public interface IComputeProvider
{
    string ProviderId { get; }      // "noop" | "libvirt" | "k3s"

    Task<ComputeWorkloadHandle> StartAsync(
        ComputeWorkloadSpec spec,
        CancellationToken cancellationToken);

    Task StopAsync(
        ComputeWorkloadHandle handle,
        CancellationToken cancellationToken);

    Task<ComputeWorkloadState> GetStateAsync(
        ComputeWorkloadHandle handle,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        ComputeWorkloadHandle handle,
        CancellationToken cancellationToken);
}
```

The interface is the seam every compute backend implements.
`ComputeWorkloadSpec` carries the workload kind + spec
JSON; `ComputeWorkloadHandle` is the runtime handle the
provider mints (libvirt UUID, k3s pod name, etc.).

### NoOpComputeProvider default

`Plexor.NodeAgent` ships a `NoOpComputeProvider` that:

- `StartAsync` returns a `ComputeWorkloadHandle` with
  `LocalId = Guid.NewGuid()` and `ProviderId = "noop"`.
- `StopAsync` / `DeleteAsync` are no-ops (return `Task`).
- `GetStateAsync` always returns `Running`.

This is the v0.1 default. A self-hosted deploy that wires
the real libvirt provider overrides the DI registration
in `Plexor.NodeAgent/Program.cs`.

### Domain event for state changes

`WorkloadStateChanged`
(`Plexor.Modules.Clusters.Domain.Events.WorkloadStateChanged`)
— published on every state transition; carries
`(WorkloadId, FromState, ToState, ActorUserId?,
OccurredAt)`. The audit module consumes the event and
writes a row to `atlas.audit_entries` with
`action = "workload.state.changed"`,
`target_type = "Workload"`,
`payload_json = { "from": "Pending", "to": "Running" }`.

## Impact

- **`clusters` capability** — additive delta:
  - New `WorkloadLifecycleState` requirement (state
    machine).
  - New `IComputeProvider` seam requirement.
  - New `NoOpComputeProvider` default requirement.
  - New `WorkloadStateChanged` audit event.
  See `openspec/changes/fe-vm-lifecycle/specs/clusters/spec.md`.

## Out of scope (libvirt-provider ships separately)

- **Real libvirt provider** — `openspec/changes/libvirt-provider/`
  ships `LibvirtComputeProvider : IComputeProvider`
  using `virsh` / `virt-install` shell-out.
- **k3s provider** — Phase 5+ with the workload
  controller's K8s integration.
- **Podman provider** — Phase 5+ as a sibling to k3s.
- **Migration between providers** — a workload's
  `LocalId` is provider-specific; migration requires
  restart (`Stopped` → `Pending`).
- **Live migration** — Phase 5+.
