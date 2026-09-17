// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// HostClusterContracts — wire-shape DTOs for `plx cluster *` commands.
// Mirrors the host's Plexor.Modules.Clusters.Api + Application types
// (`ClusterSummary`, `ClusterDetail`, `JoinTokenResult`,
// `NodeCounts`, `ClusterStatus`). Declared locally in the CLI so the
// installer never depends on the host's Clusters assemblies — same
// pattern as HostNodeResponse.cs / HostRegisterNodeContracts.cs.
//
// AOT: Refit 12.x + System.Text.Json source-gen handles record types
// with init-only string/DateTimeOffset/nullable primitives without
// reflection. No [JsonPropertyName] overrides — the host's JSON keys
// match C# PascalCase property names via the framework's default
// case-insensitive matching.
//
// ClusterId / NodeId wire formats (`cluster_<uuidv7>`,
// `node_<uuidv7>`) are passed as plain strings to keep this file free
// of a dependency on Plexor.Shared.Identifiers.
// ============================================================================

namespace Plexor.Installer.Cli.Refit;

/// <summary>Wire shape for <c>GET /api/v1/compute/clusters</c> entries.</summary>
/// <param name="Id">Cluster id (wire string, e.g. <c>cluster_&lt;UUIDv7&gt;</c>).</param>
/// <param name="OrgId">Tenant scope.</param>
/// <param name="Name">Cluster name.</param>
/// <param name="Region">Operator-assigned region label.</param>
/// <param name="Status">Lifecycle status (Pending / Provisioning / Ready / Degraded / Offline).</param>
/// <param name="Endpoint">Where the host is reachable.</param>
/// <param name="HostVersion">Plexor.Host binary version.</param>
/// <param name="RuntimeId">
///     Cluster-level runtime (<c>docker-compose</c> / <c>podman-quadlet</c> /
///     <c>k3s</c>). Immutable after creation.
/// </param>
/// <param name="NodeCounts">Aggregated node counts by status.</param>
/// <param name="CreatedAt">Cluster creation time (UTC).</param>
/// <param name="UpdatedAt">Last modification time (UTC).</param>
public sealed record HostClusterSummary(
    string Id,
    Guid OrgId,
    string Name,
    string Region,
    HostClusterStatus Status,
    string Endpoint,
    string HostVersion,
    string RuntimeId,
    HostNodeCounts NodeCounts,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
///     Wire shape for <c>GET /api/v1/compute/clusters/{id}</c> — the
///     single-cluster detail with embedded child nodes.
/// </summary>
/// <param name="Id">Cluster id.</param>
/// <param name="OrgId">Tenant scope.</param>
/// <param name="Name">Cluster name.</param>
/// <param name="Region">Region label.</param>
/// <param name="Status">Lifecycle status.</param>
/// <param name="Endpoint">Host reachability URL.</param>
/// <param name="HostVersion">Host binary version.</param>
/// <param name="RuntimeId">Cluster-level runtime identifier.</param>
/// <param name="InstallProviders">Providers selected at <c>plx init</c>.</param>
/// <param name="WireguardPublicKey">Host WireGuard public key.</param>
/// <param name="JoinTokenExpiresAt">When the active join token expires, null if none.</param>
/// <param name="Nodes">Child nodes (empty if none joined).</param>
/// <param name="CreatedAt">Creation time (UTC).</param>
/// <param name="UpdatedAt">Last modification time (UTC).</param>
public sealed record HostClusterDetail(
    string Id,
    Guid OrgId,
    string Name,
    string Region,
    HostClusterStatus Status,
    string Endpoint,
    string HostVersion,
    string RuntimeId,
    IReadOnlyList<string> InstallProviders,
    string WireguardPublicKey,
    DateTimeOffset? JoinTokenExpiresAt,
    IReadOnlyList<HostClusterNodeSummary> Nodes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
///     Lightweight child-node projection embedded in
///     <see cref="HostClusterDetail.Nodes" />.
/// </summary>
/// <param name="Id">Node id (wire string).</param>
/// <param name="Hostname">OS-reported hostname.</param>
/// <param name="Role">Node role (Control / Compute).</param>
/// <param name="Status">Persisted lifecycle status (numeric, host-stable).</param>
public sealed record HostClusterNodeSummary(
    string Id,
    string Hostname,
    HostNodeRole Role,
    int Status);

/// <summary>
///     One-shot join token — returned by create + rotate. The token is
///     sensitive (shown once by the CLI); <c>Endpoint</c> is the
///     post-join rendezvous (mTLS + WireGuard).
/// </summary>
/// <param name="ClusterId">Cluster the token belongs to.</param>
/// <param name="Token">Opaque JWT-format token (signed by the host).</param>
/// <param name="ExpiresAt">When the token expires (UTC).</param>
/// <param name="Endpoint">Where <c>plx node join</c> should POST.</param>
public sealed record HostJoinTokenResult(
    string ClusterId,
    string Token,
    DateTimeOffset ExpiresAt,
    string Endpoint);

/// <summary>
///     Aggregated node counts by lifecycle status. Returned in the
///     <see cref="HostClusterSummary.NodeCounts" /> slot.
/// </summary>
/// <param name="Total">Total nodes across all statuses.</param>
/// <param name="Ready">Nodes in <c>NodeStatus.Ready</c>.</param>
/// <param name="Pending">Nodes in <c>NodeStatus.Pending</c>.</param>
/// <param name="Offline">Nodes in <c>NodeStatus.Gone</c>.</param>
/// <param name="Draining">Nodes in <c>NodeStatus.Draining</c>.</param>
public sealed record HostNodeCounts(
    int Total,
    int Ready,
    int Pending,
    int Offline,
    int Draining);

/// <summary>
///     Lifecycle status of a cluster. Mirrors
///     <c>Plexor.Modules.Clusters.Domain.ClusterStatus</c>.
/// </summary>
public enum HostClusterStatus
{
    /// <summary>Cluster row exists, no node has joined yet.</summary>
    Pending = 0,

    /// <summary>One or more nodes have joined and are heartbeating.</summary>
    Provisioning = 1,

    /// <summary>All joined nodes are Ready; cluster is eligible for scheduling.</summary>
    Ready = 2,

    /// <summary>Some nodes Ready but at least one Offline (dashboard warning).</summary>
    Degraded = 3,

    /// <summary>No node has reported a heartbeat in the last 90 s.</summary>
    Offline = 4,
}