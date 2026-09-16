// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodeCommands — Outpost command/query contracts.
//
// Outpost owns four operator-facing flows:
//   * RegisterNodeCommand — NodeAgent redeems a join token; the host
//     creates the NodeRecord row + returns a node-bearer token.
//   * HeartbeatCommand — periodic keepalive from a joined node; the
//     host stamps LastHeartbeatAt + flips Status to Ready.
//   * ListNodesQuery — read surface for the dashboard (filtered by
//     cluster id).
//   * GetNodeQuery — read one node by id (used by GET /nodes/{id}/health).
//
// All commands + results are immutable records. The handler lives in
// Outpost.Infrastructure; the controller resolves it via the
// ICommandHandler<TCommand, TResult> interface in
// Plexor.Modules.Outpost.Application.Abstractions.
// ============================================================================

using Plexor.Modules.Clusters.Domain;
using Plexor.Shared.Identifiers;

namespace Plexor.Modules.Outpost.Application.NodeCommands;

/// <summary>
///     First-call payload from a Plexor.NodeAgent redeeming a join
///     token. Anonymous at the HTTP layer (the join token itself is
///     the credential). On success the host creates a
///     <see cref="NodeRecord" /> row and returns a node-bearer token +
///     WireGuard config.
/// </summary>
/// <param name="JoinToken">Opaque JWT-format token from <c>RotateJoinToken</c>.</param>
/// <param name="Hostname">OS-reported hostname (operator-verifiable).</param>
/// <param name="IpAddress">Address the agent wants to be reached at.</param>
/// <param name="Role">Requested role (must match a role the cluster accepts).</param>
/// <param name="Spec">Hardware snapshot probed at boot.</param>
/// <param name="IsoVersion">ISO image version the agent booted from.</param>
/// <param name="WireguardPublicKey">WireGuard public key (empty for non-mesh v0.1).</param>
public sealed record RegisterNodeCommand(
    string JoinToken,
    string Hostname,
    string IpAddress,
    NodeRole Role,
    NodeSpec Spec,
    string IsoVersion,
    string WireguardPublicKey);

/// <summary>
///     Result of a successful <see cref="RegisterNodeCommand" />. The
///     NodeAgent persists these and uses them on every subsequent
///     heartbeat + command-poll call.
/// </summary>
/// <param name="NodeRecord">The freshly-minted node row.</param>
/// <param name="NodeToken">Node-bearer token (proves this node's identity on every subsequent call).</param>
/// <param name="ClusterEndpoint">Post-join rendezvous point (mTLS + WireGuard).</param>
public sealed record RegisterNodeResult(
    NodeRecord NodeRecord,
    string NodeToken,
    string ClusterEndpoint);

/// <summary>
///     Periodic keepalive from a joined node. Stamps
///     <see cref="NodeRecord.LastHeartbeatAt" /> + refreshes
///     <see cref="NodeRecord.IpAddress" />. Status flip is handled by
///     the handler (Ready when the cluster is up; preserved when
///     Offline / Draining).
/// </summary>
/// <param name="NodeId">Caller's own node id (from the node-bearer token).</param>
/// <param name="ClusterId">Cluster the node belongs to.</param>
/// <param name="Spec">Fresh hardware snapshot (may drift over time).</param>
/// <param name="IpAddress">Refreshed IP address (DHCP / VPN changes).</param>
public sealed record HeartbeatCommand(
    NodeId NodeId,
    ClusterId ClusterId,
    NodeSpec Spec,
    string IpAddress);

/// <summary>Ack from a successful heartbeat. Mirrors the v0.1
/// <c>forge.nodes</c> response shape so the NodeAgent's existing
/// parser doesn't need changes.</summary>
/// <param name="NodeId">Echo of the caller's node id.</param>
/// <param name="ClusterStatus">Cluster's current status — drives NodeAgent
/// behaviour (e.g. <c>Offline</c> ⇒ NodeAgent should drain + exit).</param>
/// <param name="ServerTime">Host's UTC now — NodeAgent uses this for clock-skew checks.</param>
public sealed record HeartbeatResult(
    NodeId NodeId,
    ClusterStatus ClusterStatus,
    DateTimeOffset ServerTime);

/// <summary>List nodes in one cluster (read surface for the dashboard).</summary>
/// <param name="ClusterId">Target cluster.</param>
public sealed record ListNodesQuery(ClusterId ClusterId);

/// <summary>Get one node by id.</summary>
/// <param name="NodeId">Target node.</param>
public sealed record GetNodeQuery(NodeId NodeId);

// --- projections --------------------------------------------------------

/// <summary>Public projection of <see cref="NodeRecord" />. Wire shape
/// for <c>GET /api/v1/nodes/{id}</c>.</summary>
public sealed class NodeSummary
{
    /// <summary>Node id (node_&lt;UUIDv7&gt;).</summary>
    public NodeId Id { get; init; }

    /// <summary>Parent cluster.</summary>
    public ClusterId ClusterId { get; init; }

    /// <summary>Tenant scope (denormalized for org-scoped queries).</summary>
    public Guid OrgId { get; init; }

    /// <summary>OS-reported hostname.</summary>
    public string Hostname { get; init; } = string.Empty;

    /// <summary>Address the agent wants to be reached at.</summary>
    public string IpAddress { get; init; } = string.Empty;

    /// <summary>Role within the cluster.</summary>
    public NodeRole Role { get; init; }

    /// <summary>Lifecycle status.</summary>
    public NodeStatus Status { get; init; }

    /// <summary>Hardware snapshot.</summary>
    public NodeSpec Spec { get; init; } = new(0, 0, 0, []);

    /// <summary>Last keepalive timestamp (UTC), null if never.</summary>
    public DateTimeOffset? LastHeartbeatAt { get; init; }

    /// <summary>Node creation time (UTC).</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Last modification time (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}