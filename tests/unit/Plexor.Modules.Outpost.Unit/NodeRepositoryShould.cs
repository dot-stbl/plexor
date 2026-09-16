// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodeRepositoryShould — exercise the read surface backed by the EF
// Repository<T> + Specification<T, TResult> pattern.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Clusters.Domain;
using Plexor.Modules.Outpost.Application;
using Plexor.Modules.Outpost.Infrastructure.Persistence.Repositories;
using Plexor.Shared.Identifiers;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Outpost.Unit;

public sealed class NodeRepositoryShould
{
    private static NodeRecord NewNode(string hostname, Guid orgId, ClusterId clusterId, DateTimeOffset now)
    {
        return new NodeRecord
        {
            Id = IdGenerator.NewNodeId(),
            ClusterId = clusterId,
            OrgId = orgId,
            Hostname = hostname,
            IpAddress = "10.0.0.1",
            Role = NodeRole.Compute,
            Status = NodeStatus.Ready,
            Spec = new NodeSpec(4, 16, 100, []),
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    [Fact(DisplayName = "Given two nodes in cluster X, when ListAsync by ClusterId, then returns both")]
    public async Task ListByClusterAsync()
    {
        await using var db = await OutpostTestDb.CreateAsync();
        var clusterId = IdGenerator.NewClusterId();
        var now = DateTimeOffset.UtcNow;
        await db.NodeRecords.AddRangeAsync(
            NewNode("node-1", Guid.NewGuid(), clusterId, now),
            NewNode("node-2", Guid.NewGuid(), clusterId, now),
            NewNode("other-node", Guid.NewGuid(), IdGenerator.NewClusterId(), now));
        await db.SaveChangesAsync();

        var sut = new NodeRecordRepository(db);
        var list = await sut.ListAsync(new Infrastructure.Persistence.Specifications.NodesByClusterSpec(clusterId));

        list.Count.ShouldBe(2);
    }

    [Fact(DisplayName = "Given existing node, when GetByIdAsync, then returns the row")]
    public async Task GetByIdAsync()
    {
        await using var db = await OutpostTestDb.CreateAsync();
        var clusterId = IdGenerator.NewClusterId();
        var seeded = NewNode("node-1", Guid.NewGuid(), clusterId, DateTimeOffset.UtcNow);
        await db.NodeRecords.AddAsync(seeded);
        await db.SaveChangesAsync();

        var sut = new NodeRecordRepository(db);
        var row = await sut.FirstOrDefaultAsync(
            new Infrastructure.Persistence.Specifications.NodeByIdSpec(seeded.Id));

        row.ShouldNotBeNull();
        row!.Hostname.ShouldBe("node-1");
    }

    [Fact(DisplayName = "Given missing node id, when GetByIdAsync, then returns null")]
    public async Task GetByIdReturnsNullForMissing()
    {
        await using var db = await OutpostTestDb.CreateAsync();
        var sut = new NodeRecordRepository(db);

        var row = await sut.FirstOrDefaultAsync(
            new Infrastructure.Persistence.Specifications.NodeByIdSpec(IdGenerator.NewNodeId()));

        row.ShouldBeNull();
    }

    [Fact(DisplayName = "Given two nodes in cluster X + Y, when ListByCluster X, then Y's nodes excluded")]
    public async Task ListFiltersByCluster()
    {
        await using var db = await OutpostTestDb.CreateAsync();
        var clusterX = IdGenerator.NewClusterId();
        var clusterY = IdGenerator.NewClusterId();
        var now = DateTimeOffset.UtcNow;
        await db.NodeRecords.AddRangeAsync(
            NewNode("x-node-1", Guid.NewGuid(), clusterX, now),
            NewNode("x-node-2", Guid.NewGuid(), clusterX, now),
            NewNode("y-node-1", Guid.NewGuid(), clusterY, now));
        await db.SaveChangesAsync();

        var sut = new NodeRecordRepository(db);
        var list = await sut.ListAsync(new Infrastructure.Persistence.Specifications.NodesByClusterSpec(clusterX));

        list.All(n => n.ClusterId == clusterX).ShouldBeTrue();
        list.Count.ShouldBe(2);
    }
}