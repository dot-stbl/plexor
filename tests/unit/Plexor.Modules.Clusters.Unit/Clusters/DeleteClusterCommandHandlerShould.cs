// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// DeleteClusterCommandHandlerShould — exercise the soft-delete cascade.
// Node tracking moved to Plexor.Modules.Outpost as part of the
// node-tracking extraction; this test now only asserts the cluster's
// own status flip (cluster.Status → ClusterStatus.Offline).
// The node-cascade behaviour (every cluster node → NodeStatus.Gone)
// is covered in Plexor.Modules.Outpost.Unit (Outpost's node-tracking
// is its own concern now).
// ============================================================================

using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Plexor.Modules.Clusters.Application.Clusters;
using Plexor.Modules.Clusters.Domain;
using Plexor.Modules.Clusters.Domain.Errors;
using Plexor.Modules.Clusters.Infrastructure.Clusters;
using Plexor.Modules.Outpost.Application;
using Plexor.Modules.Outpost.Application.Abstractions;
using Plexor.Shared.Identifiers;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Clusters.Unit.Clusters;

public sealed class DeleteClusterCommandHandlerShould
{
    [Fact(DisplayName = "Given existing cluster, when DeleteCluster, then cluster status flips to Offline")]
    public async Task DeleteClusterFlipsClusterStatusAsync()
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

        // INodeRegistry is a NSubstitute stub — Outpost tracks the
        // cascade, not this handler.
        var nodeRegistry = Substitute.For<INodeRegistry>();
        nodeRegistry.ListNodesAsync(Arg.Any<ClusterId>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<NodeRecord>>([]));

        var sut = new DeleteClusterCommandHandler(db, nodeRegistry);
        await sut.HandleAsync(new DeleteClusterCommand(clusterId));

        var cluster = await db.Clusters.AsNoTracking().FirstAsync();
        cluster.Status.ShouldBe(ClusterStatus.Offline);
    }

    [Fact(DisplayName = "Given non-existent cluster, when DeleteCluster, then throws ClusterNotFound")]
    public async Task DeleteClusterThrowsForMissingAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var nodeRegistry = Substitute.For<INodeRegistry>();
        var sut = new DeleteClusterCommandHandler(db, nodeRegistry);

        var ex = await Should.ThrowAsync<ClustersException>(
            () => sut.HandleAsync(new DeleteClusterCommand(IdGenerator.NewClusterId())));
        ex.Code.ShouldBe(ClustersExceptions.ClusterNotFound);
    }
}