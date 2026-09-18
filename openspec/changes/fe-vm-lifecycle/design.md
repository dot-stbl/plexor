# Design: fe-vm-lifecycle

Technical decisions for the VM lifecycle + compute-provider
seam work.

## State machine vs ad-hoc transitions

The `Workload.State` column already existed (v0.0 shipped
`Pending` / `Provisioning` / `Running` / `Stopped` /
`Failed`). What's new in v0.1:

- **Explicit transitions.** v0.0 had no centralized
  transition rule — every handler flipped the state field
  directly. v0.1 routes every state change through
  `WorkloadStateMachine` which checks the source state
  and rejects invalid transitions (e.g. `Failed` →
  `Running` without going through `Pending`).
- **Domain event.** v0.0 also had no event; the audit
  integration required polling the row. v0.1 emits
  `WorkloadStateChanged` from the state machine so the
  audit module consumes a real event.

The state machine is a `public sealed class` with one
`TransitionAsync(workloadId, toState, actorUserId, ct)`
method. The body is the single source of truth for "what
states can move to what".

## IComputeProvider vs the existing NodeAgent shape

`Plexor.NodeAgent` already has a `LibvirtKvmProvider`
(Phase D Tier 3.5). The new `IComputeProvider` is a
**higher-level abstraction** that sits between the
NodeAgent's command-dispatch pipeline and the existing
hypervisor-specific code.

```
Plexor.NodeAgent (command poll) →
  IComputeProvider.StartAsync(spec, ct) →
    LibvirtKvmProvider (existing, KVM/QEMU)
    NoOpComputeProvider (v0.1 default)
    K3sComputeProvider (Phase 5+)
```

The NodeAgent's existing `LibvirtKvmProvider` becomes
the libvirt implementation of `IComputeProvider` — no
rewrite of the libvirt path, just an adapter that
implements the new interface.

## ComputeWorkloadSpec vs Workload.SpecJson

The existing `Workload.SpecJson` is the operator-supplied
JSON for the workload (`{ image: "ubuntu-22.04", vcpu: 2,
ram_gb: 4, ... }`). The new `ComputeWorkloadSpec` is the
provider-facing typed shape — the NodeAgent parses
`SpecJson` into a `ComputeWorkloadSpec` and forwards to
the provider.

The two shapes are intentionally separate:

- `Workload.SpecJson` — opaque to Plexor; the runtime
  decides what fields it needs.
- `ComputeWorkloadSpec` — Plexor's interpretation of the
  common fields (`Kind`, `Image`, `Cpu`, `Ram`, `Disk`)
  every backend needs.

If the runtime needs additional fields, they live in
`SpecJson` and the provider parses them locally.

## NoOp default — why it's the right v0.1 shape

The NoOp default is intentional, not laziness. Three
reasons:

1. **Self-hosted testing.** A contributor can run
   `Plexor.Host` + `Plexor.NodeAgent` locally without
   KVM, libvirt, or k3s; the workload moves through the
   state machine and shows up in the UI as `Running`
   even though no VM exists.
2. **CI / smoke tests.** The integration tests don't
   need a hypervisor; the NoOp path is the canonical
   "no hardware" deployment.
3. **Forward compatibility.** When the libvirt / k3s
   providers ship, switching is a one-line DI registration
   — the workload entity, the state machine, the audit
   event all stay unchanged.

The NoOp provider is honest about its nature — the FE
shows a "no-op provider" badge next to the workload so
operators don't confuse "Running" with "actually exists".

## WorkloadStateChanged → audit event shape

The audit event payload is the minimum needed for an
auditor to reconstruct the timeline:

```jsonc
{
  "from": "Pending",
  "to": "Running",
  "actorUserId": "..."  // optional — null for system events
}
```

The full transition history is queryable via the audit
endpoint (`GET /api/v1/audit?actionPrefix=workload.state.*&targetId=X`).
The state field on the workload row carries only the
current state — history lives in the audit log.

## DI registration

`Plexor.NodeAgent/Program.cs`:

```csharp
// v0.1 default — NoOp unless overridden by a real provider
services.AddSingleton<IComputeProvider, NoOpComputeProvider>();

// Real libvirt provider (optional, ships in
// openspec/changes/libvirt-provider/):
// services.AddSingleton<IComputeProvider, LibvirtComputeProvider>();
```

The conditional registration is per-deployment: an
operator who wires libvirt uncomments the second line.
The v0.1 default is NoOp; the libvirt provider is opt-in.

## Open questions deferred

- **k3s provider** — Phase 5+ with the workload
  controller's K8s integration.
- **Podman provider** — Phase 5+ as a sibling to k3s.
- **Live migration between providers** — a workload's
  `LocalId` is provider-specific; migration requires
  restart (`Stopped` → `Pending`).
- **Live migration between nodes** — Phase 5+ for the
  real libvirt path.
