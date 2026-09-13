using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Clusters.Application.Clusters;
using Plexor.Modules.Clusters.Domain;
using Plexor.Modules.Clusters.Domain.Errors;
using Plexor.Modules.Clusters.Infrastructure.Clusters;
using Plexor.Modules.Clusters.Infrastructure.Mappers;
using Plexor.Shared.Identifiers;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Clusters.Unit.Clusters;

public sealed class UpdateClusterCommandHandlerShould
{
    [Fact(DisplayName = "Given unique new name, when UpdateCluster, then rename succeeds + cluster.Name updated")]
    public async Task UpdateClusterRenamesToUniqueNameAsync()
    {
        var clusterId = IdGenerator.NewClusterId();
        var orgId = Guid.NewGuid();
        await using var db = await TestDb.SqliteAsync();
        var createdAt = DateTimeOffset.UtcNow.AddDays(-1);
        await db.Clusters.AddAsync(new Cluster
        {
            Id = clusterId,
            OrgId = orgId,
            Name = "prod-eu-1",
            Region = "eu-central-1",
            Status = ClusterStatus.Ready,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
        });
        await db.SaveChangesAsync();

        var sut = new UpdateClusterCommandHandler(db, new ClusterMapper());
        var result = await sut.HandleAsync(new UpdateClusterCommand(
            clusterId,
            "prod-eu-1-renamed",
            "eu-west-1"));

        result.Id.ShouldBe(clusterId);
        result.Name.ShouldBe("prod-eu-1-renamed");
        result.Region.ShouldBe("eu-west-1");

        db.ChangeTracker.Clear();
        var persisted = await db.Clusters.AsNoTracking().SingleAsync();
        persisted.Name.ShouldBe("prod-eu-1-renamed");
        persisted.Region.ShouldBe("eu-west-1");
        persisted.UpdatedAt.ShouldBeGreaterThan(createdAt);
    }

    [Fact(DisplayName = "Given rename to name taken by another cluster in same org, when UpdateCluster, then throws ClusterNameTaken")]
    public async Task UpdateClusterRejectsNameTakenBySiblingAsync()
    {
        var orgId = Guid.NewGuid();
        var clusterId = IdGenerator.NewClusterId();
        var siblingId = IdGenerator.NewClusterId();
        await using var db = await TestDb.SqliteAsync();
        var now = DateTimeOffset.UtcNow;
        await db.Clusters.AddAsync(new Cluster
        {
            Id = clusterId,
            OrgId = orgId,
            Name = "prod-eu-1",
            Region = "eu-central-1",
            Status = ClusterStatus.Ready,
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.Clusters.AddAsync(new Cluster
        {
            Id = siblingId,
            OrgId = orgId,
            Name = "prod-eu-2",
            Region = "eu-central-1",
            Status = ClusterStatus.Ready,
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync();

        var sut = new UpdateClusterCommandHandler(db, new ClusterMapper());
        var ex = await Should.ThrowAsync<ClustersException>(() =>
            sut.HandleAsync(new UpdateClusterCommand(clusterId, "prod-eu-2", null)));

        ex.Code.ShouldBe(ClustersExceptions.ClusterNameTaken);

        db.ChangeTracker.Clear();
        var persisted = await db.Clusters.AsNoTracking().SingleAsync(c => c.Id == clusterId);
        persisted.Name.ShouldBe("prod-eu-1");
    }

    [Fact(DisplayName = "Given rename to current name, when UpdateCluster, then succeeds + name unchanged + UpdatedAt bumped")]
    public async Task UpdateClusterRenamesToCurrentNameSucceedsAsync()
    {
        var clusterId = IdGenerator.NewClusterId();
        await using var db = await TestDb.SqliteAsync();
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        await db.Clusters.AddAsync(new Cluster
        {
            Id = clusterId,
            OrgId = Guid.NewGuid(),
            Name = "prod-eu-1",
            Region = "eu-central-1",
            Status = ClusterStatus.Ready,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
        });
        await db.SaveChangesAsync();

        var sut = new UpdateClusterCommandHandler(db, new ClusterMapper());
        var result = await sut.HandleAsync(new UpdateClusterCommand(
            clusterId,
            "prod-eu-1",
            null));

        result.Name.ShouldBe("prod-eu-1");

        db.ChangeTracker.Clear();
        var persisted = await db.Clusters.AsNoTracking().SingleAsync();
        persisted.Name.ShouldBe("prod-eu-1");
        persisted.UpdatedAt.ShouldBeGreaterThan(createdAt);
    }

    [Fact(DisplayName = "Given non-existent cluster, when UpdateCluster, then throws ClusterNotFound")]
    public async Task UpdateClusterThrowsForMissingAsync()
    {
        await using var db = await TestDb.SqliteAsync();
        var sut = new UpdateClusterCommandHandler(db, new ClusterMapper());

        var ex = await Should.ThrowAsync<ClustersException>(() =>
            sut.HandleAsync(new UpdateClusterCommand(
                IdGenerator.NewClusterId(),
                "prod-eu-1",
                null)));

        ex.Code.ShouldBe(ClustersExceptions.ClusterNotFound);
    }

    [Fact(DisplayName = "Given empty new name, when UpdateCluster, then handler applies empty Name to the row (no FluentValidation guard today)")]
    public async Task UpdateClusterCurrentlyDoesNotValidateEmptyNameAsync()
    {
        var clusterId = IdGenerator.NewClusterId();
        await using var db = await TestDb.SqliteAsync();
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await db.Clusters.AddAsync(new Cluster
        {
            Id = clusterId,
            OrgId = Guid.NewGuid(),
            Name = "prod-eu-1",
            Region = "eu-central-1",
            Status = ClusterStatus.Ready,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
        });
        await db.SaveChangesAsync();

        var sut = new UpdateClusterCommandHandler(db, new ClusterMapper());
        await sut.HandleAsync(new UpdateClusterCommand(clusterId, string.Empty, null));

        db.ChangeTracker.Clear();
        var persisted = await db.Clusters.AsNoTracking().SingleAsync();
        persisted.Name.ShouldBe(string.Empty);
    }
}

