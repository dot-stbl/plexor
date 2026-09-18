// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// WorkloadLifecycleState — the host's view of a workload's lifecycle.
//
// Distinction from Plexor.Shared.Workloads.WorkloadState. The shared
// WorkloadState enum (Provisioning / Running / Stopped / Failed /
// Unknown) is what the NodeAgent reports back to the control plane —
// it's the runtime's view. WorkloadLifecycleState is the host's view:
// it tracks the host's intent + the provider's acknowledgement across
// the lifetime of a forge.workloads row (Pending → Provisioning →
// Stopped → Running → Stopped → Deleting → Deleted).
//
// The two enums are mapped at the boundary: a successful StartAsync
// flips LifecycleState to Running AND mirrors State (the agent's
// reported runtime state) to Running on the next reconciliation tick.
// The host does not block on the agent — it tracks the provider
// promise optimistically and reconciles in the background.
// ============================================================================

namespace Plexor.Modules.Clusters.Domain.Entities;

/// <summary>
///     Host-side lifecycle state of a workload. Validated transitions
///     live in <see cref="WorkloadLifecycleTransitions" />; the
///     <c>Workload</c> aggregate's <c>Mark*</c> methods enforce them
///     and append a <see cref="WorkloadLifecycleEvent" /> row on every
///     successful transition.
/// </summary>
public enum WorkloadLifecycleState
{
    /// <summary>
    ///     Row exists in the database; no provider-side resources
    ///     allocated yet. The starting state for any newly created
    ///     workload.
    /// </summary>
    Pending = 0,

    /// <summary>
    ///     <see cref="Plexor.Shared.Kernel.Compute.IComputeProvider.CreateVmAsync" />
    ///     dispatched; awaiting provider confirmation that the VM
    ///     exists.
    /// </summary>
    Provisioning = 1,

    /// <summary>
    ///     Provider confirmed the VM exists but is currently powered
    ///     off. Resources are still allocated.
    /// </summary>
    Stopped = 2,

    /// <summary>VM exists and is currently running.</summary>
    Running = 3,

    /// <summary>
    ///     Last lifecycle operation failed. Row stays in the table
    ///     for operator inspection / manual recovery.
    /// </summary>
    Failed = 4,

    /// <summary>
    ///     <see cref="Plexor.Shared.Kernel.Compute.IComputeProvider.DeleteVmAsync" />
    ///     dispatched; awaiting provider confirmation that the VM
    ///     is gone.
    /// </summary>
    Deleting = 5,

    /// <summary>
    ///     Provider confirmed the VM is gone; row preserved for
    ///     audit + FK integrity. Terminal state — no further
    ///     transitions accepted.
    /// </summary>
    Deleted = 6
}
