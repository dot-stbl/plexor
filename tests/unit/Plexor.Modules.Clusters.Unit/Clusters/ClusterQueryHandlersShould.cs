// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// GetClusterQueryHandlerShould — exercise the read surface after the
// node-tracking extraction.
//
// Node rows now live in Plexor.Modules.Outpost (outpost.node_records);
// the cluster detail's Nodes collection is populated by Outpost's
// list handler. The Clusters-side GetClusterQueryHandler no longer
// eager-loads nodes — the v0.1 detail view returns an empty Nodes
// collection; the dashboard's full cluster card makes a separate
// GET /api/v1/nodes?clusterId=X call.
// ==========================================================================

using Plexor.Modules.Clusters.Application.Clusters;
using Plexor.Modules.Clusters.Domain;
using Plexor.Modules.Clusters.Domain.Errors;
using Plexor.Modules.Clusters.Infrastructure.Clusters;
using Plexor.Modules.Clusters.Infrastructure.Mappers;
using Plexor.Modules.Clusters.Infrastructure.Persistence;
using Plexor.Shared.Identifiers;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Clusters.Unit.Clusters;

public sealed class GetClusterQueryHandlerShould
{
    [Fact(DisplayName = "Given existing cluster, when GetCluster, then returns detail with empty Nodes collection")]
    public async Task GetClusterReturnsDetailWithEmptyNodesAsync()
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
        await db.SaveChangesAsync();

        var sut = new GetClusterQueryHandler(
            new ClusterRepository(db),
            new ClusterMapper());
        var result = await sut.HandleAsync(new GetClusterQuery(clusterId));

        result.Id.ShouldBe(clusterId);
        result.Name.ShouldBe("prod-eu-1");
        result.Status.ShouldBe(ClusterStatus.Ready);
        // Nodes collection is empty until the dashboard pairs this
        // call with GET /api/v1/nodes?clusterId=X (Outpost).
        result.Nodes.Count.ShouldBe(0);
    }

    [Fact(DisplayName = "Given non-existent cluster, when GetCluster, then throws ClusterNotFound")]
    public async Task GetClusterThrowsForMissingAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var sut = new GetClusterQueryHandler(
            new ClusterRepository(db),
            new ClusterMapper());

        var ex = await Should.ThrowAsync<ClustersException>(
            () => sut.HandleAsync(new GetClusterQuery(IdGenerator.NewClusterId())));
        ex.Code.ShouldBe(ClustersExceptions.ClusterNotFound);
    }
}