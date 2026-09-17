// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodeRecord — one row in `outpost.node_records` (snake_case: node_records).
//
// A NodeRecord is the host-side view of a single Plexor.NodeAgent that has
// joined a Plexor.Host control plane. The agent self-reports hostname,
// IP, hardware snapshot, and lifecycle status; the host stamps
// `last_heartbeat_at` on every keepalive.
//
// Naming: architecture theme = `outpost` (the schema); concept =
// NodeRecord (the C# class). See AGENTS.md §"Naming".
//
// Outpost is the canonical owner of node tracking as of this PR — the
// historical forge.nodes row lives here now. Workloads FK into
// outpost.node_records via assigned_node_id (FK is varchar(64), no
// cross-DbContext navigation property to keep the schema-per-module
// contract honest).
// ============================================================================

using Plexor.Modules.Clusters.Domain;
using Plexor.Shared.Filtering.Registry;
using Plexor.Shared.Identifiers;
using Plexor.Shared.Kernel.Common;
using NodeStatus = Plexor.Modules.Outpost.Application.NodeStatus;

namespace Plexor.Modules.Outpost.Application;

/// <summary>
///     Host-side view of a Plexor.NodeAgent that has joined a Plexor.Host.
///     One row per node, uniquely keyed by <see cref="Id" />.
/// </summary>
/// <remarks>
///     <para><b>Init-only.</b> Mutability happens via tracked-entity writes
///     on the EF entity (a separate row in the Infrastructure layer);
///     the Application-side <see cref="NodeRecord" /> is the immutable
///     shape handlers + mappers project to.</para>
///     <para><b>Tenant scope.</b> <see cref="OrgId" /> is the auth tenant
///     the node belongs to. v0.1 is single-tenant (Guid.Empty for
///     everything); the column exists for the multi-tenant migration
///     in Phase 2+.</para>
///     <para><b>Cluster linkage.</b> <see cref="ClusterId" /> is the
///     cluster the node belongs to (FK to forge.clusters). The
///     relationship is enforced at the column level only — no navigation
///     property, no DbContext cross-reference (schema-per-module
///     contract).</para>
/// </remarks>
public sealed class NodeRecord : IFilterableEntity, ICreatedAt, IUpdatedAt
{
    /// <summary>Strongly-typed <c>node_&lt;UUIDv7&gt;</c> wire format.</summary>
    public NodeId Id { get; init; }

    /// <summary>Cluster the node belongs to (forge.clusters.id).</summary>
    public ClusterId ClusterId { get; init; }

    /// <summary>Tenant scope (denormalized for org-scoped queries).</summary>
    public Guid OrgId { get; init; }

    /// <summary>
    ///     Self-reported hostname (Plexor.NodeAgent populates on join).
    ///     Unique per cluster — enforced by
    ///     <c>ix_outpost_node_records_cluster_id_hostname</c>.
    /// </summary>
    public string Hostname { get; init; } = string.Empty;

    /// <summary>
    ///     Self-reported IP address (the address the agent wants to be
    ///     reached at for mTLS callbacks / metrics scrape). Stored as a
    ///     string so IPv4 and IPv6 both fit without a column shape
    ///     change.
    /// </summary>
    public string IpAddress { get; init; } = string.Empty;

    /// <summary>Role within the cluster — see <see cref="NodeRole" />.</summary>
    public NodeRole Role { get; init; }

    /// <summary>Lifecycle status — see <see cref="NodeStatus" />.</summary>
    public NodeStatus Status { get; init; }

    /// <summary>
    ///     Hardware snapshot reported at join. Immutable — updated only if
    ///     the agent re-joins after a wipe. Persisted as JSONB (Postgres)
    ///     / JSON string (InMemory).
    /// </summary>
    public NodeSpec Spec { get; init; } = new(0, 0, 0, []);

    /// <summary>
    ///     ISO image version the node was provisioned from
    ///     (e.g. "0.1.0-dev"). Compared against the cluster's
    ///     <c>host_version</c> at every heartbeat.
    /// </summary>
    public string IsoVersion { get; init; } = string.Empty;

    /// <summary>
    ///     Last heartbeat timestamp (UTC). Bumped on every
    ///     <c>POST /api/v1/nodes/heartbeat</c>. Used by the
    ///     <see cref="Abstractions.INodeHeartbeatEvaluator" /> to compute
    ///     <see cref="NodeHealth" />.
    /// </summary>
    public DateTimeOffset? LastHeartbeatAt { get; init; }

    /// <summary>When the node first joined the cluster.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    ///     Last modification time (UTC) — bumped on any field write
    ///     (status change, hardware update, etc.).
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>
    ///     WireGuard public key of the node. Set during the join
    ///     handshake. Workers use it to authenticate the WireGuard
    ///     mesh.
    /// </summary>
    public string WireguardPublicKey { get; init; } = string.Empty;

    /// <summary>
    ///     How many VMs are currently scheduled on this node. Updated
    ///     by the workload runtime on each deploy / undeploy.
    /// </summary>
    public int VmCount { get; init; }
}