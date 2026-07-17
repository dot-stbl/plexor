// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodeHeartbeatCommandHandlerShould — Phase D Tier 4 drift
// detection unit tests. The handler reconciles the agent's per-
// workload state reports against the durable forge.workloads
// view; we exercise the happy path (report matches a known
// workload) and the drift path (report names a workload we
// never provisioned).
//
// The InMemory provider used here doesn't support
// ExecuteUpdateAsync, so tests that need to flip a row's status
// pre-Heartbeat load the row, mutate via tracked entity, save
// (no-op), then call the handler — which is the same plumbing
// the production path runs.
// ==========================================================================

using Plexor.Modules.Clusters.Application.Clusters;
using Plexor.Modules.Clusters.Domain;
using Plexor.Modules.Clusters.Domain.Entities;
using Plexor.Modules.Clusters.Domain.Errors;
using Plexor.Modules.Clusters.Infrastructure.Clusters;
using Plexor.Modules.Clusters.Infrastructure.Persistence;
using Plexor.Shared.Identifiers;
using Plexor.Shared.NodeApi;
using Plexor.Shared.Workloads;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Clusters.Unit.Clusters;

public sealed class NodeHeartbeatCommandHandlerShould
{
    [Fact(DisplayName = "Given matching report, when HandleAsync, then reconciles State + LocalId + LastReportedAt")]
    public async Task ReconcilesReports()
    {
        await using var db = await TestDb.CreateAsync();
        var (cluster, node) = await SeedClusterAndNodeAsync(db);

        var workloadId = IdGenerator.NewWorkloadId();
        var workload = new Workload
        {
            Id = workloadId,
            ClusterId = cluster.Id,
            AssignedNodeId = node.Id,
            LocalId = null,
            Name = "web-1",
            Kind = "container",
            SpecJson = "{}",
            State = WorkloadState.Provisioning,
            LastReportedAt = null,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        await db.Workloads.AddAsync(workload);
        await db.SaveChangesAsync();

        var sut = new NodeHeartbeatCommandHandler(db);

        var result = await sut.HandleAsync(new NodeHeartbeatCommand(
            node.Id,
            cluster.Id,
            new NodeSpec(Vcpu: 2, RamGb: 4, DiskGb: 20, Providers: []),
            Reports:
            [
                new WorkloadReport(
                    WorkloadId: workloadId.Value,
                    LocalId: "docker-abc123",
                    Name: "web-1",
                    State: WorkloadReportState.Running)
            ]));

        result.NodeId.ShouldBe(node.Id);

        // Detached re-fetch — the handler modifies via tracked
        // change tracking; we want the post-SaveChangesAsync
        // snapshot. InMemory provider doesn't support ExecuteUpdate
        // so we reuse the tracked load through db.Workloads.
        var refreshed = await db.Workloads.FindAsync(workloadId);
        refreshed.ShouldNotBeNull();
        refreshed!.State.ShouldBe(WorkloadState.Running);
        refreshed.LocalId.ShouldBe("docker-abc123");
        refreshed.LastReportedAt.ShouldNotBeNull();
    }

    [Fact(DisplayName = "Given empty reports, when HandleAsync, then heartbeat still flips node to Ready")]
    public async Task NoReportsStillStampsHeartbeat()
    {
        await using var db = await TestDb.CreateAsync();
        // Seed the node in Gone state so we can observe the
        // status flip to Ready in this heartbeat. Node.Status is
        // init-only, so we seed the desired state instead of
        // mutating tracked.
        var (cluster, node) = await SeedClusterAndNodeAsync(db, status: NodeStatus.Gone);

        var sut = new NodeHeartbeatCommandHandler(db);
        var result = await sut.HandleAsync(new NodeHeartbeatCommand(
            node.Id,
            cluster.Id,
            new NodeSpec(2, 4, 20, []),
            Reports: []));

        result.NodeId.ShouldBe(node.Id);

        db.ChangeTracker.Clear();
        var reloaded = await db.Nodes.FindAsync(node.Id);
        reloaded!.Status.ShouldBe(NodeStatus.Ready);
        reloaded.LastHeartbeatAt.ShouldNotBeNull();
    }

    [Fact(DisplayName = "Given report naming a workload the cluster never provisioned, when HandleAsync, then throws WorkloadNotFound (drift)")]
    public async Task UnknownReportIsDrift()
    {
        await using var db = await TestDb.CreateAsync();
        var (cluster, node) = await SeedClusterAndNodeAsync(db);
        var sut = new NodeHeartbeatCommandHandler(db);

        var bogusReport = new WorkloadReport(
            WorkloadId: IdGenerator.NewWorkloadId().Value,
            LocalId: "docker-orphan",
            Name: "phantom-vm",
            State: WorkloadReportState.Running);

        var ex = await Should.ThrowAsync<ClustersException>(() =>
            sut.HandleAsync(new NodeHeartbeatCommand(
                node.Id,
                cluster.Id,
                new NodeSpec(2, 4, 20, []),
                Reports: [bogusReport])));

        ex.Code.ShouldBe(ClustersExceptions.WorkloadNotFound);
    }

    private static async Task<(Cluster Cluster, Node Node)> SeedClusterAndNodeAsync(ClusterDbContext db)
    {
        return await SeedClusterAndNodeAsync(db, status: NodeStatus.Pending);
    }

    private static async Task<(Cluster Cluster, Node Node)> SeedClusterAndNodeAsync(
        ClusterDbContext db,
        NodeStatus status)
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
            Status = status,
            Spec = new NodeSpec(Vcpu: 2, RamGb: 4, DiskGb: 20, Providers: []),
            CreatedAt = now,
            UpdatedAt = now,
        };
        await db.Nodes.AddAsync(node);
        await db.SaveChangesAsync();
        return (cluster, node);
    }
}
