using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Clusters.Application.Clusters;
using Plexor.Modules.Clusters.Domain;
using Plexor.Modules.Clusters.Domain.Entities;
using Plexor.Modules.Clusters.Domain.Errors;
using Plexor.Modules.Clusters.Infrastructure.Clusters;
using Plexor.Shared.Identifiers;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Clusters.Unit.Clusters;

public sealed class CreateClusterCommandHandlerShould
{
    [Fact(DisplayName = "Given unique name, when CreateCluster, then returns join token + persists cluster")]
    public async Task CreateClusterPersistsClusterAndReturnsToken()
    {
        await using var db = await TestDb.CreateAsync();
        var sut = new CreateClusterCommandHandler(db);
        var command = new CreateClusterCommand(
            Guid.NewGuid(),
            "prod-eu-1",
            "eu-central-1",
            NodeRole.Control);

        var result = await sut.HandleAsync(command);

        result.ClusterId.ShouldNotBe(default(ClusterId));
        result.Token.ShouldNotBeNullOrWhiteSpace();
        result.ExpiresAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow);

        var persisted = await db.Clusters.SingleAsync();
        persisted.Name.ShouldBe("prod-eu-1");
        persisted.Region.ShouldBe("eu-central-1");
        persisted.Status.ShouldBe(ClusterStatus.Pending);

        var token = await db.JoinTokens.SingleAsync();
        token.ClusterId.ShouldBe(persisted.Id);
        token.Status.ShouldBe(TokenStatus.Active);
        token.IntendedRole.ShouldBe(NodeRole.Control);
    }

    [Fact(DisplayName = "Given duplicate name in same org, when CreateCluster, then throws ClusterNameTaken")]
    public async Task CreateClusterRejectsDuplicateName()
    {
        var orgId = Guid.NewGuid();
        await using var db = await TestDb.CreateAsync();
        var now = DateTimeOffset.UtcNow;
        await db.Clusters.AddAsync(new Cluster
        {
            Id = IdGenerator.NewClusterId(),
            OrgId = orgId,
            Name = "prod-eu-1",
            Region = "eu-central-1",
            Status = ClusterStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync();

        var sut = new CreateClusterCommandHandler(db);
        var command = new CreateClusterCommand(orgId, "prod-eu-1", "eu-west-1", NodeRole.Control);

        var ex = await Should.ThrowAsync<ClustersException>(() => sut.HandleAsync(command));
        ex.Code.ShouldBe(ClustersExceptions.ClusterNameTaken);
    }

    [Fact(DisplayName = "Given empty name, when CreateCluster, then throws ClusterNameTaken")]
    public async Task CreateClusterRejectsEmptyName()
    {
        await using var db = await TestDb.CreateAsync();
        var sut = new CreateClusterCommandHandler(db);

        var ex = await Should.ThrowAsync<ClustersException>(
            () => sut.HandleAsync(new CreateClusterCommand(Guid.NewGuid(), "", "eu-central-1", NodeRole.Control)));
        ex.Code.ShouldBe(ClustersExceptions.ClusterNameTaken);
    }
}
