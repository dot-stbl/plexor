// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Volume — disk volume attached to a Plexor cluster node. The schema is
// `storage` (architecture theme); the table is `storage.volumes`. One
// row per disk volume. Tenant scoping is via OrgId (denormalized for
// tenant-scoped queries; the canonical Org / Team / Folder hierarchy
// lives in realm.organizations / realm.teams / realm.folders — Volume
// does not FK into them by design, the API layer validates the path).
//
// Volume is the load-bearing entity behind the storage.volumes.count
// and storage.volumes.gb quota keys. Every CreateVolumeHandler must
// call IQuotaEnforcer.CheckAndReserveAsync before INSERT — see
// Plexor.Modules.Quotas.Infrastructure.Quotas.EfQuotaEnforcer.
// ============================================================================

using Plexor.Shared.Filtering.Registry;
using Plexor.Shared.Kernel.Common;

namespace Plexor.Modules.Storage.Domain.Entities;

/// <summary>
///     Disk volume attached to a Plexor cluster node. Backed by the
///     <c>storage.volumes</c> table. Tenant-scoped via
///     <see cref="OrgId" /> (denormalized). Quota-affecting fields:
///     <see cref="SizeGb" /> (drives the storage.volumes.gb counter)
///     and the row itself (drives the storage.volumes.count counter).
/// </summary>
/// <remarks>
///     <para><b>Cluster FK is optional in v0.1.</b> <see cref="ClusterId" />
///     is left as a <see cref="Guid" /> so the entity doesn't take a
///     dependency on the Clusters module's domain types. The API layer
///     validates the cluster exists at write time; the storage module
///     does not enforce a DB-level FK to forge.clusters — same
///     separation-of-concerns pattern as
///     <c>Plexor.Modules.Branding.Domain.Entities.OrgThemeConfig</c>.</para>
///     <para><b>Why no Folder/Team.</b> Plexor's 3-tier scope hierarchy
///     is org → team → folder. Volume lives at the org scope in v0.1
///     (the cluster the volume belongs to is implicitly in one org);
///     folder-scoped volumes are a future extension and would land as
///     a new nullable column.</para>
///     <para><b>Quota accounting.</b> Two fields drive the catalog
///     keys: row count (<c>storage.volumes.count</c>) and
///     <see cref="SizeGb" /> (<c>storage.volumes.gb</c>). The enforcer
///     reads both via <c>IStorageQuotaReader.CountAsync</c> on every
///     CreateVolume path.</para>
/// </remarks>
public sealed class Volume : IFilterableEntity, ICreatedAt, IUpdatedAt
{
    /// <summary>Unique identifier (UUID v7).</summary>
    public Guid Id { get; init; }

    /// <summary>Tenant scope. Denormalized on the row so the
    /// quota enforcer can scope <c>SELECT COUNT(*) FROM volumes WHERE
    /// org_id = ?</c> without joining Realm.</summary>
    public Guid OrgId { get; init; }

    /// <summary>FK to the cluster that hosts the volume. v0.1
    /// does not enforce a DB-level FK — the API layer validates the
    /// cluster exists before INSERT (the storage module does not take
    /// a dependency on Plexor.Modules.Clusters).</summary>
    public Guid ClusterId { get; init; }

    /// <summary>Volume name as it appears in the dashboard +
    /// NodeAgent's runtime. Unique per cluster — enforced by the
    /// <c>ix_storage_volumes_cluster_id_name</c> UNIQUE index.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Size in GiB. Drives the
    /// <c>storage.volumes.gb</c> quota. Whole-number GiB is fine for
    /// v0.1 (Plexor doesn't expose fractional volume sizes yet).
    /// Mutable so the resize path (UpdateVolumeEndpoint → service)
    /// can stamp the new size after the API-layer validation.</summary>
    public int SizeGb { get; set; }

    /// <summary>Lifecycle status — see <see cref="VolumeStatus" />.</summary>
    public VolumeStatus Status { get; set; }

    /// <summary>Row creation time (UTC).</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Last modification time (UTC) — bumped on any
    /// field write (status change, size resize, rename).</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
