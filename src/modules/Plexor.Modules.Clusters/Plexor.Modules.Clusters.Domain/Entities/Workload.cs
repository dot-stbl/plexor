// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Workload — the control-plane view of a workload the operator
// asked us to deploy. Records the runtime (vm / lxc / k8s.pod /
// container), the spec the operator passed, which cluster + node
// it landed on, and the current reported state.
//
// The NodeAgent owns the local lifecycle (libvirt UUID, container
// id, k3s pod name) — that's the LocalWorkload record on the
// node side. This entity is the control-plane's mirror: it
// records what was asked and which node is supposed to be
// running it. Drift detection (Phase D follow-up) reconciles
// this row against the agent's LocalWorkload on a periodic
// timer.
//
// Lives in Plexor.Modules.Clusters because the workload table
// lives in the forge schema (clusters own the node fleet that
// workloads run on). The per-runtime provider project owns its
// own LocalWorkload; this row is the durable view.
// ============================================================================

using Plexor.Shared.Filtering.Registry;
using Plexor.Shared.Identifiers;
using Plexor.Shared.Kernel.Common;
using Plexor.Shared.Workloads;

namespace Plexor.Modules.Clusters.Domain.Entities;

/// <summary>
///     The control-plane's view of a workload the operator asked
///     us to deploy. Lifecycle is driven by the NodeAgent
///     (Pending → Provisioning → Running → Stopped / Failed); the
///     host only mirrors what the agent reports.
/// </summary>
public sealed class Workload : IFilterableEntity, ICreatedAt, IUpdatedAt
{
    /// <summary>
    ///     Workload id. Wire format <c>wl_&lt;UUIDv7&gt;</c> —
    ///     the operator-facing handle. The NodeAgent's
    ///     <see cref="LocalWorkload" /> keeps a separate local id
    ///     (libvirt UUID, k3s pod name) that we keep in
    ///     <see cref="LocalId" /> for cross-reference.
    /// </summary>
    public WorkloadId Id { get; init; }

    /// <summary>FK to the parent cluster.</summary>
    public ClusterId ClusterId { get; init; }

    /// <summary>
    ///     FK to the assigned node. Null while the workload is
    ///     <see cref="WorkloadState.Provisioning" /> and during
    ///     transient rebalancing.
    /// </summary>
    public NodeId? AssignedNodeId { get; set; }

    /// <summary>
    ///     Stable per-runtime handle returned by the NodeAgent
    ///     (libvirt UUID, container id, k8s pod name). Lets the
    ///     host correlate a row here with a row on the node
    ///     without owning the runtime's id space. Null until
    ///     the agent reports back.
    /// </summary>
    public string? LocalId { get; set; }

    /// <summary>Operator-facing name. Unique per cluster.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Runtime identifier — "vm", "lxc", "k8s.pod", "container".</summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>Operator-supplied configuration (image, env, ports, volumes). JSON.</summary>
    public string SpecJson { get; init; } = "{}";

    /// <summary>Current lifecycle state as reported by the NodeAgent.</summary>
    public WorkloadState State { get; set; }

    /// <summary>Last state message from the NodeAgent (last error, etc.).</summary>
    public string? LastMessage { get; set; }

    /// <summary>When the agent last reported on this workload.</summary>
    public DateTimeOffset? LastReportedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; init; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; init; }
}