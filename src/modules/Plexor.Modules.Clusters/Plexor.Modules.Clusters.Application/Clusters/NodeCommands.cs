// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Node join + heartbeat + list commands. These are the NodeAgent-
// facing surface: <c>POST /join</c> (anonymous, mTLS), <c>POST /heartbeat</c>
// (node-bearer-token auth).
// ============================================================================

using Plexor.Modules.Clusters.Domain;
using Plexor.Shared.Identifiers;
using Plexor.Shared.NodeApi;

namespace Plexor.Modules.Clusters.Application.Clusters;

/// <summary>
///     First-call payload from a NodeAgent redeeming a join token.
///     Anonymous at the controller layer — the join token itself is
///     the credential. On success the host creates a
///     <see cref="Node" /> row and returns a
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
///     <see cref="Node.LastHeartbeatAt" /> and flips
///     <see cref="Node.Status" /> to
///     <see cref="NodeStatus.Ready" />. Returns 401 + node-removal
///     signal if the cluster has been disabled. Phase D Tier 4:
///     <see cref="Reports" /> drives drift detection — the control
///     plane reconciles each report against its durable
///     <c>forge.workloads</c> view.
/// </summary>
/// <param name="NodeId">Caller's own node id (from the node-bearer token).</param>
/// <param name="ClusterId">Cluster the node belongs to.</param>
/// <param name="Hardware">Fresh hardware snapshot (may drift over time).</param>
/// <param name="Reports">
///     Per-workload state reports (Phase D Tier 4).
///     Empty when the node hasn't provisioned any workloads
///     yet. The handler reconciles each against
///     <c>forge.workloads</c> by (cluster, node, name) — unmatched
///     reports throw <c>ClustersException.WorkloadNotFound</c>
///     (drift detected: a provider is running something we never
///     provisioned).
/// </param>
public sealed record NodeHeartbeatCommand(
    NodeId NodeId,
    ClusterId ClusterId,
    NodeSpec Hardware,
    IReadOnlyList<WorkloadReport> Reports);

/// <summary>List nodes in one cluster.</summary>
/// <param name="ClusterId">Target cluster.</param>
public sealed record ListNodesQuery(ClusterId ClusterId);

// --- projections ------------------------------------------------------------

/// <summary>Public projection of <see cref="Node" />.
/// <c>sealed partial class</c> with init-only properties for
/// Mapperly source-generation compatibility (see
/// <c>ClusterMappers.ToNodeSummary</c>).</summary>
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
/// <para>
///     Result of a successful <see cref="NodeJoinCommand" />. The
///     NodeAgent persists these and uses them for every subsequent
///     <c>/heartbeat</c> + <c>/commands/poll</c> call.
/// </para>
/// <para>
///     The mTLS triple (<see cref="NodeCertificatePem" /> /
///     <see cref="NodePrivateKeyPem" /> / <see cref="CaCertificatePem" />)
///     is issued by the host on every successful join — the
///     NodeAgent writes the cert + key to disk and uses them as
///     the client cert on every subsequent HTTPS call. The CA
///     certificate is included so the NodeAgent can verify the
///     host's server cert (mutual trust at the TLS layer).
/// </para>
/// </summary>
/// <param name="NodeId">Newly-minted node id.</param>
/// <param name="ClusterId">Cluster the node joined.</param>
/// <param name="NodeToken">Node-bearer token (proves this node's identity).</param>
/// <param name="ControlPlaneUrl">Post-join rendezvous point (mTLS + WireGuard).</param>
/// <param name="WireguardConfig">Base64-encoded <c>wg-quick.conf</c> blob.</param>
/// <param name="NodeCertificatePem">
///     PEM-encoded client cert (CN=node_&lt;id&gt;,
///     EKU=ClientAuth, signed by the Plexor CA). NodeAgent
///     presents this on every mTLS call.
/// </param>
/// <param name="NodePrivateKeyPem">
///     PKCS#8 PEM-encoded private key for the client cert.
///     NodeAgent persists with mode 0600.
/// </param>
/// <param name="CaCertificatePem">
///     PEM-encoded Plexor CA root. NodeAgent pins this when
///     validating the host's server cert.
/// </param>
public sealed record NodeJoinResult(
    NodeId NodeId,
    ClusterId ClusterId,
    string NodeToken,
    string ControlPlaneUrl,
    string WireguardConfig,
    string NodeCertificatePem,
    string NodePrivateKeyPem,
    string CaCertificatePem);

/// <summary>Ack from a successful heartbeat.</summary>
/// <param name="NodeId">Echo of the caller's node id.</param>
/// <param name="ClusterStatus">Cluster's current status — drives NodeAgent
/// behaviour (e.g. <c>Offline</c> ⇒ NodeAgent should drain + exit).</param>
/// <param name="ServerTime">Host's UTC now — NodeAgent uses this for clock-skew checks.</param>
public sealed record NodeHeartbeatResult(
    NodeId NodeId,
    ClusterStatus ClusterStatus,
    DateTimeOffset ServerTime);
