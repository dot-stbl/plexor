// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// FloatingIpEntityTests — exercise the in-memory DbContext against
// Plexor.Modules.Network.Domain.Entities.FloatingIp. Pin the
// column mapping + the (org_id) index that backs the
// INetworkQuotaReader.CountAsync aggregate.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Network.Domain.Entities;
using Plexor.Modules.Network.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Network.Unit.Entities;

/// <summary>
///     Behavioural tests for <see cref="FloatingIp" /> against an
///     in-memory <see cref="NetworkDbContext" />. Mirrors
///     Plexor.Modules.Storage.Unit.Entities.VolumeEntityTests.
/// </summary>
public sealed class FloatingIpEntityTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 9, 18, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    ///     Given a new FloatingIp, when inserted + read back by Id,
    ///     then every field round-trips.
    /// </summary>
    [Fact(DisplayName = "Given a new FloatingIp, when inserted and read back, then every field round-trips")]
    public async Task InsertAndReadBack_RoundTripsEveryFieldAsync()
    {
        await using var db = NetworkTestDb.Create();
        var ip = new FloatingIp
        {
            Id = Guid.NewGuid(),
            OrgId = Guid.NewGuid(),
            ClusterId = Guid.NewGuid(),
            Address = "203.0.113.42",
            Status = NetworkResourceStatus.Attached,
            CreatedAt = FixedNow,
            UpdatedAt = FixedNow,
        };
        await db.FloatingIps.AddAsync(ip);
        await db.SaveChangesAsync();

        var read = await db.FloatingIps.AsNoTracking().SingleAsync(x => x.Id == ip.Id);

        read.Id.ShouldBe(ip.Id);
        read.OrgId.ShouldBe(ip.OrgId);
        read.ClusterId.ShouldBe(ip.ClusterId);
        read.Address.ShouldBe("203.0.113.42");
        read.Status.ShouldBe(NetworkResourceStatus.Attached);
        read.CreatedAt.ShouldBe(FixedNow);
        read.UpdatedAt.ShouldBe(FixedNow);
    }

    /// <summary>
    ///     Given multiple IPs in one org + one in another, when the
    ///     org-scoped count runs, then COUNT(*) returns only the
    ///     target org's rows. Cross-org isolation is the safety
    ///     property the quota enforcer relies on.
    /// </summary>
    [Fact(DisplayName = "Given multiple IPs across two orgs, when the org-scoped count runs, then only the target org's rows are counted")]
    public async Task OrgScopedCount_ScopesToTargetOrgAsync()
    {
        await using var db = NetworkTestDb.Create();
        var orgA = Guid.NewGuid();
        var orgB = Guid.NewGuid();
        await SeedFloatingIpsAsync(db, orgA, count: 5);
        await SeedFloatingIpsAsync(db, orgB, count: 2);

        var countA = await db.FloatingIps
            .AsNoTracking()
            .CountAsync(ip => ip.OrgId == orgA);

        countA.ShouldBe(5);
    }

    private static async Task SeedFloatingIpsAsync(
        NetworkDbContext db,
        Guid orgId,
        int count)
    {
        for (var i = 0; i < count; i++)
        {
            await db.FloatingIps.AddAsync(new FloatingIp
            {
                Id = Guid.NewGuid(),
                OrgId = orgId,
                ClusterId = Guid.NewGuid(),
                Address = $"203.0.113.{i + 1}",
                Status = NetworkResourceStatus.Attached,
                CreatedAt = FixedNow,
                UpdatedAt = FixedNow,
            });
        }

        await db.SaveChangesAsync();
    }
}
