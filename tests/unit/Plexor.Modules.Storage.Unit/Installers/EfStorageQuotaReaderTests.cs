// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EfStorageQuotaReaderTests — exercise the IStorageQuotaReader EF
// implementation against an in-memory StorageDbContext. The reader
// runs on the Quotas enforcer's hot path; the tests pin the
// COUNT(*) + SUM(size_gb) aggregate + the empty-org coalesce-to-zero
// behaviour so a refactor that drops the WHERE org_id = ? filter
// breaks the test loudly.
// ============================================================================

using Plexor.Modules.Storage.Domain.Entities;
using Plexor.Modules.Storage.Infrastructure.Installers;
using Plexor.Modules.Storage.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Storage.Unit.Installers;

/// <summary>
/// <para>
///     Behavioural tests for <see cref="EfStorageQuotaReader" />.
///     Mirrors the shape of the quotas enforcer's storage path:
///     <c>IStorageQuotaReader.CountAsync(orgId)</c> on every
///     CreateVolume call. Verifies:
/// </para>
/// <para>
///   1. Aggregate sums the right rows + correct totals.
///   2. Empty org returns zero counters (enforcer interprets as
///      "no usage").
///   3. Cross-org isolation — one org's rows don't leak into another.
/// </para>
/// </summary>
public sealed class EfStorageQuotaReaderTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 9, 18, 0, 0, 0, TimeSpan.Zero);

    private static EfStorageQuotaReader BuildReader(out StorageDbContext db)
    {
        db = StorageTestDb.Create();
        return new EfStorageQuotaReader(db);
    }

    /// <summary>
    ///     Given an org with three volumes (sizes 100 + 200 + 50), when
    ///     CountAsync runs, then VolumeCount = 3 + VolumeGbTotal = 350.
    /// </summary>
    [Fact(DisplayName = "Given three volumes in one org, when CountAsync runs, then count and GiB total are returned")]
    public async Task CountAsync_AggregatesAcrossAllVolumesAsync()
    {
        var reader = BuildReader(out var db);
        var orgId = Guid.NewGuid();
        await SeedVolumesAsync(db, orgId, [100, 200, 50]);

        var counters = await reader.CountAsync(orgId);

        counters.VolumeCount.ShouldBe(3);
        counters.VolumeGbTotal.ShouldBe(350m);
    }

    /// <summary>
    ///     Given no volumes for an org, when CountAsync runs, then
    ///     VolumeCount = 0 + VolumeGbTotal = 0 (the enforcer interprets
    ///     this as "no usage", not as "deny").
    /// </summary>
    [Fact(DisplayName = "Given an org with no volumes, when CountAsync runs, then both counters are zero")]
    public async Task CountAsync_EmptyOrgReturnsZerosAsync()
    {
        var reader = BuildReader(out _);

        var counters = await reader.CountAsync(Guid.NewGuid());

        counters.VolumeCount.ShouldBe(0);
        counters.VolumeGbTotal.ShouldBe(0m);
    }

    /// <summary>
    ///     Given two orgs with disjoint volumes, when CountAsync runs
    ///     against one org, then only that org's rows are counted.
    ///     Cross-org isolation is the safety property that the enforcer
    ///     relies on.
    /// </summary>
    [Fact(DisplayName = "Given two orgs, when CountAsync runs for one, then the other org's volumes are not counted")]
    public async Task CountAsync_DoesNotLeakAcrossOrgsAsync()
    {
        var reader = BuildReader(out var db);
        var orgA = Guid.NewGuid();
        var orgB = Guid.NewGuid();
        await SeedVolumesAsync(db, orgA, [10, 20]);
        await SeedVolumesAsync(db, orgB, [1024, 2048, 4096]);

        var counters = await reader.CountAsync(orgA);

        counters.VolumeCount.ShouldBe(2);
        counters.VolumeGbTotal.ShouldBe(30m);
    }

    /// <summary>
    ///     Given a single volume in the target org, when CountAsync
    ///     runs, then the count is 1 + the GiB total is the volume's
    ///     SizeGb verbatim. Smallest non-trivial case; guards against
    ///     off-by-one in the GROUP BY shape.
    /// </summary>
    [Fact(DisplayName = "Given a single volume, when CountAsync runs, then count is 1 and GiB total matches the row")]
    public async Task CountAsync_SingleVolumeReturnsExpectedTotalsAsync()
    {
        var reader = BuildReader(out var db);
        var orgId = Guid.NewGuid();
        await SeedVolumesAsync(db, orgId, [512]);

        var counters = await reader.CountAsync(orgId);

        counters.VolumeCount.ShouldBe(1);
        counters.VolumeGbTotal.ShouldBe(512m);
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
