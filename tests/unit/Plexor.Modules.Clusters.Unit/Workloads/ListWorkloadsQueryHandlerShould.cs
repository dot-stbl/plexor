// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ListWorkloadsQueryHandler unit tests.
// ==========================================================================

using Plexor.Modules.Clusters.Application.Clusters;
using Plexor.Modules.Clusters.Domain;
using Plexor.Modules.Clusters.Domain.Entities;
using Plexor.Modules.Clusters.Infrastructure.Clusters;
using Plexor.Modules.Clusters.Infrastructure.Mappers;
using Plexor.Modules.Clusters.Infrastructure.Persistence;
using Plexor.Shared.Filtering.Query;
using Plexor.Shared.Filtering.Registry;
using Plexor.Shared.Identifiers;
using Plexor.Shared.Persistence;
using Plexor.Shared.Workloads;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Clusters.Unit.Workloads;

public sealed class ListWorkloadsQueryHandlerShould
{
    [Fact(DisplayName = "Given two clusters with workloads, when ListWorkloads, then returns only matching cluster's workloads")]
    public async Task ListWorkloadsFiltersByCluster()
    {
        await using var db = await TestDb.CreateAsync();
        var cluster1 = await SeedClusterAsync(db, "cluster-1");
        var cluster2 = await SeedClusterAsync(db, "cluster-2");
        await SeedWorkloadAsync(db, cluster1.Id, "web-1");
        await SeedWorkloadAsync(db, cluster1.Id, "web-2");
        await SeedWorkloadAsync(db, cluster2.Id, "db-1");

        var sut = new ListWorkloadsQueryHandler(
            new WorkloadRepository(db),
            FilterableFieldRegistry.For<Workload>(),
            new WorkloadMapper());

        var page = await sut.HandleAsync(
            new ListWorkloadsQuery(cluster1.Id, new FilterQuery()));

        page.Total.ShouldBe(2);
        page.Items.Count.ShouldBe(2);
        page.Items.ShouldAllBe(w => w.ClusterId == cluster1.Id);
    }

    [Fact(DisplayName = "Given empty cluster, when ListWorkloads, then returns empty page")]
    public async Task ListWorkloadsEmptyClusterReturnsEmptyPage()
    {
        await using var db = await TestDb.CreateAsync();
        var cluster = await SeedClusterAsync(db);

        var sut = new ListWorkloadsQueryHandler(
            new WorkloadRepository(db),
            FilterableFieldRegistry.For<Workload>(),
            new WorkloadMapper());

        var page = await sut.HandleAsync(
            new ListWorkloadsQuery(cluster.Id, new FilterQuery()));

        page.Total.ShouldBe(0);
        page.Items.ShouldBeEmpty();
    }

    private static async Task<Cluster> SeedClusterAsync(ClusterDbContext db, string? nameSuffix = null)
    {
        var now = DateTimeOffset.UtcNow;
        var cluster = new Cluster
        {
            Id = IdGenerator.NewClusterId(),
            OrgId = Guid.NewGuid(),
            Name = $"cluster-{nameSuffix ?? Guid.NewGuid().ToString("N")[..8]}",
            Region = "eu-central-1",
            Status = ClusterStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await db.Clusters.AddAsync(cluster);
        await db.SaveChangesAsync();
        return cluster;
    }

    private static async Task SeedWorkloadAsync(ClusterDbContext db, ClusterId clusterId, string name)
    {
        var now = DateTimeOffset.UtcNow;
        await db.Workloads.AddAsync(new Workload
        {
            Id = IdGenerator.NewWorkloadId(),
            ClusterId = clusterId,
            Name = name,
            Kind = "vm",
            SpecJson = "{}",
            State = WorkloadState.Provisioning,
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync();
    }
}
