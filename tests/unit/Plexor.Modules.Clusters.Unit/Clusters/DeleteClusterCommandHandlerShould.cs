using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Clusters.Application.Clusters;
using Plexor.Modules.Clusters.Domain;
using Plexor.Modules.Clusters.Domain.Entities;
using Plexor.Modules.Clusters.Domain.Errors;
using Plexor.Modules.Clusters.Infrastructure.Clusters;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Clusters.Unit.Clusters;

public sealed class DeleteClusterCommandHandlerShould
{
    [Fact(DisplayName = "Given cluster with 2 nodes, when DeleteCluster, then cluster + nodes flipped to terminal status")]
    public async Task DeleteClusterCascadesNodeStatus()
    {
        var clusterId = Guid.NewGuid();
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
            Id = Guid.NewGuid(),
            ClusterId = clusterId,
            OrgId = Guid.NewGuid(),
            Hostname = "node-1",
            Role = NodeRole.Control,
            Status = NodeStatus.Ready,
            Spec = new NodeSpec(4, 16, 100, []),
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.Nodes.AddAsync(new Node
        {
            Id = Guid.NewGuid(),
            ClusterId = clusterId,
            OrgId = Guid.NewGuid(),
            Hostname = "node-2",
            Role = NodeRole.Compute,
            Status = NodeStatus.Ready,
            Spec = new NodeSpec(8, 32, 200, []),
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync();

        var sut = new DeleteClusterCommandHandler(db);
        await sut.HandleAsync(new DeleteClusterCommand(clusterId));

        var cluster = await db.Clusters.AsNoTracking().SingleAsync();
        cluster.Status.ShouldBe(ClusterStatus.Offline);

        var nodes = await db.Nodes.AsNoTracking().ToArrayAsync();
        nodes.Length.ShouldBe(2);
        nodes.ShouldAllBe(node => node.Status == NodeStatus.Gone);
    }

    [Fact(DisplayName = "Given non-existent cluster, when DeleteCluster, then throws ClusterNotFound")]
    public async Task DeleteClusterThrowsForMissing()
    {
        await using var db = await TestDb.CreateAsync();
        var sut = new DeleteClusterCommandHandler(db);

        var ex = await Should.ThrowAsync<ClustersException>(
            () => sut.HandleAsync(new DeleteClusterCommand(Guid.NewGuid())));
        ex.Code.ShouldBe(ClustersExceptions.ClusterNotFound);
    }
}
