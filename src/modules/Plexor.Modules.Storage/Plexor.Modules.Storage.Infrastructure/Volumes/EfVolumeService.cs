// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EfVolumeService — EF-backed IVolumeService. Read + write surface for
// the volumes table. AsNoTracking on reads; scoped lifetime (matches
// the per-request DbContext).
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Storage.Application.Volumes;
using Plexor.Modules.Storage.Domain.Entities;
using Plexor.Modules.Storage.Infrastructure.Persistence;
using Plexor.Shared.Kernel.Identity;
using Plexor.Shared.Kernel.Quotas;
using Plexor.Shared.Persistence;

namespace Plexor.Modules.Storage.Infrastructure.Volumes;

/// <summary>
///     EF-backed <see cref="IVolumeService" />. AsNoTracking reads on
///     every list / get path; tracked entities only on the
///     create / update write paths. The Tenant scope is always
///     <c>org_id = ?</c> on every query — the (org_id) index backs
///     every read.
/// </summary>
/// <remarks>
///     <para><b>Why public.</b> Same shape as
///     <c>Plexor.Modules.Quotas.Infrastructure.Quotas.EfQuotaScopeResolver</c>
///     — public so the unit-test project can construct it directly
///     with the in-memory <see cref="StorageDbContext" />.</para>
///     <para><b>Quota enforcement (4.5.d follow-up).</b>
///     <see cref="CreateAsync" /> opens a transaction, reserves both
///     <c>storage.volumes.count</c> (amount = 1) and
///     <c>storage.volumes.gb</c> (amount = <see cref="NewVolumeInput.SizeGb" />)
///     against the org scope, then INSERTs the volume row. Both
///     reservations share the same transaction as the INSERT — a
///     <c>Denied</c> from either enforcer check throws
///     <see cref="QuotaExceededException" />, the open transaction
///     rolls back on dispose, and no row + no quota counter advances.</para>
///     <para><b>Quota enforcement on resize (4.5.d follow-up).
    ///     </b> <see cref="UpdateSizeAsync" /> opens the same shape of
    ///     transaction as <see cref="CreateAsync" />. It reads the
    ///     current <c>SizeGb</c> first, computes the delta
    ///     (<c>newSizeGb</c> - current), and reserves only the delta
    ///     against <c>storage.volumes.gb</c>. Shrinking or no-change
    ///     resizes skip the enforcer entirely (a negative or zero
    ///     delta never fails on a capacity quota). A <c>Denied</c> on
    ///     the delta throws <see cref="QuotaExceededException" />;
    ///     the open transaction rolls back on dispose and the
    ///     volume's <c>SizeGb</c> stays at its prior value.</para>
/// </remarks>
/// <param name="db">Scoped <see cref="StorageDbContext" />.</param>
/// <param name="clock">Injected <see cref="TimeProvider" /> for the row's
/// <c>CreatedAt</c> + <c>UpdatedAt</c> stamps.</param>
/// <param name="quotaEnforcer">
///     Reserves <c>storage.volumes.count</c> + <c>storage.volumes.gb</c>
///     capacity inside the same transaction as the volume INSERT —
///     see 4.5.c and
///     <c>openspec/changes/phase-4-5-quotas/design.md</c> §"Enforcement:
///     pure-sync with pg_advisory_xact_lock".
/// </param>
/// <param name="currentUser">
///     Per-request caller identity (4.5.h). The handler forwards
///     <see cref="ICurrentUser.UserId" /> into the
///     <see cref="QuotaScope" /> so the audit emitter can attach the
///     actor to every <c>UsageExceeded</c> / <c>LimitApproaching</c>
///     event.
/// </param>
public sealed class EfVolumeService(
    StorageDbContext db,
    TimeProvider clock,
    IQuotaEnforcer quotaEnforcer,
    ICurrentUser currentUser) : IVolumeService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<Volume>> ListAsync(
        Guid orgId,
        CancellationToken cancellationToken = default)
    {
        return await db.Volumes
            .AsNoTracking()
            .Where(volume => volume.OrgId == orgId)
            .OrderByDescending(static volume => volume.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<Volume?> GetAsync(
        Guid volumeId,
        Guid orgId,
        CancellationToken cancellationToken = default)
    {
        return db.Volumes
            .AsNoTracking()
            .FirstOrDefaultAsync(
                volume => volume.Id == volumeId && volume.OrgId == orgId,
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Volume> CreateAsync(
        NewVolumeInput input,
        CancellationToken cancellationToken = default)
    {
        // Single transaction: both quota reservations + the volume
        // INSERT commit (or roll back) together. Both contexts share
        // the same NpgsqlDataSource via the PlexorDataSourceExtensions
        // composition root; the InMemory provider used by handler unit
        // tests is skipped (no transaction support).
        await using var transaction =
            await db.Database.BeginTransactionIfSupportedAsync(cancellationToken);

        // Reserve storage.volumes.count at the org scope (count = 1).
        // ActorUserId flows into the QuotaScope so the audit emitter
        // can attach the caller to any UsageExceeded / LimitApproaching
        // event.
        var countCheck = await quotaEnforcer.CheckAndReserveAsync(
            QuotaScope.Org(input.OrgId, currentUser.UserId),
            QuotaDefinitionKey.VolumesCount,
            amount: 1,
            cancellationToken);
        if (countCheck is QuotaCheckResult.Denied countDenied)
        {
            throw new QuotaExceededException(
                countDenied.Limit,
                countDenied.Used,
                countDenied.Requested);
        }

        // Reserve storage.volumes.gb at the org scope (amount = the
        // new volume's SizeGb). Both reservations ride the same open
        // transaction; a Denied here rolls back the count reservation
        // on dispose before the exception propagates.
        var sizeCheck = await quotaEnforcer.CheckAndReserveAsync(
            QuotaScope.Org(input.OrgId, currentUser.UserId),
            QuotaDefinitionKey.VolumesGb,
            amount: input.SizeGb,
            cancellationToken);
        if (sizeCheck is QuotaCheckResult.Denied sizeDenied)
        {
            throw new QuotaExceededException(
                sizeDenied.Limit,
                sizeDenied.Used,
                sizeDenied.Requested);
        }

        var now = clock.GetUtcNow();
        var entity = new Volume
        {
            Id = Guid.NewGuid(),
            OrgId = input.OrgId,
            ClusterId = input.ClusterId,
            Name = input.Name,
            SizeGb = input.SizeGb,
            Status = VolumeStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await db.Volumes.AddAsync(entity, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
        return entity;
    }

    /// <inheritdoc />
    public async Task<Volume?> UpdateSizeAsync(
        Guid volumeId,
        Guid orgId,
        int newSizeGb,
        CancellationToken cancellationToken = default)
    {
        // Single transaction: the enforcer's UPDATE on quotas.quota_usage
        // and the volume UPDATE commit (or roll back) together. Mirrors
        // CreateAsync — the InMemory provider used by handler unit tests
        // is skipped (no transaction support).
        await using var transaction =
            await db.Database.BeginTransactionIfSupportedAsync(cancellationToken);

        var volume = await db.Volumes
            .FirstOrDefaultAsync(
                volume => volume.Id == volumeId && volume.OrgId == orgId,
                cancellationToken);
        if (volume is null)
        {
            return null;
        }

        // Only a positive delta needs a reservation — shrinking a
        // volume frees capacity, no-change is a no-op write. The
        // delta is signed (new - old), so the gate is straightforward.
        var delta = newSizeGb - volume.SizeGb;
        if (delta > 0)
        {
            var quotaCheck = await quotaEnforcer.CheckAndReserveAsync(
                QuotaScope.Org(orgId, currentUser.UserId),
                QuotaDefinitionKey.VolumesGb,
                amount: delta,
                cancellationToken);
            if (quotaCheck is QuotaCheckResult.Denied denied)
            {
                throw new QuotaExceededException(
                    denied.Limit,
                    denied.Used,
                    denied.Requested);
            }
        }

        volume.SizeGb = newSizeGb;
        volume.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
        return volume;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(
        Guid volumeId,
        Guid orgId,
        CancellationToken cancellationToken = default)
    {
        var volume = await db.Volumes
            .FirstOrDefaultAsync(
                volume => volume.Id == volumeId && volume.OrgId == orgId,
                cancellationToken);
        if (volume is null)
        {
            return false;
        }

        db.Volumes.Remove(volume);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
