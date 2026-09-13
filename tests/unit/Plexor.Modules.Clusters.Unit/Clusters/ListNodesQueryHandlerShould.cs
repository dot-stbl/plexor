using Plexor.Modules.Clusters.Application.Clusters;
using Plexor.Modules.Clusters.Domain;
using Plexor.Modules.Clusters.Infrastructure.Clusters;
using Plexor.Modules.Clusters.Infrastructure.Mappers;
using Plexor.Modules.Clusters.Infrastructure.Persistence;
using Plexor.Shared.Identifiers;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Clusters.Unit.Clusters;

public sealed class ListNodesQueryHandlerShould
{
    [Fact(DisplayName = "Given cluster with zero nodes, when ListNodes, then returns empty list")]
    public async Task ListNodesReturnsEmptyForClusterWithNoNodesAsync()
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
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync();

        var sut = new ListNodesQueryHandler(new NodeRepository(db), new ClusterMapper());
        var result = await sut.HandleAsync(new ListNodesQuery(clusterId));

        result.ShouldBeEmpty();
    }

    [Fact(DisplayName = "Given cluster with 3 nodes, when ListNodes, then returns all 3 summaries")]
    public async Task ListNodesReturnsAllNodesForClusterAsync()
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
            CreatedAt = now,
            UpdatedAt = now,
        });
        for (var i = 0; i < 3; i++)
        {
            await db.Nodes.AddAsync(new Node
            {
                Id = IdGenerator.NewNodeId(),
                ClusterId = clusterId,
                OrgId = Guid.NewGuid(),
                Hostname = $"node-{i}",
                Role = NodeRole.Compute,
                Status = NodeStatus.Ready,
                Spec = new NodeSpec(4, 16, 100, []),
                CreatedAt = now.AddSeconds(i),
                UpdatedAt = now.AddSeconds(i),
            });
        }
        await db.SaveChangesAsync();

        var sut = new ListNodesQueryHandler(new NodeRepository(db), new ClusterMapper());
        var result = await sut.HandleAsync(new ListNodesQuery(clusterId));

        result.Count.ShouldBe(3);
        result.Select(static node => node.Hostname)
            .ShouldBe(["node-0", "node-1", "node-2"], ignoreOrder: true);
        result.ShouldAllBe(static node => node.Status == NodeStatus.Ready);
    }

    [Fact(DisplayName = "Given mixed-status nodes, when ListNodes, then returns all statuses (handler has no status filter)")]
    public async Task ListNodesReturnsMixedStatusesAsync()
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
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.Nodes.AddAsync(new Node
        {
            Id = IdGenerator.NewNodeId(),
            ClusterId = clusterId,
            OrgId = Guid.NewGuid(),
            Hostname = "ready-1",
            Role = NodeRole.Compute,
            Status = NodeStatus.Ready,
            Spec = new NodeSpec(4, 16, 100, []),
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.Nodes.AddAsync(new Node
        {
            Id = IdGenerator.NewNodeId(),
            ClusterId = clusterId,
            OrgId = Guid.NewGuid(),
            Hostname = "draining-1",
            Role = NodeRole.Compute,
            Status = NodeStatus.Draining,
            Spec = new NodeSpec(4, 16, 100, []),
            CreatedAt = now.AddSeconds(1),
            UpdatedAt = now.AddSeconds(1),
        });
        await db.Nodes.AddAsync(new Node
        {
            Id = IdGenerator.NewNodeId(),
            ClusterId = clusterId,
            OrgId = Guid.NewGuid(),
            Hostname = "gone-1",
            Role = NodeRole.Compute,
            Status = NodeStatus.Gone,
            Spec = new NodeSpec(4, 16, 100, []),
            CreatedAt = now.AddSeconds(2),
            UpdatedAt = now.AddSeconds(2),
        });
        await db.SaveChangesAsync();

        var sut = new ListNodesQueryHandler(new NodeRepository(db), new ClusterMapper());
        var result = await sut.HandleAsync(new ListNodesQuery(clusterId));

        result.Count.ShouldBe(3);
        result.Select(static node => node.Status)
            .ShouldBe(
                [NodeStatus.Ready, NodeStatus.Draining, NodeStatus.Gone],
                ignoreOrder: true);
    }
}

