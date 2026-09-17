// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IStorageQuotaReader — read-only seam over Plexor.Modules.Storage for
// the Quotas module's EfQuotaEnforcer. The enforcer needs to know
// "current volume count" + "current GiB total" per org on every
// CreateVolume path; this seam gives it that without the enforcer
// taking a project reference to Plexor.Modules.Storage.Infrastructure
// (which would break Law 3 — cross-module coupling).
//
// Mirrors IOrgAuthProviderConfigReader (Realm → Sigil): the owning
// module (Storage) defines the seam in its Application layer; the
// consumer (Quotas.Infrastructure) implements it from Storage.Infrastructure.
//
// Lifetime: Scoped (mirrors StorageDbContext — reads share the
// per-request scope so the enforcer's advisory lock + INSERT +
// storage counter SELECT all ride one physical connection).
// ============================================================================

using Plexor.Modules.Storage.Domain.Projections;

namespace Plexor.Modules.Storage.Application.Storage;

/// <summary>
///     Read-only access to <see cref="Plexor.Modules.Storage.Domain.Projections.StorageOrgScopedCounters" />
///     for callers outside the Storage module. The Quotas module's
///     <c>EfQuotaEnforcer.CheckAndReserveAsync</c> resolves this
///     interface per-call to enforce the
///     <c>storage.volumes.count</c> and <c>storage.volumes.gb</c>
///     quota keys.
/// </summary>
public interface IStorageQuotaReader
{
    /// <summary>
    ///     Count the volume rows + sum the cumulative GiB for a given
    ///     organization. Returns a default-initialised
    ///     <see cref="StorageOrgScopedCounters" /> when no rows exist
    ///     (a brand-new org with zero volumes).
    /// </summary>
    /// <param name="orgId">Tenant scope to count.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    public Task<StorageOrgScopedCounters> CountAsync(
        Guid orgId,
        CancellationToken cancellationToken = default);
}
