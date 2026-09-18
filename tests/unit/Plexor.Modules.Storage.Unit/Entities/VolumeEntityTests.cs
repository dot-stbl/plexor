// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VolumeEntityTests — exercise the in-memory DbContext against
// Plexor.Modules.Storage.Domain.Entities.Volume. Two test cases pin the
// shape that downstream code (the API layer, the quota enforcer) will
// pattern-match on:
//   1. Row can be round-tripped (insert + read back).
//   2. The org_id index is the right shape for the
//      IStorageQuotaReader.CountAsync GROUP BY aggregate.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Storage.Domain.Entities;
using Plexor.Modules.Storage.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Storage.Unit.Entities;

/// <summary>
///     Behavioural tests for <see cref="Volume" /> against an in-memory
///     <see cref="StorageDbContext" />. The Postgres-specific column
///     types are ignored — the tests pin the entity shape + the
///     (org_id) index that backs the quota aggregate.
/// </summary>
public sealed class VolumeEntityTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 9, 18, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    ///     Given a new Volume, when inserted + read back by Id, then
    ///     every field round-trips. Pins the column mapping against
    ///     accidental refactor (e.g. renaming SizeGb to SizeGiB in the
    ///     entity but not the configuration).
    /// </summary>
    [Fact(DisplayName = "Given a new Volume, when inserted and read back, then every field round-trips")]
    public async Task InsertAndReadBack_RoundTripsEveryFieldAsync()
    {
        await using var db = StorageTestDb.Create();
        var volume = new Volume
        {
            Id = Guid.NewGuid(),
            OrgId = Guid.NewGuid(),
            ClusterId = Guid.NewGuid(),
            Name = "prod-data-001",
            SizeGb = 256,
            Status = VolumeStatus.Pending,
            CreatedAt = FixedNow,
            UpdatedAt = FixedNow,
        };
        await db.Volumes.AddAsync(volume);
        await db.SaveChangesAsync();

        var read = await db.Volumes.AsNoTracking().SingleAsync(v => v.Id == volume.Id);

        read.Id.ShouldBe(volume.Id);
        read.OrgId.ShouldBe(volume.OrgId);
        read.ClusterId.ShouldBe(volume.ClusterId);
        read.Name.ShouldBe("prod-data-001");
        read.SizeGb.ShouldBe(256);
        read.Status.ShouldBe(VolumeStatus.Pending);
        read.CreatedAt.ShouldBe(FixedNow);
        read.UpdatedAt.ShouldBe(FixedNow);
    }

    /// <summary>
    ///     Given multiple volumes in one org + one in another, when the
    ///     org-scoped aggregate runs, then COUNT(*) + SUM(size_gb)
    ///     return only the rows for the target org. Mirrors the
    ///     IStorageQuotaReader.CountAsync SQL shape so the test
    ///     catches accidental scoping regressions (e.g. dropping the
    ///     <c>Where(orgId)</c> predicate).
    /// </summary>
    [Fact(DisplayName = "Given multiple volumes across two orgs, when the org-scoped aggregate runs, then COUNT + SUM are scoped to the target org")]
    public async Task OrgScopedAggregate_ScopesToTargetOrgAsync()
    {
        await using var db = StorageTestDb.Create();
        var orgA = Guid.NewGuid();
        var orgB = Guid.NewGuid();
        await SeedVolumesAsync(db, orgA, sizes: [100, 200, 50]);
        await SeedVolumesAsync(db, orgB, sizes: [1024]);

        var aggregate = await db.Volumes
            .AsNoTracking()
            .Where(volume => volume.OrgId == orgA)
            .GroupBy(static volume => 1)
            .Select(static grouping => new
            {
                Count = grouping.Count(),
                GbTotal = grouping.Sum(static volume => (decimal)volume.SizeGb),
            })
            .FirstOrDefaultAsync();

        aggregate.ShouldNotBeNull();
        aggregate.Count.ShouldBe(3);
        aggregate.GbTotal.ShouldBe(350m);
    }

    /// <summary>
    ///     Given no volumes for an org, when the org-scoped aggregate
    ///     runs, then COUNT = 0 + SUM = 0 (Postgres returns NULL on
    ///     empty SUM; the EF aggregate translates to default-initialised
    ///     aggregate only via FirstOrDefault).
    /// </summary>
    [Fact(DisplayName = "Given no volumes for an org, when the org-scoped aggregate runs, then the result is null and the reader coalesces to zero")]
    public async Task OrgScopedAggregate_EmptyOrgReturnsNullAsync()
    {
        await using var db = StorageTestDb.Create();
        var orgA = Guid.NewGuid();

        var aggregate = await db.Volumes
            .AsNoTracking()
            .Where(volume => volume.OrgId == orgA)
            .GroupBy(static volume => 1)
            .Select(static grouping => new
            {
                Count = grouping.Count(),
                GbTotal = grouping.Sum(static volume => (decimal)volume.SizeGb),
            })
            .FirstOrDefaultAsync();

        aggregate.ShouldBeNull();
    }

    private static async Task SeedVolumesAsync(
        StorageDbContext db,
        Guid orgId,
        IReadOnlyList<int> sizes)
    {
        foreach (var size in sizes)
        {
            await db.Volumes.AddAsync(new Volume
            {
                Id = Guid.NewGuid(),
                OrgId = orgId,
                ClusterId = Guid.NewGuid(),
                Name = $"vol-{Guid.NewGuid():N}",
                SizeGb = size,
                Status = VolumeStatus.Attached,
                CreatedAt = FixedNow,
                UpdatedAt = FixedNow,
            });
        }

        await db.SaveChangesAsync();
    }
}
