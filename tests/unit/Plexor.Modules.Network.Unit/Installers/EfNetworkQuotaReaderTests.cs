// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EfNetworkQuotaReaderTests — exercise the INetworkQuotaReader EF
// implementation against an in-memory NetworkDbContext. The reader
// runs on the Quotas enforcer's hot path; the tests pin the
// COUNT(*) aggregate + the empty-org coalesce-to-zero behaviour.
// ============================================================================

using Plexor.Modules.Network.Application.Network;
using Plexor.Modules.Network.Domain.Entities;
using Plexor.Modules.Network.Domain.Projections;
using Plexor.Modules.Network.Infrastructure.Installers;
using Plexor.Modules.Network.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Network.Unit.Installers;

/// <summary>
///     Behavioural tests for <see cref="EfNetworkQuotaReader" />.
///     Mirrors the shape of
///     Plexor.Modules.Storage.Unit.Installers.EfStorageQuotaReaderTests —
///     count + cross-org isolation + empty org.
/// </summary>
public sealed class EfNetworkQuotaReaderTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 9, 18, 0, 0, 0, TimeSpan.Zero);

    private static EfNetworkQuotaReader BuildReader(out NetworkDbContext db)
    {
        db = NetworkTestDb.Create();
        return new EfNetworkQuotaReader(db);
    }

    /// <summary>
    ///     Given an org with 3 floating IPs + 2 load balancers, when
    ///     CountAsync runs, then FloatingIpCount = 3 +
    ///     LoadBalancerCount = 2.
    /// </summary>
    [Fact(DisplayName = "Given floating IPs + load balancers in one org, when CountAsync runs, then both counts are returned")]
    public async Task CountAsync_AggregatesAcrossResourcesAsync()
    {
        var reader = BuildReader(out var db);
        var orgId = Guid.NewGuid();
        await SeedFloatingIpsAsync(db, orgId, count: 3);
        await SeedLoadBalancersAsync(db, orgId, count: 2);

        var counters = await reader.CountAsync(orgId);

        counters.FloatingIpCount.ShouldBe(3);
        counters.LoadBalancerCount.ShouldBe(2);
    }

    /// <summary>
    ///     Given no rows for an org, when CountAsync runs, then both
    ///     counters are zero.
    /// </summary>
    [Fact(DisplayName = "Given an org with no network resources, when CountAsync runs, then both counters are zero")]
    public async Task CountAsync_EmptyOrgReturnsZerosAsync()
    {
        var reader = BuildReader(out var db);

        var counters = await reader.CountAsync(Guid.NewGuid());

        counters.FloatingIpCount.ShouldBe(0);
        counters.LoadBalancerCount.ShouldBe(0);
    }

    /// <summary>
    ///     Given two orgs with disjoint resources, when CountAsync
    ///     runs against one org, then only that org's rows are
    ///     counted.
    /// </summary>
    [Fact(DisplayName = "Given network resources in two orgs, when CountAsync runs for one, then the other org's resources are not counted")]
    public async Task CountAsync_DoesNotLeakAcrossOrgsAsync()
    {
        var reader = BuildReader(out var db);
        var orgA = Guid.NewGuid();
        var orgB = Guid.NewGuid();
        await SeedFloatingIpsAsync(db, orgA, count: 5);
        await SeedLoadBalancersAsync(db, orgA, count: 1);
        await SeedFloatingIpsAsync(db, orgB, count: 99);
        await SeedLoadBalancersAsync(db, orgB, count: 99);

        var counters = await reader.CountAsync(orgA);

        counters.FloatingIpCount.ShouldBe(5);
        counters.LoadBalancerCount.ShouldBe(1);
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
