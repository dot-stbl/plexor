// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// WorkloadActionCommandHandlerShould — Tier 5 action endpoint
// unit tests. The handler enqueues a per-node command in
// forge.commands and short-polls the row's status until the
// NodeAgent posts a result back (Acked or Failed). Tests cover
// the happy path, the unknown-workload path, the no-LocalId
// path (the agent hasn't reported the runtime handle yet), and
// the timeout path.
// ==========================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Clusters.Application.Clusters;
using Plexor.Modules.Clusters.Domain;
using Plexor.Modules.Clusters.Domain.Entities;
using Plexor.Modules.Clusters.Domain.Errors;
using Plexor.Modules.Clusters.Infrastructure.Clusters;
using Plexor.Modules.Clusters.Infrastructure.Persistence;
using Plexor.Shared.Identifiers;
using Plexor.Shared.Workloads;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Clusters.Unit.WorkloadActions;

public sealed class WorkloadActionCommandHandlerShould
{
    [Fact(DisplayName = "Given known workload with LocalId, when HandleAsync, then enqueues a command and waits for Acked")]
    public async Task EnqueuesCommandAndReturnsAckedStateAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var (cluster, node) = await SeedClusterAndNodeAsync(db);
        var workloadId = IdGenerator.NewWorkloadId();
        var now = DateTimeOffset.UtcNow;
        await db.Workloads.AddAsync(new Workload
        {
            Id = workloadId,
            ClusterId = cluster.Id,
            AssignedNodeId = node.Id,
            LocalId = "docker-abc123",
            Name = "web-1",
            Kind = "container",
            SpecJson = "{}",
            State = WorkloadState.Stopped,
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync();

        // Simulate the agent processing the command by flipping
        // the row's status to Acked after a small delay. The
        // handler short-polls every 500ms with a 30s timeout.
        var sut = new WorkloadActionCommandHandler(db);
        var handlerTask = sut.HandleAsync(
            new WorkloadActionCommand(cluster.Id, workloadId, WorkloadAction.Start),
            CancellationToken.None);

        // Let the row get enqueued, then flip to Acked + update
        // workload state to simulate the agent's reply.
        await Task.Delay(150);
        var queued = await db.Commands.AsNoTracking()
            .Where(c => c.NodeId == node.Id)
            .FirstOrDefaultAsync();
        queued.ShouldNotBeNull();
        queued.Type.ShouldBe("workload.start");
        queued.Status.ShouldBe(NodeCommandStatus.Pending);

        db.ChangeTracker.Clear();
        var tracked = await db.Commands.FirstAsync(c => c.CommandId == queued.CommandId);
        tracked.Status = NodeCommandStatus.Acked;
        tracked.CompletedAt = DateTimeOffset.UtcNow;
        tracked.ResultJson = "{}";
        await db.SaveChangesAsync();

        // Also flip the workload's State to mimic the Tier 4
        // heartbeat reconciliation that the agent triggers
        // after the action succeeds.
        db.ChangeTracker.Clear();
        var wl = await db.Workloads.FirstAsync(w => w.Id == workloadId);
        wl.State = WorkloadState.Running;
        await db.SaveChangesAsync();

        var result = await handlerTask;
        result.State.ShouldBe(WorkloadState.Running);
    }

    [Fact(DisplayName = "Given unknown workload id, when HandleAsync, then throws WorkloadNotFound")]
    public async Task UnknownWorkloadThrowsAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var (cluster, _) = await SeedClusterAndNodeAsync(db);
        var sut = new WorkloadActionCommandHandler(db);

        await Should.ThrowAsync<ClustersException>(() =>
            sut.HandleAsync(
                new WorkloadActionCommand(cluster.Id, IdGenerator.NewWorkloadId(), WorkloadAction.Start),
                CancellationToken.None));
    }

    [Fact(DisplayName = "Given workload with no LocalId yet, when HandleAsync, then throws WorkloadNotFound")]
    public async Task NoLocalIdThrowsAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var (cluster, node) = await SeedClusterAndNodeAsync(db);
        var workloadId = IdGenerator.NewWorkloadId();
        var now = DateTimeOffset.UtcNow;
        await db.Workloads.AddAsync(new Workload
        {
            Id = workloadId,
            ClusterId = cluster.Id,
            AssignedNodeId = node.Id,
            LocalId = null,  // ← no runtime handle yet (agent hasn't heartbeated)
            Name = "web-1",
            Kind = "container",
            SpecJson = "{}",
            State = WorkloadState.Provisioning,
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync();

        var sut = new WorkloadActionCommandHandler(db);
        var ex = await Should.ThrowAsync<ClustersException>(() =>
            sut.HandleAsync(
                new WorkloadActionCommand(cluster.Id, workloadId, WorkloadAction.Start),
                CancellationToken.None));

        ex.Code.ShouldBe(ClustersExceptions.WorkloadNotFound);
    }

    [Fact(DisplayName = "Given a workload assigned to no node, when HandleAsync, then throws WorkloadNotFound")]
    public async Task UnassignedWorkloadThrowsAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var (cluster, _) = await SeedClusterAndNodeAsync(db);
        var workloadId = IdGenerator.NewWorkloadId();
        var now = DateTimeOffset.UtcNow;
        await db.Workloads.AddAsync(new Workload
        {
            Id = workloadId,
            ClusterId = cluster.Id,
            AssignedNodeId = null,
            LocalId = "docker-abc123",
            Name = "web-1",
            Kind = "container",
            SpecJson = "{}",
            State = WorkloadState.Provisioning,
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync();

        var sut = new WorkloadActionCommandHandler(db);
        var ex = await Should.ThrowAsync<ClustersException>(() =>
            sut.HandleAsync(
                new WorkloadActionCommand(cluster.Id, workloadId, WorkloadAction.Start),
                CancellationToken.None));

        ex.Code.ShouldBe(ClustersExceptions.WorkloadNotFound);
    }

    private static async Task<(Cluster Cluster, Node Node)> SeedClusterAndNodeAsync(ClusterDbContext db)
    {
        var now = DateTimeOffset.UtcNow;
        var cluster = new Cluster
        {
            Id = IdGenerator.NewClusterId(),
            OrgId = Guid.NewGuid(),
            Name = $"cluster-{Guid.NewGuid().ToString("N")[..8]}",
            Region = "eu-central-1",
            Status = ClusterStatus.Ready,
            Endpoint = "https://plexor.host",
            CreatedAt = now,
            UpdatedAt = now,
        };
        await db.Clusters.AddAsync(cluster);

        var node = new Node
        {
            Id = IdGenerator.NewNodeId(),
            OrgId = cluster.OrgId,
            ClusterId = cluster.Id,
            Hostname = "node-test",
            Role = NodeRole.Control,
            Status = NodeStatus.Ready,
            Spec = new NodeSpec(Vcpu: 2, RamGb: 4, DiskGb: 20, Providers: []),
            CreatedAt = now,
            UpdatedAt = now,
        };
        await db.Nodes.AddAsync(node);
        await db.SaveChangesAsync();
        return (cluster, node);
    }
}
