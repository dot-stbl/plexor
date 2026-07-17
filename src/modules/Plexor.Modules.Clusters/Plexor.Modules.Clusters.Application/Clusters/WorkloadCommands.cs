// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Workload commands + queries + projections. Application layer
// carries the wire shapes; handlers in Infrastructure run the EF
// queries. Mirrors the ClusterCommands pattern.
// ============================================================================

using Plexor.Modules.Clusters.Domain.Entities;
using Plexor.Shared.Contracts.Pagination;
using Plexor.Shared.Filtering.Query;
using Plexor.Shared.Identifiers;

namespace Plexor.Modules.Clusters.Application.Clusters;

// --- commands ---------------------------------------------------------------

/// <summary>
///     Provision a new workload. Admin-only — caller must hold
///     the workload-create permission.
///     Lifecycle starts at <see cref="Plexor.Shared.Workloads.WorkloadState.Provisioning" />;
///     the NodeAgent's drift-detection job (Phase D Tier 4) moves
///     it forward as the runtime reports back.
/// </summary>
/// <param name="ClusterId">Target cluster.</param>
/// <param name="Name">Operator-facing name (unique per cluster).</param>
/// <param name="Kind">Runtime identifier — "vm" / "lxc" / "k8s.pod" / "container".</param>
/// <param name="SpecJson">Operator-supplied configuration (image, env, ports, volumes) as JSON.</param>
public sealed record CreateWorkloadCommand(
    ClusterId ClusterId,
    string Name,
    string Kind,
    string SpecJson);

/// <summary>
///     Soft-delete a workload. The NodeAgent's next drift poll
///     tears down the local runtime handle; the control-plane
///     row stays in <c>forge.workloads</c> for audit + FK integrity.
/// </summary>
/// <param name="ClusterId">Target cluster.</param>
/// <param name="WorkloadId">Target workload.</param>
public sealed record DeleteWorkloadCommand(
    ClusterId ClusterId,
    WorkloadId WorkloadId);

// --- queries ----------------------------------------------------------------

/// <summary>Fetch one workload by id.</summary>
/// <param name="ClusterId">Parent cluster.</param>
/// <param name="WorkloadId">Target workload.</param>
public sealed record GetWorkloadQuery(
    ClusterId ClusterId,
    WorkloadId WorkloadId);

/// <summary>List workloads in one cluster, paged + filtered via
/// the standard <see cref="FilterQuery" /> envelope.</summary>
/// <param name="ClusterId">Target cluster.</param>
/// <param name="Query">URL envelope (filter DSL + sort + paging).</param>
public sealed record ListWorkloadsQuery(
    ClusterId ClusterId,
    FilterQuery Query);

// --- projections ------------------------------------------------------------

/// <summary>Public projection of <see cref="Workload" /> — list-card shape.
/// <c>sealed partial class</c> with init-only properties so
/// Mapperly's source generator can emit the mapping body.</summary>
public sealed partial class WorkloadSummary
{
    /// <summary>Workload id (wl_&lt;UUIDv7&gt;).</summary>
    public WorkloadId Id { get; init; }

    /// <summary>Parent cluster.</summary>
    public ClusterId ClusterId { get; init; }

    /// <summary>Assigned node, null while pending placement.</summary>
    public NodeId? AssignedNodeId { get; init; }

    /// <summary>Per-runtime handle (libvirt UUID, container id).</summary>
    public string? LocalId { get; init; }

    /// <summary>Operator-facing name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Runtime identifier — vm / lxc / k8s.pod / container.</summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>Current lifecycle state as reported by the NodeAgent.</summary>
    public Plexor.Shared.Workloads.WorkloadState State { get; init; }

    /// <summary>When the agent last reported on this workload.</summary>
    public DateTimeOffset? LastReportedAt { get; init; }

    /// <summary>Creation time (UTC).</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Last modification time (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}

// Paged list response uses Plexor.Shared.Contracts.Pagination.PageResult<T>
// directly — no project-specific wrapper.

// --- action commands (Tier 5) ----------------------------------------------

/// <summary>Workload action verb — what the operator asked the agent to do.</summary>
public enum WorkloadAction
{
    /// <summary>Start a previously provisioned workload (boot the runtime).</summary>
    Start = 0,

    /// <summary>Gracefully shut down a running workload (resources stay allocated).</summary>
    Stop = 1,

    /// <summary>Restart = Stop + Start in one round trip. Convenience for the UI.</summary>
    Restart = 2
}

/// <summary>
///     Enqueue a <see cref="WorkloadAction" /> against an
///     already-provisioned workload. The control plane writes
///     the matching <c>forge.commands</c> row; the NodeAgent's
///     long-poll picks it up within seconds, executes via
///     the agent's ICommandExecutor, and
///     posts back the result. The handler returns once the
///     command reaches a terminal state (Acked or Failed).
/// </summary>
/// <param name="ClusterId">Parent cluster (scope check).</param>
/// <param name="WorkloadId">Target workload (must be in this cluster).</param>
/// <param name="Action">Verb to execute.</param>
public sealed record WorkloadActionCommand(
    ClusterId ClusterId,
    WorkloadId WorkloadId,
    WorkloadAction Action);

/// <summary>
///     Result of a successful <see cref="WorkloadActionCommand" />.
///     Carries the new state the agent reported back plus the
///     idempotency key the control plane uses to correlate the
///     long-poll result with the originally-enqueued command.
/// </summary>
/// <param name="CommandId">Wire-format command id (UUIDv7). Stable across retries.</param>
/// <param name="State">Workload state after the action executed (e.g. <see cref="Plexor.Shared.Workloads.WorkloadState.Running" /> after start).</param>
public sealed record WorkloadActionResult(
    Guid CommandId,
    Plexor.Shared.Workloads.WorkloadState State);
