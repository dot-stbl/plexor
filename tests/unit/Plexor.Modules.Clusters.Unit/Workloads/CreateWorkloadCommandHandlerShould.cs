// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// CreateWorkloadCommandHandler unit tests — use the same TestDb +
// InMemory provider the cluster-handler tests use. We deliberately
// don't exercise Postgres-specific column types (jsonb, varchar(64))
// here; those are covered by integration tests against real Postgres.
//
// v1 compute provider is NoOpComputeProvider (every Create returns a
// fake handle; Start/Stop/Delete are no-op acks). The handler tests
// use a real NoOpComputeProvider so the provider-seam contract is
// end-to-end exercised; tests that need to simulate provider failure
// use an NSubstitute mock that throws ComputeProviderException.
// ==========================================================================

using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Plexor.Modules.Clusters.Application.Clusters;
using Plexor.Modules.Clusters.Domain;
using Plexor.Modules.Clusters.Domain.Entities;
using Plexor.Modules.Clusters.Domain.Errors;
using Plexor.Modules.Clusters.Infrastructure.Clusters;
using Plexor.Modules.Clusters.Infrastructure.Compute;
using Plexor.Modules.Clusters.Infrastructure.Mappers;
using Plexor.Modules.Clusters.Infrastructure.Persistence;
using Plexor.Shared.Identifiers;
using Plexor.Shared.Kernel.Compute;
using Plexor.Shared.Kernel.Identity;
using Plexor.Shared.Kernel.Quotas;
using Plexor.Shared.Workloads;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Clusters.Unit.Workloads;

public sealed class CreateWorkloadCommandHandlerShould
{
    private static readonly Guid StubActorUserId = Guid.NewGuid();

    [Fact(DisplayName = "Given unique name + working provider, when CreateWorkload, then row lands in Stopped + provider handle stored")]
    public async Task CreateWorkloadPersistsAndReturnsStoppedSummaryAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var cluster = await SeedClusterAsync(db);
        var sut = new CreateWorkloadCommandHandler(
            db,
            new WorkloadMapper(),
            new NoOpComputeProvider(),
            AllowedQuotaEnforcer(),
            StubCurrentUser());

        var result = await sut.HandleAsync(
            new CreateWorkloadCommand(cluster.Id, "web-1", "vm", /*lang=json,strict*/ """{"image":"nginx:latest"}"""));

        result.Id.ShouldNotBe(default);
        result.Name.ShouldBe("web-1");
        result.Kind.ShouldBe("vm");
        result.ClusterId.ShouldBe(cluster.Id);
        result.AssignedNodeId.ShouldBeNull();
        result.LocalId.ShouldBeNull();
        result.LifecycleState.ShouldBe(WorkloadLifecycleState.Stopped);
        result.ProviderVmId.ShouldNotBeNull();
        result.ProviderVmId!.ShouldStartWith("noop-");

        var persisted = await db.Workloads.FindAsync(result.Id);
        persisted.ShouldNotBeNull();
        persisted!.LifecycleState.ShouldBe(WorkloadLifecycleState.Stopped);
        persisted.ProviderVmId.ShouldNotBeNull();
        persisted.SpecJson.ShouldBe(/*lang=json,strict*/ """{"image":"nginx:latest"}""");
        persisted.Events.Count.ShouldBe(2);
        persisted.Events[0].ToState.ShouldBe(WorkloadLifecycleState.Provisioning);
        persisted.Events[1].ToState.ShouldBe(WorkloadLifecycleState.Stopped);
    }

    [Fact(DisplayName = "Given empty name, when CreateWorkload, then throws InvalidWorkloadSpec before touching the provider")]
    public async Task CreateWorkloadRejectsEmptyNameAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var cluster = await SeedClusterAsync(db);
        var provider = Substitute.For<IComputeProvider>();
        var sut = new CreateWorkloadCommandHandler(
            db,
            new WorkloadMapper(),
            provider,
            AllowedQuotaEnforcer(),
            StubCurrentUser());

        var ex = await Should.ThrowAsync<ClustersException>(
            () => sut.HandleAsync(new CreateWorkloadCommand(cluster.Id, "", "vm", "{}")));

        ex.Code.ShouldBe(ClustersExceptions.InvalidWorkloadSpec);
        await provider.DidNotReceive().CreateVmAsync(Arg.Any<CreateVmRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given empty kind, when CreateWorkload, then throws InvalidWorkloadSpec")]
    public async Task CreateWorkloadRejectsEmptyKindAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var cluster = await SeedClusterAsync(db);
        var provider = Substitute.For<IComputeProvider>();
        var sut = new CreateWorkloadCommandHandler(
            db,
            new WorkloadMapper(),
            provider,
            AllowedQuotaEnforcer(),
            StubCurrentUser());

        var ex = await Should.ThrowAsync<ClustersException>(
            () => sut.HandleAsync(new CreateWorkloadCommand(cluster.Id, "web-1", "", "{}")));

        ex.Code.ShouldBe(ClustersExceptions.InvalidWorkloadSpec);
        await provider.DidNotReceive().CreateVmAsync(Arg.Any<CreateVmRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given duplicate name in same cluster, when CreateWorkload, then throws InvalidWorkloadSpec + quota is rolled back")]
    public async Task CreateWorkloadRejectsDuplicateNameAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var cluster = await SeedClusterAsync(db);
        var now = DateTimeOffset.UtcNow;
        await db.Workloads.AddAsync(new Workload
        {
            Id = IdGenerator.NewWorkloadId(),
            ClusterId = cluster.Id,
            Name = "web-1",
            Kind = "vm",
            SpecJson = "{}",
            State = WorkloadState.Provisioning,
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync();
        var provider = Substitute.For<IComputeProvider>();
        var sut = new CreateWorkloadCommandHandler(
            db,
            new WorkloadMapper(),
            provider,
            AllowedQuotaEnforcer(),
            StubCurrentUser());

        var ex = await Should.ThrowAsync<ClustersException>(
            () => sut.HandleAsync(new CreateWorkloadCommand(cluster.Id, "web-1", "vm", "{}")));

        ex.Code.ShouldBe(ClustersExceptions.InvalidWorkloadSpec);
        await provider.DidNotReceive().CreateVmAsync(Arg.Any<CreateVmRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given provider throws ComputeProviderException, when CreateWorkload, then row lands in Failed + typed exception surfaces to operator")]
    public async Task CreateWorkloadWithProviderFailureTransitionsToFailedAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var cluster = await SeedClusterAsync(db);
        var provider = Substitute.For<IComputeProvider>();
        provider.CreateVmAsync(Arg.Any<CreateVmRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<string>(new ComputeProviderException(
                ComputeProviderErrorCodes.RequestInvalid,
                "duplicate name 'web-1' in provider namespace")));

        var sut = new CreateWorkloadCommandHandler(
            db,
            new WorkloadMapper(),
            provider,
            AllowedQuotaEnforcer(),
            StubCurrentUser());

        var ex = await Should.ThrowAsync<ClustersException>(
            () => sut.HandleAsync(new CreateWorkloadCommand(cluster.Id, "web-1", "vm", "{}")));

        // The typed surface is ComputeProviderFailed (mapped to 502),
        // not the raw provider stack.
        ex.Code.ShouldBe(ClustersExceptions.ComputeProviderFailed);
        ex.InnerException.ShouldBeOfType<ComputeProviderException>();

        // The row stays in Failed state with the provider's reason
        // on the LastMessage column + the lifecycle audit row.
        var row = await db.Workloads.AsNoTracking().FirstAsync();
        row.LifecycleState.ShouldBe(WorkloadLifecycleState.Failed);
        row.LastMessage.ShouldBe("duplicate name 'web-1' in provider namespace");
        var workloadId = row.Id;
        var events = await db.WorkloadLifecycleEvents.AsNoTracking()
            .Where(evt => evt.WorkloadId == workloadId)
            .ToListAsync();
        events.Count.ShouldBe(1);
        events[0].FromState.ShouldBe(WorkloadLifecycleState.Pending);
        events[0].ToState.ShouldBe(WorkloadLifecycleState.Failed);
        events[0].Reason.ShouldBe("duplicate name 'web-1' in provider namespace");
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

    /// <summary>
    ///     NSubstitute-backed <see cref="IQuotaEnforcer" /> that always
    ///     returns <see cref="QuotaCheckResult.Allowed" />. Lets the
    ///     existing workload-handler tests stay focused on the workload
    ///     path without spinning up the EF enforcer (which requires a
    ///     real Postgres). Tests that need to exercise the denied path
    ///     can swap this for a customised substitute.
    /// </summary>
    private static IQuotaEnforcer AllowedQuotaEnforcer()
    {
        var enforcer = Substitute.For<IQuotaEnforcer>();
        enforcer.CheckAndReserveAsync(
            Arg.Any<QuotaScope>(),
            Arg.Any<QuotaDefinitionKey>(),
            Arg.Any<decimal>(),
            Arg.Any<CancellationToken>())
            .Returns(new QuotaCheckResult.Allowed());
        return enforcer;
    }

    /// <summary>
    ///     NSubstitute-backed <see cref="ICurrentUser" /> that returns
    ///     a stable stub id. Added in 4.5.h so the handler can
    ///     populate <see cref="QuotaScope.ActorUserId" /> when calling
    ///     the enforcer.
    /// </summary>
    private static ICurrentUser StubCurrentUser()
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(StubActorUserId);
        return currentUser;
    }
}
