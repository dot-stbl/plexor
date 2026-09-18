// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// DeleteWorkloadCommandHandlerShould — the new provider-driven
// Delete semantics: tear down the VM through IComputeProvider,
// transition the row * → Deleting → Deleted, KEEP the row
// (forge.workloads keeps the audit trail of "this workload existed
// and was torn down at T"). The old soft-delete (db.Remove + no
// provider call) was replaced in this commit — see the handler
// itself for the rationale.
// ==========================================================================

using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Plexor.Modules.Clusters.Application.Clusters;
using Plexor.Modules.Clusters.Domain;
using Plexor.Modules.Clusters.Domain.Entities;
using Plexor.Modules.Clusters.Domain.Errors;
using Plexor.Modules.Clusters.Infrastructure.Clusters;
using Plexor.Modules.Clusters.Infrastructure.Persistence;
using Plexor.Shared.Identifiers;
using Plexor.Shared.Kernel.Compute;
using Plexor.Shared.Workloads;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Clusters.Unit.Workloads;

public sealed class DeleteWorkloadCommandHandlerShould
{
    [Fact(DisplayName = "Given existing Stopped workload, when DeleteWorkload, then transitions to Deleted + provider sees DeleteVmAsync")]
    public async Task DeleteWorkloadTransitionsToDeletedAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var cluster = await SeedClusterAsync(db);
        var providerHandle = await SeedWorkloadAsync(db, cluster.Id, WorkloadLifecycleState.Stopped);
        var provider = Substitute.For<IComputeProvider>();
        var sut = new DeleteWorkloadCommandHandler(db, provider);

        var result = await sut.HandleAsync(new DeleteWorkloadCommand(cluster.Id, providerHandle.WorkloadId));

        result.ShouldBe(Infrastructure.Clusters.Unit.Value);

        var row = await db.Workloads.AsNoTracking().FirstAsync();
        row.LifecycleState.ShouldBe(WorkloadLifecycleState.Deleted);
        // The row is preserved (audit + reconciliation), not removed.
        db.Workloads.Count().ShouldBe(1);
        var workloadId = row.Id;
        var events = await db.WorkloadLifecycleEvents.AsNoTracking()
            .Where(evt => evt.WorkloadId == workloadId)
            .ToListAsync();
        events.Count.ShouldBe(2);
        events[0].ToState.ShouldBe(WorkloadLifecycleState.Deleting);
        events[1].ToState.ShouldBe(WorkloadLifecycleState.Deleted);

        // The provider saw DeleteVmAsync exactly once with the right handle.
        await provider.Received(1).DeleteVmAsync(providerHandle.ProviderVmId!, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given non-existent workload, when DeleteWorkload, then throws WorkloadNotFound + provider is not called")]
    public async Task DeleteWorkloadRejectsUnknownIdAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var cluster = await SeedClusterAsync(db);
        var provider = Substitute.For<IComputeProvider>();
        var sut = new DeleteWorkloadCommandHandler(db, provider);
        var bogus = IdGenerator.NewWorkloadId();

        var ex = await Should.ThrowAsync<ClustersException>(
            () => sut.HandleAsync(new DeleteWorkloadCommand(cluster.Id, bogus)));

        ex.Code.ShouldBe(ClustersExceptions.WorkloadNotFound);
        await provider.DidNotReceive().DeleteVmAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given workload in different cluster, when DeleteWorkload, then throws WorkloadNotFound")]
    public async Task DeleteWorkloadRejectsWrongClusterAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var cluster1 = await SeedClusterAsync(db, "cluster-1");
        var cluster2 = await SeedClusterAsync(db, "cluster-2");
        var seeded = await SeedWorkloadAsync(db, cluster1.Id, WorkloadLifecycleState.Stopped);
        var provider = Substitute.For<IComputeProvider>();
        var sut = new DeleteWorkloadCommandHandler(db, provider);

        var ex = await Should.ThrowAsync<ClustersException>(
            () => sut.HandleAsync(new DeleteWorkloadCommand(cluster2.Id, seeded.WorkloadId)));

        ex.Code.ShouldBe(ClustersExceptions.WorkloadNotFound);
    }

    [Fact(DisplayName = "Given workload in Deleted state, when DeleteWorkload, then throws InvalidLifecycleTransition + provider is not called")]
    public async Task DeleteWorkloadRejectsAlreadyDeletedAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var cluster = await SeedClusterAsync(db);
        var now = DateTimeOffset.UtcNow;
        var id = IdGenerator.NewWorkloadId();
        // Seed a workload directly in the Deleted state (skipping
        // the state machine — the migration tests the existing row
        // shape, this test only asserts the state machine refuses).
        var workload = new Workload
        {
            Id = id,
            ClusterId = cluster.Id,
            Name = "already-deleted",
            Kind = "vm",
            SpecJson = "{}",
            State = WorkloadState.Stopped,
            LifecycleState = WorkloadLifecycleState.Deleted,
            ProviderVmId = $"noop-{Guid.NewGuid()}",
            CreatedAt = now,
            UpdatedAt = now,
        };
        await db.Workloads.AddAsync(workload);
        await db.SaveChangesAsync();

        var provider = Substitute.For<IComputeProvider>();
        var sut = new DeleteWorkloadCommandHandler(db, provider);

        await Should.ThrowAsync<InvalidWorkloadLifecycleTransitionException>(
            () => sut.HandleAsync(new DeleteWorkloadCommand(cluster.Id, id)));
        await provider.DidNotReceive().DeleteVmAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static async Task<Cluster> SeedClusterAsync(ClusterDbContext db, string? nameSuffix = null)
    {
        var now = DateTimeOffset.UtcNow;
        var cluster = new Cluster
        {
            Id = IdGenerator.NewClusterId(),
            OrgId = Guid.NewGuid(),
            Name = $"cluster-{nameSuffix ?? Guid.NewGuid().ToString("N")[..8]}",
            Region = "eu-central-1",
            Status = ClusterStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await db.Clusters.AddAsync(cluster);
        await db.SaveChangesAsync();
        return cluster;
    }

    /// <summary>
    ///     Seeds a workload in the requested lifecycle state, with a
    ///     matching <c>ProviderVmId</c> (the Delete handler looks it up
    ///     before calling the provider).
    /// </summary>
    private static async Task<(WorkloadId WorkloadId, string? ProviderVmId)> SeedWorkloadAsync(
        ClusterDbContext db, ClusterId clusterId, WorkloadLifecycleState lifecycleState)
    {
        var now = DateTimeOffset.UtcNow;
        var id = IdGenerator.NewWorkloadId();
        var providerVmId = $"noop-{Guid.NewGuid()}";
        // Compute the "matching" agent-reported State for the seeded
        // LifecycleState — the handler doesn't actually check this,
        // but the seed keeps the row consistent.
        var agentState = lifecycleState switch
        {
            WorkloadLifecycleState.Pending => WorkloadState.Provisioning,
            WorkloadLifecycleState.Provisioning => WorkloadState.Provisioning,
            WorkloadLifecycleState.Stopped => WorkloadState.Stopped,
            WorkloadLifecycleState.Running => WorkloadState.Running,
            WorkloadLifecycleState.Failed => WorkloadState.Failed,
            WorkloadLifecycleState.Deleting => WorkloadState.Stopped,
            WorkloadLifecycleState.Deleted => WorkloadState.Stopped,
            _ => WorkloadState.Stopped
        };
        await db.Workloads.AddAsync(new Workload
        {
            Id = id,
            ClusterId = clusterId,
            ProviderVmId = providerVmId,
            Name = $"wl-{Guid.NewGuid().ToString("N")[..8]}",
            Kind = "vm",
            SpecJson = "{}",
            State = agentState,
            LifecycleState = lifecycleState,
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync();
        return (id, providerVmId);
    }
}
