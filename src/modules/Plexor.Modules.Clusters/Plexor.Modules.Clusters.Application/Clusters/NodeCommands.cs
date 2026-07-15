// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Node join + heartbeat + list commands. These are the NodeAgent-
// facing surface: <c>POST /join</c> (anonymous, mTLS), <c>POST /heartbeat</c>
// (node-bearer-token auth).
// ============================================================================

using Plexor.Modules.Clusters.Domain;
using Plexor.Modules.Clusters.Domain.Entities;

namespace Plexor.Modules.Clusters.Application.Clusters;

/// <summary>
///     First-call payload from a NodeAgent redeeming a join token.
///     Anonymous at the controller layer — the join token itself is
///     the credential. On success the host creates a
///     <see cref="Domain.Entities.Node" /> row and returns a
///     node-bearer token + WireGuard config.
/// </summary>
/// <param name="JoinToken">Opaque JWT-format token from <see cref="RotateJoinTokenCommand" />.</param>
/// <param name="Hostname">OS-reported hostname (operator-verifiable).</param>
/// <param name="Role">Requested role (must match a role the cluster accepts).</param>
/// <param name="Hardware">Hardware snapshot probed at boot.</param>
public sealed record NodeJoinCommand(
    string JoinToken,
    string Hostname,
    NodeRole Role,
    NodeSpec Hardware);

/// <summary>
///     Periodic keepalive from a joined node. Stamps
///     <see cref="Domain.Entities.Node.LastHeartbeatAt" /> and flips
///     <see cref="Domain.Entities.Node.Status" /> to
///     <see cref="NodeStatus.Ready" />. Returns 401 + node-removal
///     signal if the cluster has been disabled.
/// </summary>
/// <param name="NodeId">Caller's own node id (from the node-bearer token).</param>
/// <param name="ClusterId">Cluster the node belongs to.</param>
/// <param name="Hardware">Fresh hardware snapshot (may drift over time).</param>
public sealed record NodeHeartbeatCommand(
    Guid NodeId,
    Guid ClusterId,
    NodeSpec Hardware);

/// <summary>List nodes in one cluster.</summary>
/// <param name="ClusterId">Target cluster.</param>
public sealed record ListNodesQuery(Guid ClusterId);

// --- projections ------------------------------------------------------------

/// <summary>Public projection of <see cref="Domain.Entities.Node" />.
/// <c>sealed partial class</c> with init-only properties for
/// Mapperly source-generation compatibility (see
/// <c>ClusterMappers.ToNodeSummary</c>).</summary>
public sealed partial class NodeSummary
{
    /// <summary>UUID v7 node id.</summary>
    public Guid Id { get; init; }

    /// <summary>Parent cluster.</summary>
    public Guid ClusterId { get; init; }

    /// <summary>Tenant scope (denormalized for org-scoped queries).</summary>
    public Guid OrgId { get; init; }

    /// <summary>OS-reported hostname.</summary>
    public string Hostname { get; init; } = string.Empty;

    /// <summary>Role within the cluster.</summary>
    public NodeRole Role { get; init; }

    /// <summary>Lifecycle status.</summary>
    public NodeStatus Status { get; init; }

    /// <summary>Hardware snapshot.</summary>
    public NodeSpec Hardware { get; init; } = new(0, 0, 0, []);

    /// <summary>Last keepalive timestamp (UTC), null if never.</summary>
    public DateTimeOffset? LastHeartbeatAt { get; init; }

    /// <summary>Node creation time (UTC).</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Last modification time (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>
///     Result of a successful <see cref="NodeJoinCommand" />. The
///     NodeAgent persists these and uses them for every subsequent
///     <c>/heartbeat</c> + <c>/commands/poll</c> call.
/// </summary>
/// <param name="NodeId">Newly-minted node id.</param>
/// <param name="ClusterId">Cluster the node joined.</param>
/// <param name="NodeToken">Node-bearer token (proves this node's identity).</param>
/// <param name="ControlPlaneUrl">Post-join rendezvous point (mTLS + WireGuard).</param>
/// <param name="WireguardConfig">Base64-encoded <c>wg-quick.conf</c> blob.</param>
public sealed record NodeJoinResult(
    Guid NodeId,
    Guid ClusterId,
    string NodeToken,
    string ControlPlaneUrl,
    string WireguardConfig);

/// <summary>Ack from a successful heartbeat.</summary>
/// <param name="NodeId">Echo of the caller's node id.</param>
/// <param name="ClusterStatus">Cluster's current status — drives NodeAgent
/// behaviour (e.g. <c>Offline</c> ⇒ NodeAgent should drain + exit).</param>
/// <param name="ServerTime">Host's UTC now — NodeAgent uses this for clock-skew checks.</param>
public sealed record NodeHeartbeatResult(
    Guid NodeId,
    ClusterStatus ClusterStatus,
    DateTimeOffset ServerTime);
