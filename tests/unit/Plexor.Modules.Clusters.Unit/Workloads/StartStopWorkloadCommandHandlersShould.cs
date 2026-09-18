// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// StartWorkloadCommandHandler / StopWorkloadCommandHandler — provider-
// driven lifecycle transitions. Use NSubstitute for IComputeProvider
// (handler tests should assert the right call + the right transition;
// the provider's own contract is covered by NoOpComputeProviderTests).
// ==========================================================================

using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Plexor.Modules.Clusters.Application.Clusters;
using Plexor.Modules.Clusters.Domain;
using Plexor.Modules.Clusters.Domain.Entities;
using Plexor.Modules.Clusters.Domain.Errors;
using Plexor.Modules.Clusters.Infrastructure.Clusters;
using Plexor.Modules.Clusters.Infrastructure.Mappers;
using Plexor.Modules.Clusters.Infrastructure.Persistence;
using Plexor.Shared.Identifiers;
using Plexor.Shared.Kernel.Compute;
using Plexor.Shared.Workloads;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Clusters.Unit.Workloads;

public sealed class StartStopWorkloadCommandHandlersShould
{
    [Fact(DisplayName = "Given Stopped workload with provider handle, when StartWorkload, then transitions to Running + provider sees StartVmAsync")]
    public async Task StartWorkloadOnStoppedVmTransitionsToRunningAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var cluster = await SeedClusterAsync(db);
        var seeded = await SeedWorkloadAsync(db, cluster.Id, WorkloadLifecycleState.Stopped);
        var provider = Substitute.For<IComputeProvider>();
        var sut = new StartWorkloadCommandHandler(db, provider, new WorkloadMapper());

        var result = await sut.HandleAsync(new StartWorkloadCommand(cluster.Id, seeded.WorkloadId));

        result.Workload.LifecycleState.ShouldBe(WorkloadLifecycleState.Running);
        var row = await db.Workloads.AsNoTracking().FirstAsync();
        row.LifecycleState.ShouldBe(WorkloadLifecycleState.Running);
        var workloadId = row.Id;
        var events = await db.WorkloadLifecycleEvents.AsNoTracking()
            .Where(evt => evt.WorkloadId == workloadId)
            .ToListAsync();
        events[^1].FromState.ShouldBe(WorkloadLifecycleState.Stopped);
        events[^1].ToState.ShouldBe(WorkloadLifecycleState.Running);
        await provider.Received(1).StartVmAsync(seeded.ProviderVmId!, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given Deleted workload, when StartWorkload, then throws InvalidLifecycleTransition + provider is not called")]
    public async Task StartWorkloadOnDeletedVmThrowsAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var cluster = await SeedClusterAsync(db);
        var seeded = await SeedWorkloadAsync(db, cluster.Id, WorkloadLifecycleState.Deleted);
        var provider = Substitute.For<IComputeProvider>();
        var sut = new StartWorkloadCommandHandler(db, provider, new WorkloadMapper());

        await Should.ThrowAsync<InvalidWorkloadLifecycleTransitionException>(
            () => sut.HandleAsync(new StartWorkloadCommand(cluster.Id, seeded.WorkloadId)));
        await provider.DidNotReceive().StartVmAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given Running workload, when StartWorkload, then throws (Stop required before re-Start)")]
    public async Task StartWorkloadOnRunningVmThrowsAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var cluster = await SeedClusterAsync(db);
        var seeded = await SeedWorkloadAsync(db, cluster.Id, WorkloadLifecycleState.Running);
        var provider = Substitute.For<IComputeProvider>();
        var sut = new StartWorkloadCommandHandler(db, provider, new WorkloadMapper());

        await Should.ThrowAsync<InvalidWorkloadLifecycleTransitionException>(
            () => sut.HandleAsync(new StartWorkloadCommand(cluster.Id, seeded.WorkloadId)));
        await provider.DidNotReceive().StartVmAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given workload without provider_vm_id, when StartWorkload, then throws WorkloadNotFound + provider not called")]
    public async Task StartWorkloadOnPendingVmWithoutHandleThrowsAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var cluster = await SeedClusterAsync(db);
        var now = DateTimeOffset.UtcNow;
        var id = IdGenerator.NewWorkloadId();
        await db.Workloads.AddAsync(new Workload
        {
            Id = id,
            ClusterId = cluster.Id,
            ProviderVmId = null,
            Name = "no-handle",
            Kind = "vm",
            SpecJson = "{}",
            State = WorkloadState.Provisioning,
            LifecycleState = WorkloadLifecycleState.Pending,
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync();

        var provider = Substitute.For<IComputeProvider>();
        var sut = new StartWorkloadCommandHandler(db, provider, new WorkloadMapper());

        var ex = await Should.ThrowAsync<ClustersException>(
            () => sut.HandleAsync(new StartWorkloadCommand(cluster.Id, id)));
        ex.Code.ShouldBe(ClustersExceptions.WorkloadNotFound);
        await provider.DidNotReceive().StartVmAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given provider throws ComputeProviderException, when StartWorkload, then row lands in Failed + typed exception surfaces")]
    public async Task StartWorkloadWithProviderFailureTransitionsToFailedAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var cluster = await SeedClusterAsync(db);
        var seeded = await SeedWorkloadAsync(db, cluster.Id, WorkloadLifecycleState.Stopped);
        var provider = Substitute.For<IComputeProvider>();
        provider.StartVmAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new ComputeProviderException(
                ComputeProviderErrorCodes.Unreachable,
                "libvirt endpoint unreachable")));

        var sut = new StartWorkloadCommandHandler(db, provider, new WorkloadMapper());

        var ex = await Should.ThrowAsync<ClustersException>(
            () => sut.HandleAsync(new StartWorkloadCommand(cluster.Id, seeded.WorkloadId)));
        ex.Code.ShouldBe(ClustersExceptions.ComputeProviderFailed);

        var row = await db.Workloads.AsNoTracking().FirstAsync();
        row.LifecycleState.ShouldBe(WorkloadLifecycleState.Failed);
        row.LastMessage.ShouldBe("libvirt endpoint unreachable");
    }

    [Fact(DisplayName = "Given Running workload, when StopWorkload, then transitions to Stopped + provider sees StopVmAsync")]
    public async Task StopWorkloadOnRunningVmTransitionsToStoppedAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var cluster = await SeedClusterAsync(db);
        var seeded = await SeedWorkloadAsync(db, cluster.Id, WorkloadLifecycleState.Running);
        var provider = Substitute.For<IComputeProvider>();
        var sut = new StopWorkloadCommandHandler(db, provider, new WorkloadMapper());

        var result = await sut.HandleAsync(new StopWorkloadCommand(cluster.Id, seeded.WorkloadId));

        result.Workload.LifecycleState.ShouldBe(WorkloadLifecycleState.Stopped);
        await provider.Received(1).StopVmAsync(seeded.ProviderVmId!, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given Stopped workload, when StopWorkload, then throws (must be Running)")]
    public async Task StopWorkloadOnStoppedVmThrowsAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var cluster = await SeedClusterAsync(db);
        var seeded = await SeedWorkloadAsync(db, cluster.Id, WorkloadLifecycleState.Stopped);
        var provider = Substitute.For<IComputeProvider>();
        var sut = new StopWorkloadCommandHandler(db, provider, new WorkloadMapper());

        await Should.ThrowAsync<InvalidWorkloadLifecycleTransitionException>(
            () => sut.HandleAsync(new StopWorkloadCommand(cluster.Id, seeded.WorkloadId)));
        await provider.DidNotReceive().StopVmAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given Failed workload, when DeleteWorkload, then transitions through Deleting → Deleted")]
    public async Task DeleteWorkloadOnFailedVmTransitionsToDeletedAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var cluster = await SeedClusterAsync(db);
        var seeded = await SeedWorkloadAsync(db, cluster.Id, WorkloadLifecycleState.Failed);
        var provider = Substitute.For<IComputeProvider>();
        var sut = new DeleteWorkloadCommandHandler(db, provider);

        await sut.HandleAsync(new DeleteWorkloadCommand(cluster.Id, seeded.WorkloadId));

        var row = await db.Workloads.AsNoTracking().FirstAsync();
        row.LifecycleState.ShouldBe(WorkloadLifecycleState.Deleted);
        var workloadId = row.Id;
        var events = await db.WorkloadLifecycleEvents.AsNoTracking()
            .Where(evt => evt.WorkloadId == workloadId)
            .ToListAsync();
        events[^2].ToState.ShouldBe(WorkloadLifecycleState.Deleting);
        events[^1].ToState.ShouldBe(WorkloadLifecycleState.Deleted);
        await provider.Received(1).DeleteVmAsync(seeded.ProviderVmId!, Arg.Any<CancellationToken>());
    }

    private static async Task<Cluster> SeedClusterAsync(ClusterDbContext db)
    {
        var now = DateTimeOffset.UtcNow;
        var cluster = new Cluster
        {
            Id = IdGenerator.NewClusterId(),
            OrgId = Guid.NewGuid(),
            Name = $"cluster-{Guid.NewGuid():N}",
            Region = "eu-central-1",
            Status = ClusterStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await db.Clusters.AddAsync(cluster);
        await db.SaveChangesAsync();
        return cluster;
    }

    private static async Task<(WorkloadId WorkloadId, string? ProviderVmId)> SeedWorkloadAsync(
        ClusterDbContext db, ClusterId clusterId, WorkloadLifecycleState lifecycleState)
    {
        var now = DateTimeOffset.UtcNow;
        var id = IdGenerator.NewWorkloadId();
        var providerVmId = $"noop-{Guid.NewGuid()}";
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
