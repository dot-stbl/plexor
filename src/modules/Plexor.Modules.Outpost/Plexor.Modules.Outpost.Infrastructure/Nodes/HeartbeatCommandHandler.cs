// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// HeartbeatCommandHandler — stamp a node's heartbeat.
//
// Updates LastHeartbeatAt + bumps Spec/IpAddress to the freshest
// snapshot, then re-evaluates NodeStatus (Ready when the cluster is
// up; preserved when the cluster is Offline / the node is Draining).
//
// Extracted from Plexor.Modules.Clusters.Infrastructure.Clusters
// .NodeHeartbeatCommandHandler; the workload-reconciliation block
// stays in Clusters (worktables live in forge.workloads, owned by
// Clusters). Heartbeat's only Outpost concern is the node row + the
// cluster status echo that drives NodeAgent drain-on-offline.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Clusters.Domain;
using Plexor.Modules.Clusters.Infrastructure.Persistence;
using Plexor.Modules.Outpost.Application;
using Plexor.Modules.Outpost.Application.Abstractions;
using Plexor.Modules.Outpost.Application.NodeCommands;
using Plexor.Modules.Outpost.Infrastructure.Persistence;
using Plexor.Modules.Outpost.Infrastructure.Persistence.Specifications;
using Plexor.Shared.Identifiers;
using Plexor.Shared.Persistence;
using NodeStatus = Plexor.Modules.Outpost.Application.NodeStatus;

namespace Plexor.Modules.Outpost.Infrastructure.Nodes;

/// <summary>
///     Periodic keepalive from a joined node. Stamps the node's
///     LastHeartbeatAt and returns the cluster status so the NodeAgent
///     can react (e.g. drain + exit on <see cref="ClusterStatus.Offline" />).
/// </summary>
/// <param name="outpostDb">OutpostDbContext for the node row write.</param>
/// <param name="clusterDb">ClusterDbContext for the cluster status lookup.</param>
/// <param name="nodeRepo">Read surface for the by-id lookup (spec-driven).</param>
public sealed class HeartbeatCommandHandler(
    OutpostDbContext outpostDb,
    ClusterDbContext clusterDb,
    Repository<NodeRecord> nodeRepo) : ICommandHandler<HeartbeatCommand, HeartbeatResult>
{
    /// <inheritdoc />
    public async Task<HeartbeatResult> HandleAsync(
        HeartbeatCommand command,
        CancellationToken cancellationToken = default)
    {
        if (await clusterDb.Clusters
            .AsNoTracking()
            .Where(cluster => cluster.Id == command.ClusterId)
            .Select(cluster => (ClusterStatus?)cluster.Status)
            .FirstOrDefaultAsync(cancellationToken) is not { } clusterStatus)
        {
            throw new OutpostException(
                OutpostExceptions.ClusterNotFound,
                $"Cluster '{command.ClusterId}' not found.");
        }

        if (await nodeRepo.FirstOrDefaultAsync(
                new NodeByIdSpec(command.NodeId),
                cancellationToken) is not { } node
            || node.ClusterId != command.ClusterId)
        {
            throw new OutpostException(
                OutpostExceptions.NodeNotFound,
                $"Node '{command.NodeId}' in cluster '{command.ClusterId}' not found.");
        }

        var now = DateTimeOffset.UtcNow;
        var reloadedNode = await outpostDb.NodeRecords
            .FirstAsync(n => n.Id == command.NodeId, cancellationToken);
        outpostDb.Entry(reloadedNode).Property(static n => n.LastHeartbeatAt).CurrentValue = now;
        outpostDb.Entry(reloadedNode).Property(static n => n.IpAddress).CurrentValue = command.IpAddress;
        // Don't flip a draining node back to Ready mid-drain — operators
        // want the drain to complete cleanly. Likewise an Offline cluster
        // means the host is unreachable; keep the node in its terminal state.
        if (reloadedNode.Status != NodeStatus.Draining && clusterStatus != ClusterStatus.Offline)
        {
            outpostDb.Entry(reloadedNode).Property(static n => n.Status).CurrentValue = NodeStatus.Ready;
        }
        outpostDb.Entry(reloadedNode).Property(static n => n.Spec).CurrentValue = command.Spec;
        outpostDb.Entry(reloadedNode).Property(static n => n.UpdatedAt).CurrentValue = now;

        await outpostDb.SaveChangesAsync(cancellationToken);

        return new HeartbeatResult(command.NodeId, clusterStatus, DateTimeOffset.UtcNow);
    }
}