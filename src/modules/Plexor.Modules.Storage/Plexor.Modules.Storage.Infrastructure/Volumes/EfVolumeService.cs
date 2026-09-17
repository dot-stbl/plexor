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
/// </remarks>
/// <param name="db">Scoped <see cref="StorageDbContext" />.</param>
/// <param name="clock">Injected <see cref="TimeProvider" /> for the row's
/// <c>CreatedAt</c> + <c>UpdatedAt</c> stamps.</param>
public sealed class EfVolumeService(
    StorageDbContext db,
    TimeProvider clock) : IVolumeService
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
        return entity;
    }

    /// <inheritdoc />
    public async Task<Volume?> UpdateSizeAsync(
        Guid volumeId,
        Guid orgId,
        int newSizeGb,
        CancellationToken cancellationToken = default)
    {
        var volume = await db.Volumes
            .FirstOrDefaultAsync(
                volume => volume.Id == volumeId && volume.OrgId == orgId,
                cancellationToken);
        if (volume is null)
        {
            return null;
        }

        volume.SizeGb = newSizeGb;
        volume.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
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
