// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// INodeRegistry — the host-side node registry port.
//
// The Outpost module owns the write/read surface for nodes that have
// joined a Plexor.Host (NodeAgent → Host via mTLS). The Application
// layer defines the port; the Infrastructure layer provides the EF
// implementation in OutpostDbContext.
//
// Why a port + interface here:
//   * Keeps the controller decoupled from EF (handler tests resolve a
//     NSubstitute proxy of the port, no DbContext needed).
//   * Keeps the read API (`GetAsync`, `ListAsync`) discoverable from
//     the controller without traversing DbContext internals.
// ============================================================================

using Plexor.Modules.Clusters.Domain;
using Plexor.Modules.Outpost.Application;
using Plexor.Shared.Identifiers;

namespace Plexor.Modules.Outpost.Application.Abstractions;

/// <summary>
///     Host-side registry of joined Plexor.NodeAgent instances.
///     Implementation lives in Outpost.Infrastructure over
///     <c>OutpostDbContext</c> + a per-entity EF Repository.
/// </summary>
public interface INodeRegistry
{
    /// <summary>
    ///     Register a node (called from the join flow). Looks up the
    ///     join token, validates it, creates the <see cref="NodeRecord" />
    ///     row, and returns the freshly-minted node id + the
    ///     <c>nodeToken</c> the agent will use on every subsequent
    ///     heartbeat / command-poll call.
    /// </summary>
    /// <param name="joinToken">One-time token issued by Clusters / RotateJoinToken.</param>
    /// <param name="hostname">OS-reported hostname (operator-verifiable).</param>
    /// <param name="ipAddress">
    ///     Address the agent wants to be reached at (used by the
    ///     dashboard + future mesh routing).
    /// </param>
    /// <param name="role">Requested role (must match the token's intended role).</param>
    /// <param name="spec">Hardware snapshot probed at boot.</param>
    /// <param name="isoVersion">ISO image version the agent booted from.</param>
    /// <param name="wireguardPublicKey">
    ///     WireGuard public key of the node (empty for non-mesh v0.1).
    /// </param>
    /// <param name="cancellationToken">Forwarded to the DB writes.</param>
    /// <returns>The new node's id + the long-lived node-bearer token.</returns>
    public Task<NodeRecord> RegisterAsync(
        string joinToken,
        string hostname,
        string ipAddress,
        NodeRole role,
        NodeSpec spec,
        string isoVersion,
        string wireguardPublicKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Stamp a node's heartbeat. Updates <c>LastHeartbeatAt</c>,
    ///     bumps <c>Spec</c> to the freshest snapshot, and re-evaluates
    ///     <see cref="NodeStatus" /> (Ready when the cluster is up;
    ///     preserved when the cluster is Offline / the node is Draining).
    /// </summary>
    /// <param name="nodeId">Caller's own node id (from the node-bearer token).</param>
    /// <param name="clusterId">Cluster the node belongs to.</param>
    /// <param name="spec">Fresh hardware snapshot (may drift over time).</param>
    /// <param name="ipAddress">Refreshed IP address (DHCP / VPN changes).</param>
    /// <param name="cancellationToken">Forwarded to the DB writes.</param>
    /// <returns>The updated node row.</returns>
    public Task<NodeRecord> HeartbeatAsync(
        NodeId nodeId,
        ClusterId clusterId,
        NodeSpec spec,
        string ipAddress,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get one node by id (any cluster).
    /// </summary>
    /// <param name="nodeId">Target node id.</param>
    /// <param name="cancellationToken">Forwarded to the DB read.</param>
    /// <returns>The node row, or <c>null</c> when no row matches.</returns>
    public Task<NodeRecord?> GetNodeAsync(
        NodeId nodeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     List nodes in one cluster, ordered by creation time desc.
    /// </summary>
    /// <param name="clusterId">Filter to this cluster.</param>
    /// <param name="cancellationToken">Forwarded to the DB read.</param>
    /// <returns>Read-only collection; empty when the cluster has no nodes yet.</returns>
    public Task<IReadOnlyList<NodeRecord>> ListNodesAsync(
        ClusterId clusterId,
        CancellationToken cancellationToken = default);
}