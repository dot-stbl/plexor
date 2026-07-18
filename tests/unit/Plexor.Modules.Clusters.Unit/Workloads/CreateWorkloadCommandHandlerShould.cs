// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// CreateWorkloadCommandHandler unit tests — use the same TestDb +
// InMemory provider the cluster-handler tests use. We deliberately
// don't exercise Postgres-specific column types (jsonb, varchar(64))
// here; those are covered by integration tests against real Postgres.
// ==========================================================================

using Plexor.Modules.Clusters.Application.Clusters;
using Plexor.Modules.Clusters.Domain;
using Plexor.Modules.Clusters.Domain.Entities;
using Plexor.Modules.Clusters.Domain.Errors;
using Plexor.Modules.Clusters.Infrastructure.Clusters;
using Plexor.Modules.Clusters.Infrastructure.Mappers;
using Plexor.Modules.Clusters.Infrastructure.Persistence;
using Plexor.Shared.Identifiers;
using Plexor.Shared.Workloads;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Clusters.Unit.Workloads;

public sealed class CreateWorkloadCommandHandlerShould
{
    [Fact(DisplayName = "Given unique name, when CreateWorkload, then persists workload + returns summary")]
    public async Task CreateWorkloadPersistsAndReturnsSummaryAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var cluster = await SeedClusterAsync(db);
        var sut = new CreateWorkloadCommandHandler(db, new WorkloadMapper());

        var result = await sut.HandleAsync(
            new CreateWorkloadCommand(cluster.Id, "web-1", "vm", /*lang=json,strict*/ """{"image":"nginx:latest"}"""));

        result.Id.ShouldNotBe(default);
        result.Name.ShouldBe("web-1");
        result.Kind.ShouldBe("vm");
        result.ClusterId.ShouldBe(cluster.Id);
        result.AssignedNodeId.ShouldBeNull();
        result.LocalId.ShouldBeNull();
        result.State.ShouldBe(WorkloadState.Provisioning);

        var persisted = await db.Workloads.FindAsync(result.Id);
        persisted.ShouldNotBeNull();
        persisted!.SpecJson.ShouldBe(/*lang=json,strict*/ """{"image":"nginx:latest"}""");
        persisted.LastReportedAt.ShouldBeNull();
    }

    [Fact(DisplayName = "Given empty name, when CreateWorkload, then throws InvalidWorkloadSpec")]
    public async Task CreateWorkloadRejectsEmptyNameAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var cluster = await SeedClusterAsync(db);
        var sut = new CreateWorkloadCommandHandler(db, new WorkloadMapper());

        var ex = await Should.ThrowAsync<ClustersException>(
            () => sut.HandleAsync(new CreateWorkloadCommand(cluster.Id, "", "vm", "{}")));

        ex.Code.ShouldBe(ClustersExceptions.InvalidWorkloadSpec);
    }

    [Fact(DisplayName = "Given empty kind, when CreateWorkload, then throws InvalidWorkloadSpec")]
    public async Task CreateWorkloadRejectsEmptyKindAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var cluster = await SeedClusterAsync(db);
        var sut = new CreateWorkloadCommandHandler(db, new WorkloadMapper());

        var ex = await Should.ThrowAsync<ClustersException>(
            () => sut.HandleAsync(new CreateWorkloadCommand(cluster.Id, "web-1", "", "{}")));

        ex.Code.ShouldBe(ClustersExceptions.InvalidWorkloadSpec);
    }

    [Fact(DisplayName = "Given duplicate name in same cluster, when CreateWorkload, then throws InvalidWorkloadSpec")]
    public async Task CreateWorkloadRejectsDuplicateNameAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var cluster = await SeedClusterAsync(db);
        var now = DateTimeOffset.UtcNow;
        await db.Workloads.AddAsync(new Workload
        {
            Id = IdGenerator.NewWorkloadId(),
            ClusterId = cluster.Id,
            Name = "web-1",
            Kind = "vm",
            SpecJson = "{}",
            State = WorkloadState.Provisioning,
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync();
        var sut = new CreateWorkloadCommandHandler(db, new WorkloadMapper());

        var ex = await Should.ThrowAsync<ClustersException>(
            () => sut.HandleAsync(new CreateWorkloadCommand(cluster.Id, "web-1", "vm", "{}")));

        ex.Code.ShouldBe(ClustersExceptions.InvalidWorkloadSpec);
    }

    private static async Task<Cluster> SeedClusterAsync(ClusterDbContext db)
    {
        var now = DateTimeOffset.UtcNow;
        var cluster = new Cluster
        {
            Id = IdGenerator.NewClusterId(),
            OrgId = Guid.NewGuid(),
            Name = $"cluster-{Guid.NewGuid():N}",
            Region = "eu-central-1",
            Status = ClusterStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await db.Clusters.AddAsync(cluster);
        await db.SaveChangesAsync();
        return cluster;
    }
}
