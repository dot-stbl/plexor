// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// LoadBalancer — L4/L7 load balancer. The schema is `network`
// (architecture theme); the table is `network.load_balancers`. One
// row per LB.
//
// LoadBalancer is the load-bearing entity behind the
// network.load_balancers.count quota key. Every CreateLoadBalancer
// handler must call IQuotaEnforcer.CheckAndReserveAsync before INSERT.
// ============================================================================

using Plexor.Shared.Filtering.Registry;
using Plexor.Shared.Kernel.Common;

namespace Plexor.Modules.Network.Domain.Entities;

/// <summary>
///     Load balancer (L4 or L7) attached to a Plexor cluster. Backed
///     by the <c>network.load_balancers</c> table. Tenant-scoped via
///     <see cref="OrgId" /> (denormalized). Quota-affecting: drives
///     the <c>network.load_balancers.count</c> counter.
/// </summary>
/// <remarks>
///     <para><b>Cluster FK is optional in v0.1.</b> Same separation-
///     of-concerns pattern as <see cref="FloatingIp" /> and
///     <c>Plexor.Modules.Storage.Domain.Entities.Volume</c>.</para>
///     <para><b>Backend list.</b> The list of backend nodes the LB
///     forwards to lives out-of-band (the NodeAgent reports them
///     through a separate endpoint); v0.1 just records the LB's
///     identity + scheduling choice. A future schema migration adds
///     the join table.</para>
/// </remarks>
public sealed class LoadBalancer : IFilterableEntity, ICreatedAt, IUpdatedAt
{
    /// <summary>Unique identifier (UUID v7).</summary>
    public Guid Id { get; init; }

    /// <summary>Tenant scope. Denormalized on the row so the
    /// quota enforcer can scope <c>SELECT COUNT(*) FROM load_balancers
    /// WHERE org_id = ?</c> without joining Realm.</summary>
    public Guid OrgId { get; init; }

    /// <summary>FK to the cluster that hosts the LB. v0.1 does not
    /// enforce a DB-level FK — the API layer validates the cluster
    /// exists before INSERT.</summary>
    public Guid ClusterId { get; init; }

    /// <summary>Load balancer name. Unique per cluster — enforced
    /// by the <c>ix_network_load_balancers_cluster_id_name</c> UNIQUE
    /// index.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Layer — see <see cref="LoadBalancerType" />.</summary>
    public LoadBalancerType Type { get; set; }

    /// <summary>Scheduling algorithm — see <see cref="LoadBalancerAlgorithm" />.</summary>
    public LoadBalancerAlgorithm Algorithm { get; set; }

    /// <summary>Lifecycle status — see <see cref="NetworkResourceStatus" />.</summary>
    public NetworkResourceStatus Status { get; set; }

    /// <summary>Row creation time (UTC).</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Last modification time (UTC) — bumped on any field
    /// write (status change, algorithm change, rename).</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
