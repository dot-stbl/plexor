// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Node entity. Plan: .agents/docs/plans/plan-clusters.md.
//
// A Node is a Plexor.NodeAgent instance joined to a cluster. The
// agent posts its hardware snapshot at first heartbeat, then every
// 30 s. Plexor.Host keeps a 3-missed-heartbeat window before flipping
// Status to Gone (90 s of silence).
// ============================================================================

using Plexor.Shared.Filtering.Registry;
using Plexor.Shared.Identifiers;
using Plexor.Shared.Kernel.Common;

namespace Plexor.Modules.Clusters.Domain.Entities;

/// <summary>
///     A Plexor.NodeAgent joined to a cluster. Self-reported hostname,
/// hardware snapshot, and lifecycle status are the canonical state.
/// </summary>
public sealed class Node : IFilterableEntity, ICreatedAt, IUpdatedAt
{
    /// <summary>
    ///     NodeId — strongly-typed <c>node_&lt;UUIDv7&gt;</c> wire format.
    ///     Used as the CN of the X.509 client cert in Phase B mTLS.
    /// </summary>
    public NodeId Id { get; init; }

    /// <summary>FK to <see cref="Cluster.Id" />.</summary>
    public ClusterId ClusterId { get; init; }

    /// <summary>Tenant scope (denormalized for org-scoped queries).</summary>
    public Guid OrgId { get; init; }

    /// <summary>Self-reported hostname (Plexor.NodeAgent populates on
    /// join). Unique per cluster — enforced by
    /// `ix_nodes_cluster_id_hostname`.</summary>
    public string Hostname { get; init; } = string.Empty;

    /// <summary>Role within the cluster — see <see cref="NodeRole" />.</summary>
    public NodeRole Role { get; init; }

    /// <summary>Lifecycle status — see <see cref="NodeStatus" />.</summary>
    public NodeStatus Status { get; init; }

    /// <summary>Hardware snapshot reported at join. Immutable —
    /// updated only if the agent re-joins after a wipe.</summary>
    public NodeSpec Spec { get; init; } = new(0, 0, 0, []);

    /// <summary>ISO image version the node was provisioned from
    /// (e.g. "0.1.0-dev"). Compared against the cluster's
    /// <see cref="Cluster.HostVersion" /> at every heartbeat.</summary>
    public string IsoVersion { get; init; } = string.Empty;

    /// <summary>Last heartbeat timestamp (UTC). Bumped on every
    /// <c>POST /api/v1/compute/clusters/{id}/heartbeat</c>.</summary>
    public DateTimeOffset? LastHeartbeatAt { get; init; }

    /// <summary>When the node first joined the cluster.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Last modification time (UTC) — bumped on any
    /// field write (status change, hardware update, etc.).</summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>WireGuard public key of the node. Set during the
    /// join handshake. Workers use it to authenticate the WireGuard
    /// mesh.</summary>
    public string WireguardPublicKey { get; init; } = string.Empty;

    /// <summary>How many VMs are currently scheduled on this node.
    /// Updated by the workload runtime on each deploy / undeploy.</summary>
    public int VmCount { get; init; }
}
