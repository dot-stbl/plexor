# Spec delta: clusters (fe-vm-lifecycle)

This file is the proposed state of
`openspec/specs/clusters/spec.md` after the change
`openspec/changes/fe-vm-lifecycle/` is merged. It uses the
`## ADDED Requirements` convention from OpenSpec — every
requirement here is new.

When the change lands, this delta is promoted into
`openspec/specs/clusters/spec.md` under `## Requirements`,
and the `## ADDED Requirements` heading is removed.

## ADDED Requirements

### Requirement: WorkloadLifecycleState state machine

The system SHALL route every state transition on a
`Workload` row through `WorkloadStateMachine`
(`Plexor.Modules.Clusters.Application.Workloads.WorkloadStateMachine`).

The state machine SHALL recognize five states:

- `Pending` — workload created but not yet scheduled.
- `Provisioning` — NodeAgent accepted the work; the
  runtime is allocating.
- `Running` — active on the runtime.
- `Stopped` — stopped (preserved on disk).
- `Failed` — provisioning or runtime error.

The state machine SHALL enforce the transition graph:

- `Pending` → `Provisioning` — operator Start.
- `Pending` → `Failed` — operator delete (cancelled).
- `Provisioning` → `Running` — NodeAgent reports success
  (`LocalId` populated).
- `Provisioning` → `Failed` — NodeAgent reports failure.
- `Running` → `Stopped` — operator Stop.
- `Running` → `Failed` — NodeAgent reports runtime error.
- `Stopped` → `Pending` — operator Restart.

A transition that is not in the graph SHALL be rejected
with a stable error code (`compute.workload.invalid_transition`).
The state machine is the **only** place `Workload.State` is
mutated; direct assignments are a code-review reject.

### Requirement: IComputeProvider seam

The system SHALL expose `IComputeProvider`
(`Plexor.Shared.Kernel.Compute.IComputeProvider`) with the
following surface:

```csharp
public interface IComputeProvider
{
    string ProviderId { get; }
    Task<ComputeWorkloadHandle> StartAsync(ComputeWorkloadSpec spec, CancellationToken ct);
    Task StopAsync(ComputeWorkloadHandle handle, CancellationToken ct);
    Task<ComputeWorkloadState> GetStateAsync(ComputeWorkloadHandle handle, CancellationToken ct);
    Task DeleteAsync(ComputeWorkloadHandle handle, CancellationToken ct);
}
```

The interface is the seam every compute backend
implements. `ComputeWorkloadHandle` carries the
provider-specific runtime handle (libvirt UUID, k3s pod
name, or — for the NoOp provider — a synthetic GUID).

`IComputeProvider` is owned by `Plexor.Shared.Kernel.Compute`
so NodeAgent + the cluster module both depend on it.

### Requirement: NoOpComputeProvider default

The system SHALL ship `NoOpComputeProvider`
(`Plexor.Shared.Kernel.Compute.NoOpComputeProvider`) as the
v0.1 default implementation:

- `ProviderId = "noop"`.
- `StartAsync` mints a `ComputeWorkloadHandle` with
  `LocalId = Guid.NewGuid()` and returns.
- `StopAsync` and `DeleteAsync` are no-ops (return
  `Task.CompletedTask`).
- `GetStateAsync` always returns `Running`.

The FE SHALL display a "no-op provider" badge next to
workloads backed by `NoOpComputeProvider` so operators
don't confuse "Running" with "actually exists".

The NodeAgent registers `NoOpComputeProvider` as the
default in v0.1. Real providers (libvirt, k3s) replace
the registration via `services.AddSingleton<IComputeProvider,
LibvirtComputeProvider>()` in `Plexor.NodeAgent/Program.cs`.

### Requirement: ComputeWorkloadSpec vs Workload.SpecJson

The NodeAgent SHALL parse the opaque `Workload.SpecJson`
into a typed `ComputeWorkloadSpec`
(`Plexor.Shared.Kernel.Compute.ComputeWorkloadSpec`) before
forwarding to `IComputeProvider.StartAsync`.

`ComputeWorkloadSpec` SHALL carry at minimum:

- `Kind : string` — `vm` | `lxc` | `k8s.pod` | `container`.
- `Image : string?` — image name (libvirt / QEMU volume
  source).
- `Cpu : int` — vCPU count.
- `RamGb : int` — RAM in GiB.
- `DiskGb : int` — root disk in GiB.
- `ExtraJson : string?` — provider-specific extra config
  (passed through unchanged).

`Workload.SpecJson` remains the operator-facing surface;
provider-specific extras live there for backends that
need them (e.g. KVM's `<os><type arch="...">`, k3s's
`resources.limits`).

### Requirement: WorkloadStateChanged audit event

The system SHALL publish a `WorkloadStateChanged` domain
event on every successful transition. The audit module
SHALL consume the event and write an
`atlas.audit_entries` row:

- `action = "workload.state.changed"`.
- `target_type = "Workload"`.
- `target_id = <workload id>`.
- `payload_json = { "from": "<state>", "to": "<state>",
  "actorUserId": "..." }`.

A consumer who wants the full timeline for a workload
queries `GET /api/v1/audit?actionPrefix=workload.state.*&targetId=X`.

### Requirement: Start / Stop / Delete commands route through IComputeProvider

The control plane SHALL route every
`StartWorkloadCommand`, `StopWorkloadCommand`, and
`DeleteWorkloadCommand` through `IComputeProvider`:

- `StartWorkloadCommandHandler` →
  `IComputeProvider.StartAsync(spec, ct)`. The returned
  `ComputeWorkloadHandle.LocalId` is persisted on the
  `Workload` row.
- `StopWorkloadCommandHandler` →
  `IComputeProvider.StopAsync(handle, ct)`. The state
  transitions `Running` → `Stopped`.
- `DeleteWorkloadCommandHandler` →
  `IComputeProvider.DeleteAsync(handle, ct)`. The
  workload row is deleted.

The NodeAgent's command-dispatch pipeline (`/nodes/{id}/commands/poll`)
SHALL poll the control plane, look up the workload's
`LocalId`, and call the provider on the agent host.
