# Tasks: fe-vm-lifecycle

Numbered checklist.

## VLC.1 — IComputeProvider seam

- [x] `Plexor.Shared.Kernel.Compute.IComputeProvider`
      interface.
- [x] `ComputeWorkloadSpec` /
      `ComputeWorkloadHandle` /
      `ComputeWorkloadState` types (sealed records).
- [x] `NoOpComputeProvider` implementation (default for
      v0.1 — NodeAgent wires this unless overridden).

## VLC.2 — WorkloadState state machine

- [x] `WorkloadStateMachine` in
      `Plexor.Modules.Clusters.Application.Workloads`.
- [x] State column extended to the 5-state enum.
- [x] Every state change goes through the machine.
- [x] Domain event `WorkloadStateChanged` published on
      every transition.

## VLC.3 — Wire commands through the compute provider

- [x] `StartWorkloadCommandHandler` calls
      `IComputeProvider.StartAsync(spec, ct)` and stores
      the returned handle on the `Workload.LocalId`.
- [x] `StopWorkloadCommandHandler` calls
      `IComputeProvider.StopAsync(handle, ct)`.
- [x] `DeleteWorkloadCommandHandler` calls
      `IComputeProvider.DeleteAsync(handle, ct)`.
- [x] The NodeAgent polls `/nodes/{nodeId}/commands/poll`
      and dispatches to the provider.
- [x] Unit tests: `WorkloadStateMachineShould` (8 cases) +
      `NoOpComputeProviderShould` (3 cases).

## VLC.4 — Audit integration

- [x] `WorkloadStateChanged` event consumer in the audit
      module writes an `atlas.audit_entries` row.
- [x] Wire name: `workload.state.changed`.
- [x] Payload: `{ from, to, actorUserId }`.

## VLC.5 — FE-side VM lifecycle UI

- [x] `WorkloadsPage` (`/workloads`) shows the
  workload list with the live state pill.
- [x] `WorkloadDetailPage` (`/workloads/$workloadId`)
  shows the timeline of state changes.
- [x] Start / Stop / Delete buttons gated on
  `compute.workloads.*` permissions.

## VLC.6 — Merge back into the integration branch

- [x] `merge compute-module` commit lands on `main`.
