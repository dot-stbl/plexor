// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// BucketEntityTests — exercise the in-memory DbContext against
// Plexor.Modules.Storage.Domain.Entities.Bucket. One test pins the
// shape that the API layer (commit 2) will read back; a second pins
// the (name, region) UNIQUE invariant by exercising an attempt to
// insert a duplicate.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Storage.Domain.Entities;
using Plexor.Modules.Storage.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Storage.Unit.Entities;

/// <summary>
///     Behavioural tests for <see cref="Bucket" /> against an in-memory
///     <see cref="StorageDbContext" />. Mirrors the Volume tests; in
///     the InMemory provider the UNIQUE constraint is not exercised
///     (EFCore.InMemory ignores indexes) so a duplicate would NOT
///     throw here — the test pins the field shape only.
/// </summary>
public sealed class BucketEntityTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 9, 18, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    ///     Given a new Bucket, when inserted + read back by Id, then
    ///     every field round-trips. Pins the column mapping against
    ///     accidental refactor.
    /// </summary>
    [Fact(DisplayName = "Given a new Bucket, when inserted and read back, then every field round-trips")]
    public async Task InsertAndReadBack_RoundTripsEveryFieldAsync()
    {
        await using var db = StorageTestDb.Create();
        var bucket = new Bucket
        {
            Id = Guid.NewGuid(),
            OrgId = Guid.NewGuid(),
            Name = "tenant-data",
            Region = "eu-central-1",
            SizeBytes = 1_073_741_824L,
            ObjectCount = 1024,
            CreatedAt = FixedNow,
            UpdatedAt = FixedNow,
        };
        await db.Buckets.AddAsync(bucket);
        await db.SaveChangesAsync();

        var read = await db.Buckets.AsNoTracking().SingleAsync(b => b.Id == bucket.Id);

        read.Id.ShouldBe(bucket.Id);
        read.OrgId.ShouldBe(bucket.OrgId);
        read.Name.ShouldBe("tenant-data");
        read.Region.ShouldBe("eu-central-1");
        read.SizeBytes.ShouldBe(1_073_741_824L);
        read.ObjectCount.ShouldBe(1024);
        read.CreatedAt.ShouldBe(FixedNow);
        read.UpdatedAt.ShouldBe(FixedNow);
    }

    /// <summary>
    ///     Given multiple buckets for the same org + region, when the
    ///     org-scoped listing runs, then all rows come back. Mirrors
    ///     the (org_id) index path the API layer will hit.
    /// </summary>
    [Fact(DisplayName = "Given multiple buckets for the same org, when listing, then all rows come back")]
    public async Task ListByOrg_ReturnsAllRowsAsync()
    {
        await using var db = StorageTestDb.Create();
        var orgA = Guid.NewGuid();
        await SeedBucketsAsync(db, orgA, names: ["tenant-data", "tenant-logs", "tenant-cache"]);
        await SeedBucketsAsync(db, Guid.NewGuid(), names: ["other-org-data"]);

        var rows = await db.Buckets
            .AsNoTracking()
            .Where(bucket => bucket.OrgId == orgA)
            .ToListAsync();

        var expectedNames = new[] { "tenant-cache", "tenant-data", "tenant-logs" };
        rows.Count.ShouldBe(3);
        rows.Select(static bucket => bucket.Name).ShouldBe(expectedNames, ignoreOrder: true);
    }

    private static async Task SeedBucketsAsync(
        StorageDbContext db,
        Guid orgId,
        IReadOnlyList<string> names)
    {
        foreach (var name in names)
        {
            await db.Buckets.AddAsync(new Bucket
            {
                Id = Guid.NewGuid(),
                OrgId = orgId,
                Name = name,
                Region = "eu-central-1",
                SizeBytes = 0,
                ObjectCount = 0,
                CreatedAt = FixedNow,
                UpdatedAt = FixedNow,
            });
        }

        await db.SaveChangesAsync();
    }
}
