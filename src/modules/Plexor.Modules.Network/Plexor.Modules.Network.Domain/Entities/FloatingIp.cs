// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// FloatingIp — public IP attached to a Plexor cluster node. The
// schema is `network` (architecture theme); the table is
// `network.floating_ips`. One row per floating IP.
//
// FloatingIp is the load-bearing entity behind the
// network.floating_ips.count quota key. Every CreateFloatingIp handler
// must call IQuotaEnforcer.CheckAndReserveAsync before INSERT.
// ============================================================================

using Plexor.Shared.Filtering.Registry;
using Plexor.Shared.Kernel.Common;

namespace Plexor.Modules.Network.Domain.Entities;

/// <summary>
///     Floating IP attached to a Plexor cluster node. Backed by the
///     <c>network.floating_ips</c> table. Tenant-scoped via
///     <see cref="OrgId" /> (denormalized). Quota-affecting: drives
///     the <c>network.floating_ips.count</c> counter.
/// </summary>
/// <remarks>
///     <para><b>Cluster FK is optional in v0.1.</b> <see cref="ClusterId" />
///     is left as a <see cref="Guid" /> so the entity doesn't take a
///     dependency on the Clusters module's domain types. The API layer
///     validates the cluster exists at write time; the network module
///     does not enforce a DB-level FK to forge.clusters — same
///     separation-of-concerns pattern as
///     <c>Plexor.Modules.Storage.Domain.Entities.Volume</c>.</para>
///     <para><b>Quota accounting.</b> The row itself drives the
///     catalog key. The enforcer reads the row count via
///     <c>INetworkQuotaReader.CountAsync</c> on every CreateFloatingIp
///     path.</para>
/// </remarks>
public sealed class FloatingIp : IFilterableEntity, ICreatedAt, IUpdatedAt
{
    /// <summary>Unique identifier (UUID v7).</summary>
    public Guid Id { get; init; }

    /// <summary>Tenant scope. Denormalized on the row so the
    /// quota enforcer can scope <c>SELECT COUNT(*) FROM floating_ips
    /// WHERE org_id = ?</c> without joining Realm.</summary>
    public Guid OrgId { get; init; }

    /// <summary>FK to the cluster that hosts the IP. v0.1 does not
    /// enforce a DB-level FK — the API layer validates the cluster
    /// exists before INSERT.</summary>
    public Guid ClusterId { get; init; }

    /// <summary>IP address (IPv4 or IPv6). Validated at the wire
    /// boundary by FluentValidation (must parse as a System.Net
    /// IPAddress).</summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>Lifecycle status — see <see cref="NetworkResourceStatus" />.</summary>
    public NetworkResourceStatus Status { get; set; }

    /// <summary>Row creation time (UTC).</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Last modification time (UTC) — bumped on any field
    /// write (status change, address rotation, cluster re-bind).</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
