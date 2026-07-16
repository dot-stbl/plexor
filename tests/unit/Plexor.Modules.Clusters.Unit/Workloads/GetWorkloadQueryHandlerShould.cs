// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// GetWorkloadQueryHandler unit tests.
// ==========================================================================

using Plexor.Modules.Clusters.Application.Clusters;
using Plexor.Modules.Clusters.Domain;
using Plexor.Modules.Clusters.Domain.Entities;
using Plexor.Modules.Clusters.Domain.Errors;
using Plexor.Modules.Clusters.Infrastructure.Clusters;
using Plexor.Modules.Clusters.Infrastructure.Mappers;
using Plexor.Modules.Clusters.Infrastructure.Persistence;
using Plexor.Shared.Identifiers;
using Plexor.Shared.Persistence;
using Plexor.Shared.Workloads;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Clusters.Unit.Workloads;

public sealed class GetWorkloadQueryHandlerShould
{
    [Fact(DisplayName = "Given existing workload, when GetWorkload, then returns mapped summary")]
    public async Task GetWorkloadReturnsSummary()
    {
        await using var db = await TestDb.CreateAsync();
        var cluster = await SeedClusterAsync(db);
        var workloadId = await SeedWorkloadAsync(db, cluster.Id, name: "web-1", kind: "vm");
        var sut = new GetWorkloadQueryHandler(new WorkloadRepository(db), new WorkloadMapper());

        var summary = await sut.HandleAsync(new GetWorkloadQuery(cluster.Id, workloadId));

        summary.Id.ShouldBe(workloadId);
        summary.ClusterId.ShouldBe(cluster.Id);
        summary.Name.ShouldBe("web-1");
        summary.Kind.ShouldBe("vm");
        summary.State.ShouldBe(WorkloadState.Provisioning);
    }

    [Fact(DisplayName = "Given non-existent workload, when GetWorkload, then throws WorkloadNotFound")]
    public async Task GetWorkloadRejectsUnknownId()
    {
        await using var db = await TestDb.CreateAsync();
        var cluster = await SeedClusterAsync(db);
        var sut = new GetWorkloadQueryHandler(new WorkloadRepository(db), new WorkloadMapper());
        var bogus = IdGenerator.NewWorkloadId();

        var ex = await Should.ThrowAsync<ClustersException>(
            () => sut.HandleAsync(new GetWorkloadQuery(cluster.Id, bogus)));

        ex.Code.ShouldBe(ClustersExceptions.WorkloadNotFound);
    }

    [Fact(DisplayName = "Given workload in different cluster, when GetWorkload, then throws WorkloadNotFound")]
    public async Task GetWorkloadRejectsWrongCluster()
    {
        await using var db = await TestDb.CreateAsync();
        var cluster1 = await SeedClusterAsync(db, "cluster-1");
        var cluster2 = await SeedClusterAsync(db, "cluster-2");
        var workloadId = await SeedWorkloadAsync(db, cluster1.Id);
        var sut = new GetWorkloadQueryHandler(new WorkloadRepository(db), new WorkloadMapper());

        var ex = await Should.ThrowAsync<ClustersException>(
            () => sut.HandleAsync(new GetWorkloadQuery(cluster2.Id, workloadId)));

        ex.Code.ShouldBe(ClustersExceptions.WorkloadNotFound);
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

    private static async Task<WorkloadId> SeedWorkloadAsync(ClusterDbContext db, ClusterId clusterId, string name = "web-1", string kind = "vm")
    {
        var now = DateTimeOffset.UtcNow;
        var id = IdGenerator.NewWorkloadId();
        await db.Workloads.AddAsync(new Workload
        {
            Id = id,
            ClusterId = clusterId,
            Name = name,
            Kind = kind,
            SpecJson = "{}",
            State = WorkloadState.Provisioning,
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync();
        return id;
    }
}