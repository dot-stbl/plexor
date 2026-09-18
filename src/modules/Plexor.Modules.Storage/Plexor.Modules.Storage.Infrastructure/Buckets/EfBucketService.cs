// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EfBucketService — EF-backed IBucketService. Read + write surface for
// the buckets table. AsNoTracking on reads; scoped lifetime (matches
// the per-request DbContext).
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Storage.Application.Buckets;
using Plexor.Modules.Storage.Domain.Entities;
using Plexor.Modules.Storage.Infrastructure.Persistence;

namespace Plexor.Modules.Storage.Infrastructure.Buckets;

/// <summary>
///     EF-backed <see cref="IBucketService" />. AsNoTracking reads on
///     every list / get path; tracked entities only on the create
///     write path. Tenant scope is always <c>org_id = ?</c>.
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
public sealed class EfBucketService(
    StorageDbContext db,
    TimeProvider clock) : IBucketService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<Bucket>> ListAsync(
        Guid orgId,
        CancellationToken cancellationToken = default)
    {
        return await db.Buckets
            .AsNoTracking()
            .Where(bucket => bucket.OrgId == orgId)
            .OrderByDescending(static bucket => bucket.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<Bucket?> GetAsync(
        Guid bucketId,
        Guid orgId,
        CancellationToken cancellationToken = default)
    {
        return db.Buckets
            .AsNoTracking()
            .FirstOrDefaultAsync(
                bucket => bucket.Id == bucketId && bucket.OrgId == orgId,
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Bucket> CreateAsync(
        NewBucketInput input,
        CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var entity = new Bucket
        {
            Id = Guid.NewGuid(),
            OrgId = input.OrgId,
            Name = input.Name,
            Region = input.Region,
            SizeBytes = 0,
            ObjectCount = 0,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await db.Buckets.AddAsync(entity, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(
        Guid bucketId,
        Guid orgId,
        CancellationToken cancellationToken = default)
    {
        var bucket = await db.Buckets
            .FirstOrDefaultAsync(
                bucket => bucket.Id == bucketId && bucket.OrgId == orgId,
                cancellationToken);
        if (bucket is null)
        {
            return false;
        }

        db.Buckets.Remove(bucket);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
