// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// WorkloadReport — one workload's current state, as reported by the
// node in its most recent HeartbeatRequest. Extracted from
// NodeContracts.cs (Sprint 3, item 5) per folder-organization.md §1.
// ============================================================================

namespace Plexor.Shared.NodeApi;

/// <summary>
///     One workload's current state, as reported by the node in its
///     most recent <see cref="HeartbeatRequest" />. The control
///     plane reconciles these reports against its durable
///     <c>forge.workloads</c> view and updates each
///     <c>LastReportedAt</c> + <c>State</c> accordingly. Drift
///     detection (Phase D Tier 4) consumes these to surface
///     "VM says Running but control-plane says Provisioning" to
///     the operator.
/// </summary>
/// <param name="WorkloadId">
///     Control-plane workload id (<c>wl_&lt;UUIDv7&gt;</c>). The
///     agent doesn't generate this; the control plane does, at
///     workload-create time, and stashes it in the
///     <c>workload.create</c> payload the agent received. The agent
///     echoes it back here verbatim.
/// </param>
/// <param name="LocalId">
///     Provider-assigned id (libvirt domain UUID, container id, k8s
///     pod name, etc.). Stable across start/stop on the same
///     workload; the control plane uses it for the
///     <c>Start/Stop/Delete</c> action commands.
/// </param>
/// <param name="Name">Human-facing workload name (matches <c>WorkloadSpec.Name</c>).</param>
/// <param name="State">Current lifecycle state.</param>
public sealed record WorkloadReport(
    Guid WorkloadId,
    string? LocalId,
    string Name,
    WorkloadReportState State);
