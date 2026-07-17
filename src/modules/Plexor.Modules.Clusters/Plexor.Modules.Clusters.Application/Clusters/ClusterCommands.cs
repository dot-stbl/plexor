// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Cluster CRUD commands + queries + projections. Application layer
// carries the wire shapes; handlers in Infrastructure run the EF
// queries. Mirrors the Sigil module's UserCommand pattern.
// ============================================================================

using Plexor.Modules.Clusters.Domain;
using Plexor.Modules.Clusters.Domain.Entities;
using Plexor.Shared.Filtering.Query;
using Plexor.Shared.Identifiers;
using Plexor.Shared.NodeApi;

namespace Plexor.Modules.Clusters.Application.Clusters;

// --- commands ---------------------------------------------------------------

/// <summary>
///     Provision a new cluster. Admin-only — caller must hold
///     <see cref="Authorization.ClusterPermissions.Create" />. Emits
///     a fresh join token good for the first NodeAgent to redeem.
/// </summary>
/// <param name="OrgId">Tenant scope.</param>
/// <param name="Name">Cluster name (unique per org).</param>
/// <param name="Region">Operator-assigned region label (e.g. <c>eu-central-1</c>).</param>
/// <param name="InitialNodeRole">Role the first joining node will take.</param>
/// <param name="RuntimeId">
///     Cluster-level runtime the cluster will use
///     (<c>docker-compose</c> / <c>podman-quadlet</c> / <c>k3s</c>).
///     Defaults to <see cref="Plexor.Shared.NodeApi.ClusterRuntimeIds.DockerCompose" />.
///     Immutable after creation — switching runtime requires recreating
///     the cluster.
/// </param>
public sealed record CreateClusterCommand(
    Guid OrgId,
    string Name,
    string Region,
    NodeRole InitialNodeRole,
    string RuntimeId = ClusterRuntimeIds.Default);

/// <summary>
///     Rename / change region for an existing cluster. Cannot change
///     <c>OrgId</c> (cross-tenant migration is Phase 2+).
/// </summary>
/// <param name="ClusterId">Target cluster.</param>
/// <param name="Name">New name (null = leave unchanged).</param>
/// <param name="Region">New region (null = leave unchanged).</param>
public sealed record UpdateClusterCommand(
    ClusterId ClusterId,
    string? Name,
    string? Region);

/// <summary>
///     Soft-delete a cluster. Cascades <c>Node.Status = Gone</c> on
///     every child node; emits <c>ClusterDeleted</c>. The row stays
///     in <c>forge.clusters</c> for audit + FK integrity.
/// </summary>
/// <param name="ClusterId">Target cluster.</param>
public sealed record DeleteClusterCommand(ClusterId ClusterId);

/// <summary>
///     Rotate the cluster's join token. Old token is revoked; the new
///     one is returned with a 7-day TTL. Used by the join-landing
///     page in the console.
/// </summary>
/// <param name="ClusterId">Target cluster.</param>
public sealed record RotateJoinTokenCommand(ClusterId ClusterId);

/// <summary>Fetch one cluster by id.</summary>
/// <param name="ClusterId">Target cluster.</param>
public sealed record GetClusterQuery(ClusterId ClusterId);

/// <summary>List clusters in the caller's org, paged + filtered
/// via the standard <see cref="FilterQuery" /> URL envelope
/// (<c>filter=...</c>, <c>sort=...</c>, <c>page=...</c>, <c>pageSize=...</c>).</summary>
/// <param name="OrgId">Tenant scope.</param>
/// <param name="Query">URL envelope (filter DSL + sort criteria +
/// paging).</param>
public sealed record ListClustersQuery(
    Guid OrgId,
    FilterQuery Query);

// --- projections ------------------------------------------------------------

/// <summary>Public projection of <see cref="Cluster" /> — list-card shape.
/// <c>sealed partial class</c> with init-only properties so
/// Mapperly's source generator can emit the mapping body
/// (see <c>ClusterMappers.ToSummary</c>). EF Core's
/// <c>Select(... new X { Prop = ... })</c> translates cleanly via the
/// object-initializer syntax.</summary>
public sealed partial class ClusterSummary
{
    /// <summary>Cluster id (cluster_&lt;UUIDv7&gt;).</summary>
    public ClusterId Id { get; init; }

    /// <summary>Tenant scope.</summary>
    public Guid OrgId { get; init; }

    /// <summary>Cluster name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Operator-assigned region.</summary>
    public string Region { get; init; } = string.Empty;

    /// <summary>Lifecycle status.</summary>
    public ClusterStatus Status { get; init; }

    /// <summary>Where the host is reachable.</summary>
    public string Endpoint { get; init; } = string.Empty;

    /// <summary>Plexor.Host binary version.</summary>
    public string HostVersion { get; init; } = string.Empty;

    /// <summary>
    ///     Cluster-level runtime identifier
    ///     (<c>docker-compose</c> / <c>podman-quadlet</c> / <c>k3s</c>).
    ///     Immutable after creation. The UI uses this to drive the
    ///     workload-create wizard's runtime hints.
    /// </summary>
    public string RuntimeId { get; init; } = ClusterRuntimeIds.Default;

    /// <summary>Aggregated node counts by status.</summary>
    public NodeCounts NodeCounts { get; init; } = new();

    /// <summary>Cluster creation time (UTC).</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Last modification time (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>Single-cluster shape with embedded nodes.
/// <c>sealed partial class</c> with init-only properties.</summary>
public sealed partial class ClusterDetail
{
    /// <summary>Cluster id.</summary>
    public ClusterId Id { get; init; }

    /// <summary>Tenant scope.</summary>
    public Guid OrgId { get; init; }

    /// <summary>Cluster name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Region label.</summary>
    public string Region { get; init; } = string.Empty;

    /// <summary>Lifecycle status.</summary>
    public ClusterStatus Status { get; init; }

    /// <summary>Host reachability URL.</summary>
    public string Endpoint { get; init; } = string.Empty;

    /// <summary>Host binary version.</summary>
    public string HostVersion { get; init; } = string.Empty;

    /// <summary>
    ///     Cluster-level runtime identifier
    ///     (<c>docker-compose</c> / <c>podman-quadlet</c> / <c>k3s</c>).
    ///     Immutable after creation.
    /// </summary>
    public string RuntimeId { get; init; } = ClusterRuntimeIds.Default;

    /// <summary>Install providers selected at <c>plx init</c>.</summary>
    public IReadOnlyList<string> InstallProviders { get; init; } = [];

    /// <summary>Host WireGuard public key.</summary>
    public string WireguardPublicKey { get; init; } = string.Empty;

    /// <summary>When the active join token expires, null if none.</summary>
    public DateTimeOffset? JoinTokenExpiresAt { get; init; }

    /// <summary>Child nodes (empty if none joined).</summary>
    public IReadOnlyList<NodeSummary> Nodes { get; init; } = [];

    /// <summary>Creation time (UTC).</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Last modification time (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>One-shot join token response — returned by create + rotate.</summary>
/// <param name="ClusterId">Cluster the token belongs to.</param>
/// <param name="Token">Opaque JWT-format token (signed by the host).</param>
/// <param name="ExpiresAt">When the token expires (UTC).</param>
/// <param name="Endpoint">Where <c>plx node join</c> should POST.</param>
public sealed record JoinTokenResult(
    ClusterId ClusterId,
    string Token,
    DateTimeOffset ExpiresAt,
    string Endpoint);

// Paged list response uses Plexor.Shared.Contracts.Pagination.PageResult<T>
// directly — no project-specific wrapper.
