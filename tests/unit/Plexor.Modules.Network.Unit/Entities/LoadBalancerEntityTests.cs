// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// LoadBalancerEntityTests — exercise the in-memory DbContext against
// Plexor.Modules.Network.Domain.Entities.LoadBalancer. One round-trip
// test + one org-scoped-count test (mirrors FloatingIpEntityTests).
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Network.Domain.Entities;
using Plexor.Modules.Network.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Network.Unit.Entities;

/// <summary>
///     Behavioural tests for <see cref="LoadBalancer" /> against an
///     in-memory <see cref="NetworkDbContext" />. Mirrors
///     Plexor.Modules.Storage.Unit.Entities.VolumeEntityTests.
/// </summary>
public sealed class LoadBalancerEntityTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 9, 18, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Round-trip insertion test.</summary>
    [Fact(DisplayName = "Given a new LoadBalancer, when inserted and read back, then every field round-trips")]
    public async Task InsertAndReadBack_RoundTripsEveryFieldAsync()
    {
        await using var db = NetworkTestDb.Create();
        var lb = new LoadBalancer
        {
            Id = Guid.NewGuid(),
            OrgId = Guid.NewGuid(),
            ClusterId = Guid.NewGuid(),
            Name = "prod-edge-lb-001",
            Type = LoadBalancerType.L7,
            Algorithm = LoadBalancerAlgorithm.LeastConnections,
            Status = NetworkResourceStatus.Attached,
            CreatedAt = FixedNow,
            UpdatedAt = FixedNow,
        };
        await db.LoadBalancers.AddAsync(lb);
        await db.SaveChangesAsync();

        var read = await db.LoadBalancers.AsNoTracking().SingleAsync(x => x.Id == lb.Id);

        read.Id.ShouldBe(lb.Id);
        read.OrgId.ShouldBe(lb.OrgId);
        read.ClusterId.ShouldBe(lb.ClusterId);
        read.Name.ShouldBe("prod-edge-lb-001");
        read.Type.ShouldBe(LoadBalancerType.L7);
        read.Algorithm.ShouldBe(LoadBalancerAlgorithm.LeastConnections);
        read.Status.ShouldBe(NetworkResourceStatus.Attached);
        read.CreatedAt.ShouldBe(FixedNow);
        read.UpdatedAt.ShouldBe(FixedNow);
    }

    /// <summary>Org-scoped count test — cross-org isolation.</summary>
    [Fact(DisplayName = "Given multiple load balancers across two orgs, when the org-scoped count runs, then only the target org's rows are counted")]
    public async Task OrgScopedCount_ScopesToTargetOrgAsync()
    {
        await using var db = NetworkTestDb.Create();
        var orgA = Guid.NewGuid();
        var orgB = Guid.NewGuid();
        await SeedLoadBalancersAsync(db, orgA, count: 3);
        await SeedLoadBalancersAsync(db, orgB, count: 7);

        var countA = await db.LoadBalancers
            .AsNoTracking()
            .CountAsync(lb => lb.OrgId == orgA);

        countA.ShouldBe(3);
    }

    private static async Task SeedLoadBalancersAsync(
        NetworkDbContext db,
        Guid orgId,
        int count)
    {
        for (var i = 0; i < count; i++)
        {
            await db.LoadBalancers.AddAsync(new LoadBalancer
            {
                Id = Guid.NewGuid(),
                OrgId = orgId,
                ClusterId = Guid.NewGuid(),
                Name = $"lb-{Guid.NewGuid():N}",
                Type = LoadBalancerType.L4,
                Algorithm = LoadBalancerAlgorithm.RoundRobin,
                Status = NetworkResourceStatus.Attached,
                CreatedAt = FixedNow,
                UpdatedAt = FixedNow,
            });
        }

        await db.SaveChangesAsync();
    }
}
