using Plexor.Modules.Clusters.Application.Clusters;
using Plexor.Modules.Clusters.Domain;
using Plexor.Modules.Clusters.Domain.Entities;
using Plexor.Modules.Clusters.Infrastructure.Persistence;
using Plexor.Modules.Clusters.Domain.Errors;
using Plexor.Modules.Clusters.Infrastructure.Clusters;
using Plexor.Modules.Clusters.Infrastructure.Mappers;
using Plexor.Shared.Identifiers;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Clusters.Unit.Clusters;

public sealed class GetClusterQueryHandlerShould
{
    [Fact(DisplayName = "Given existing cluster with nodes, when GetCluster, then returns detail with nodes")]
    public async Task GetClusterReturnsDetailWithNodes()
    {
        var clusterId = IdGenerator.NewClusterId();
        await using var db = await TestDb.CreateAsync();
        var now = DateTimeOffset.UtcNow;
        await db.Clusters.AddAsync(new Cluster
        {
            Id = clusterId,
            OrgId = Guid.NewGuid(),
            Name = "prod-eu-1",
            Region = "eu-central-1",
            Status = ClusterStatus.Ready,
            Endpoint = "https://plexor.host",
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.Nodes.AddAsync(new Node
        {
            Id = IdGenerator.NewNodeId(),
            ClusterId = clusterId,
            OrgId = Guid.NewGuid(),
            Hostname = "node-1",
            Role = NodeRole.Control,
            Status = NodeStatus.Ready,
            Spec = new NodeSpec(4, 16, 100, []),
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync();

        var sut = new GetClusterQueryHandler(
            new ClusterRepository(db),
            new NodeRepository(db),
            new ClusterMapper());
        var result = await sut.HandleAsync(new GetClusterQuery(clusterId));

        result.Id.ShouldBe(clusterId);
        result.Name.ShouldBe("prod-eu-1");
        result.Status.ShouldBe(ClusterStatus.Ready);
        result.Nodes.Count.ShouldBe(1);
        result.Nodes[0].Hostname.ShouldBe("node-1");
    }

    [Fact(DisplayName = "Given non-existent cluster, when GetCluster, then throws ClusterNotFound")]
    public async Task GetClusterThrowsForMissing()
    {
        await using var db = await TestDb.CreateAsync();
        var sut = new GetClusterQueryHandler(
            new ClusterRepository(db),
            new NodeRepository(db),
            new ClusterMapper());

        var ex = await Should.ThrowAsync<ClustersException>(
            () => sut.HandleAsync(new GetClusterQuery(IdGenerator.NewClusterId())));
        ex.Code.ShouldBe(ClustersExceptions.ClusterNotFound);
    }
}

public sealed class ListClustersQueryHandlerShould
{
    [Fact(DisplayName = "Given 3 clusters in org, when ListClusters page 1 size 2, then returns 2 items + total 3")]
    public async Task ListClustersPaginatesCorrectly()
    {
        var orgId = Guid.NewGuid();
        await using var db = await TestDb.CreateAsync();
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 3; i++)
        {
            await db.Clusters.AddAsync(new Cluster
            {
                Id = IdGenerator.NewClusterId(),
                OrgId = orgId,
                Name = $"cluster-{i}",
                Region = "eu-central-1",
                Status = ClusterStatus.Ready,
                CreatedAt = now.AddSeconds(i),
                UpdatedAt = now.AddSeconds(i),
            });
        }
        await db.SaveChangesAsync();

        var sut = new ListClustersQueryHandler(
            new ClusterRepository(db),
            Plexor.Shared.Filtering.Registry.FilterableFieldRegistry.For<Domain.Entities.Cluster>(),
            new ClusterMapper());
        var filterQuery = new Plexor.Shared.Filtering.Query.FilterQuery { Page = 1, PageSize = 2 };
        var result = await sut.HandleAsync(new ListClustersQuery(orgId, filterQuery));

        result.Total.ShouldBe(3);
        result.Page.ShouldBe(1);
        result.PageSize.ShouldBe(2);
        result.Items.Count.ShouldBe(2);
    }
}
