// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Cluster aggregate. Plan: .agents/docs/plans/plan-clusters.md.
//
// A Cluster is one Plexor.Host control plane + N Plexor.NodeAgent
// nodes that joined via a join token. Self-hosted: each cluster is
// born from `plx init` (ISO install) on a single host, then
// additional nodes join via the join URL with a one-time token.
// ============================================================================

using Plexor.Shared.Filtering.Registry;
using Plexor.Shared.Identifiers;
using Plexor.Shared.Kernel.Common;

namespace Plexor.Modules.Clusters.Domain.Entities;

/// <summary>
///     Plexor.Host control plane + a set of joined Plexor.NodeAgent
///     nodes. Self-hosted = one cluster per host. Multi-cluster is
///     Phase 7+ (out of scope for the v0.1 MVP).
/// </summary>
public sealed class Cluster : IFilterableEntity, ICreatedAt, IUpdatedAt
{
    /// <summary>
    ///     ClusterId — strongly-typed <c>cluster_&lt;UUIDv7&gt;</c>
    ///     wire format (see <see cref="Plexor.Shared.Identifiers.ClusterId" />).
    /// </summary>
    public ClusterId Id { get; init; }

    /// <summary>Cluster name as it appears in `plx.yaml` /
    /// `plx init` output. Unique per organization
    /// (enforced by `ix_clusters_org_id_name`).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Tenant scope. v0.1 is single-tenant; the FK
    /// exists for the multi-tenant migration in Phase 2+.</summary>
    public Guid OrgId { get; init; }

    /// <summary>Operator-assigned region label
    /// (e.g. "eu-central-1"). Helps the dashboard group clusters
    /// across regions when v0.2+ adds multi-region.</summary>
    public string Region { get; init; } = string.Empty;

    /// <summary>Lifecycle status — see <see cref="ClusterStatus" />.</summary>
    public ClusterStatus Status { get; init; }

    /// <summary>
    ///     WireGuard public key of the host. Populated by the host
    ///     on first boot. Workers use it to authenticate the WireGuard
    ///     mesh during join.
    /// </summary>
    public string WireguardPublicKey { get; init; } = string.Empty;

    /// <summary>When the currently-active join token expires. Null
    /// when no token is in flight (e.g. immediately after rotation
    /// but before issuing the next one).</summary>
    public DateTimeOffset? JoinTokenExpiresAt { get; init; }

    /// <summary>Install providers selected at `plx init`
    /// (kvm, lxc, pod, ovs, cilium, …). Comma-separated display
    /// list on the cluster card.</summary>
    public IReadOnlyList<string> InstallProviders { get; init; } = [];

    /// <summary>Version of the Plexor.Host binary running the
    /// control plane. Verified at every node join — non-matching
    /// versions warn but don't reject (the operator may be in
    /// the middle of an upgrade).</summary>
    public string HostVersion { get; init; } = string.Empty;

    /// <summary>Where the host is reachable (used in
    /// `plx node join &lt;endpoint&gt;`).</summary>
    public string Endpoint { get; init; } = string.Empty;

    /// <summary>Wall-clock the cluster was created (`plx init`).</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Last modification time (UTC) — bumped on any field
    /// write.</summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Wall-clock the host process started.</summary>
    public TimeSpan Uptime { get; init; }

    /// <summary>Nodes that have joined this cluster. Loaded by
    /// the EF query — not part of the row.</summary>
    public IReadOnlyList<Node> Nodes { get; init; } = [];

    /// <summary>Join tokens issued for this cluster. Loaded by
    /// the EF query.</summary>
    public IReadOnlyList<JoinToken> Tokens { get; init; } = [];
}
