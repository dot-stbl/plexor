// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// DeleteWorkloadCommandHandler unit tests.
// ==========================================================================

using Plexor.Modules.Clusters.Application.Clusters;
using Plexor.Modules.Clusters.Domain;
using Plexor.Modules.Clusters.Domain.Entities;
using Plexor.Modules.Clusters.Domain.Errors;
using Plexor.Modules.Clusters.Infrastructure.Clusters;
using Plexor.Modules.Clusters.Infrastructure.Persistence;
using Plexor.Shared.Identifiers;
using Plexor.Shared.Workloads;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Clusters.Unit.Workloads;

public sealed class DeleteWorkloadCommandHandlerShould
{
    [Fact(DisplayName = "Given existing workload, when DeleteWorkload, then removes from db")]
    public async Task DeleteWorkloadRemovesRowAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var cluster = await SeedClusterAsync(db);
        var workloadId = await SeedWorkloadAsync(db, cluster.Id);
        var sut = new DeleteWorkloadCommandHandler(db);

        var result = await sut.HandleAsync(new DeleteWorkloadCommand(cluster.Id, workloadId));

        result.ShouldBe(Infrastructure.Clusters.Unit.Value);
        db.Workloads.Count().ShouldBe(0);
    }

    [Fact(DisplayName = "Given non-existent workload, when DeleteWorkload, then throws WorkloadNotFound")]
    public async Task DeleteWorkloadRejectsUnknownIdAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var cluster = await SeedClusterAsync(db);
        var sut = new DeleteWorkloadCommandHandler(db);
        var bogus = IdGenerator.NewWorkloadId();

        var ex = await Should.ThrowAsync<ClustersException>(
            () => sut.HandleAsync(new DeleteWorkloadCommand(cluster.Id, bogus)));

        ex.Code.ShouldBe(ClustersExceptions.WorkloadNotFound);
    }

    [Fact(DisplayName = "Given workload in different cluster, when DeleteWorkload, then throws WorkloadNotFound")]
    public async Task DeleteWorkloadRejectsWrongClusterAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var cluster1 = await SeedClusterAsync(db, "cluster-1");
        var cluster2 = await SeedClusterAsync(db, "cluster-2");
        var workloadId = await SeedWorkloadAsync(db, cluster1.Id);
        var sut = new DeleteWorkloadCommandHandler(db);

        var ex = await Should.ThrowAsync<ClustersException>(
            () => sut.HandleAsync(new DeleteWorkloadCommand(cluster2.Id, workloadId)));

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

    private static async Task<WorkloadId> SeedWorkloadAsync(ClusterDbContext db, ClusterId clusterId)
    {
        var now = DateTimeOffset.UtcNow;
        var id = IdGenerator.NewWorkloadId();
        await db.Workloads.AddAsync(new Workload
        {
            Id = id,
            ClusterId = clusterId,
            Name = $"wl-{Guid.NewGuid().ToString("N")[..8]}",
            Kind = "vm",
            SpecJson = "{}",
            State = WorkloadState.Provisioning,
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync();
        return id;
    }
}
