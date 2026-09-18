// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// CreateVmHandlerShould — unit tests for the VM-specific provisioning
// flow. Mirrors the existing CreateWorkloadCommandHandler test
// shape (real ClusterDbContext via TestDb + NSubstitute for the
// INodeRegistry seam) and adds coverage for the Flavor/Image
// catalog resolution + Config overlay.
// ==========================================================================

using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Plexor.Modules.Clusters.Application.CreateVm;
using Plexor.Modules.Clusters.Application.Flavors;
using Plexor.Modules.Clusters.Application.Images;
using Plexor.Modules.Clusters.Domain;
using Plexor.Modules.Clusters.Domain.Entities;
using Plexor.Modules.Clusters.Domain.Errors;
using Plexor.Modules.Clusters.Infrastructure.Clusters;
using Plexor.Modules.Clusters.Infrastructure.Persistence;
using Plexor.Modules.Clusters.Infrastructure.Placement;
using Plexor.Modules.Outpost.Application.Abstractions;
using Plexor.Shared.Identifiers;
using Plexor.Shared.NodeApi;
using Plexor.Shared.Workloads;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Clusters.Unit.CreateVm;

public sealed class CreateVmHandlerShould
{
    [Fact(DisplayName = "Given a valid request, when HandleAsync, then persists a VM Workload row and returns its id")]
    public async Task HandleAsyncCreatesVmWorkloadAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var cluster = await SeedClusterAsync(db);
        var nodeId = IdGenerator.NewNodeId();
        var nodeRegistry = Substitute.For<INodeRegistry>();
        nodeRegistry.ListNodesAsync(cluster.Id, Arg.Any<CancellationToken>())
            .Returns([NodeRecordHelper.NewReadyNode(nodeId)]);

        var flavorCatalog = new DefaultFlavorCatalog();
        var imageCatalog = new DefaultImageCatalog();
        var scheduler = new ManualPlacementScheduler();
        var candidateLoader = new PlacementCandidateLoader(nodeRegistry);
        var sut = new CreateVmHandler(
            db,
            flavorCatalog,
            imageCatalog,
            scheduler,
            candidateLoader,
            TimeProvider.System);

        var result = await sut.HandleAsync(
            new CreateVmCommand(
                cluster.Id,
                "vm-alpha",
                FlavorName: "small",
                TargetNodeId: nodeId),
            CancellationToken.None);

        result.WorkloadId.Value.ShouldNotBe(Guid.Empty);
        result.AssignedNodeId.ShouldBe(nodeId);

        var persisted = await db.Workloads.AsNoTracking()
            .FirstAsync(w => w.Id == result.WorkloadId);
        persisted.Name.ShouldBe("vm-alpha");
        persisted.Kind.ShouldBe("vm");
        persisted.AssignedNodeId.ShouldBe(nodeId);
        persisted.State.ShouldBe(WorkloadState.Provisioning);
    }

    [Fact(DisplayName = "Given an empty name, when HandleAsync, then throws InvalidWorkloadSpec")]
    public async Task HandleAsyncRejectsEmptyNameAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var cluster = await SeedClusterAsync(db);
        var sut = NewHandler(db);

        var ex = await Should.ThrowAsync<ClustersException>(
            () => sut.HandleAsync(new CreateVmCommand(cluster.Id, "  "), CancellationToken.None));

        ex.Code.ShouldBe(ClustersExceptions.InvalidWorkloadSpec);
    }

    [Fact(DisplayName = "Given an unknown Flavor name, when HandleAsync, then throws InvalidWorkloadSpec")]
    public async Task HandleAsyncRejectsUnknownFlavorAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var cluster = await SeedClusterAsync(db);
        var sut = NewHandler(db);

        var ex = await Should.ThrowAsync<ClustersException>(
            () => sut.HandleAsync(
                new CreateVmCommand(cluster.Id, "vm-x", FlavorName: "nonexistent"),
                CancellationToken.None));

        ex.Code.ShouldBe(ClustersExceptions.InvalidWorkloadSpec);
        ex.Message.ShouldContain("nonexistent");
    }

    [Fact(DisplayName = "Given an unknown Image name, when HandleAsync, then throws InvalidWorkloadSpec")]
    public async Task HandleAsyncRejectsUnknownImageAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var cluster = await SeedClusterAsync(db);
        var sut = NewHandler(db);

        var ex = await Should.ThrowAsync<ClustersException>(
            () => sut.HandleAsync(
                new CreateVmCommand(cluster.Id, "vm-x", ImageName: "nonexistent"),
                CancellationToken.None));

        ex.Code.ShouldBe(ClustersExceptions.InvalidWorkloadSpec);
        ex.Message.ShouldContain("nonexistent");
    }

    [Fact(DisplayName = "Given a Config overlay with invalid Vcpu, when HandleAsync, then throws with Vcpu error")]
    public async Task HandleAsyncRejectsInvalidOverlayAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var cluster = await SeedClusterAsync(db);
        var sut = NewHandler(db);

        var ex = await Should.ThrowAsync<ClustersException>(
            () => sut.HandleAsync(
                new CreateVmCommand(
                    cluster.Id,
                    "vm-x",
                    FlavorName: "small",
                    Config: new VmRuntimeConfig(
                        Vcpu: 999,
                        RamBytes: 4L * 1024 * 1024 * 1024,
                        DiskBytes: 40L * 1024 * 1024 * 1024,
                        ImageRef: "ubuntu-22.04-cloud",
                        NetworkName: null)),
                CancellationToken.None));

        ex.Code.ShouldBe(ClustersExceptions.InvalidWorkloadSpec);
        ex.Message.ShouldContain("Vcpu");
    }

    [Fact(DisplayName = "Given a name that conflicts with an existing workload, when HandleAsync, then throws InvalidWorkloadSpec")]
    public async Task HandleAsyncRejectsDuplicateNameAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var cluster = await SeedClusterAsync(db);
        await SeedWorkloadAsync(db, cluster.Id, "vm-taken");
        var sut = NewHandler(db);

        var ex = await Should.ThrowAsync<ClustersException>(
            () => sut.HandleAsync(new CreateVmCommand(cluster.Id, "vm-taken"), CancellationToken.None));

        ex.Code.ShouldBe(ClustersExceptions.InvalidWorkloadSpec);
        ex.Message.ShouldContain("already exists");
    }

    [Fact(DisplayName = "Given no Flavor name, when HandleAsync, then resolves to the first catalog flavor (default)")]
    public async Task HandleAsyncUsesFirstCatalogFlavorAsDefaultAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var cluster = await SeedClusterAsync(db);
        var nodeId = IdGenerator.NewNodeId();
        var nodeRegistry = Substitute.For<INodeRegistry>();
        nodeRegistry.ListNodesAsync(cluster.Id, Arg.Any<CancellationToken>())
            .Returns([NodeRecordHelper.NewReadyNode(nodeId)]);

        var flavorCatalog = Substitute.For<IFlavorCatalog>();
        flavorCatalog.List().Returns(
            [
                new Flavor(
                    Name: "small",
                    Default: new VmRuntimeConfig(
                        Vcpu: 1,
                        RamBytes: 2L * 1024 * 1024 * 1024,
                        DiskBytes: 20L * 1024 * 1024 * 1024,
                        ImageRef: "ubuntu-22.04-cloud",
                        NetworkName: null)),
            ]);

        var sut = new CreateVmHandler(
            db,
            flavorCatalog,
            new DefaultImageCatalog(),
            new ManualPlacementScheduler(),
            new PlacementCandidateLoader(nodeRegistry),
            TimeProvider.System);

        var result = await sut.HandleAsync(
            new CreateVmCommand(cluster.Id, "vm-default"),
            CancellationToken.None);

        result.WorkloadId.Value.ShouldNotBe(Guid.Empty);
        flavorCatalog.DidNotReceive().Get(Arg.Any<string>());
    }

    [Fact(DisplayName = "Given a Config overlay that overrides Vcpu/Ram/Disk, when HandleAsync, then the persisted SpecJson reflects the overlay")]
    public async Task HandleAsyncAppliesConfigOverlayAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var cluster = await SeedClusterAsync(db);
        var nodeId = IdGenerator.NewNodeId();
        var nodeRegistry = Substitute.For<INodeRegistry>();
        nodeRegistry.ListNodesAsync(cluster.Id, Arg.Any<CancellationToken>())
            .Returns([NodeRecordHelper.NewReadyNode(nodeId)]);

        var sut = new CreateVmHandler(
            db,
            new DefaultFlavorCatalog(),
            new DefaultImageCatalog(),
            new ManualPlacementScheduler(),
            new PlacementCandidateLoader(nodeRegistry),
            TimeProvider.System);

        var result = await sut.HandleAsync(
            new CreateVmCommand(
                cluster.Id,
                "vm-custom",
                FlavorName: "small",
                Config: new VmRuntimeConfig(
                    Vcpu: 8,
                    RamBytes: 16L * 1024 * 1024 * 1024,
                    DiskBytes: 160L * 1024 * 1024 * 1024,
                    ImageRef: "ubuntu-22.04-cloud",
                    NetworkName: "prod-vpc")),
            CancellationToken.None);

        var persisted = await db.Workloads.AsNoTracking()
            .FirstAsync(w => w.Id == result.WorkloadId);
        persisted.SpecJson.ShouldContain("\"Vcpu\":8");
        persisted.SpecJson.ShouldContain("\"NetworkName\":\"prod-vpc\"");
    }

    [Fact(DisplayName = "Given an assigned node, when HandleAsync, then enqueues a workload.create NodeCommand")]
    public async Task HandleAsyncEnqueuesWorkloadCreateCommandAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var cluster = await SeedClusterAsync(db);
        var nodeId = IdGenerator.NewNodeId();
        var nodeRegistry = Substitute.For<INodeRegistry>();
        nodeRegistry.ListNodesAsync(cluster.Id, Arg.Any<CancellationToken>())
            .Returns([NodeRecordHelper.NewReadyNode(nodeId)]);

        var sut = new CreateVmHandler(
            db,
            new DefaultFlavorCatalog(),
            new DefaultImageCatalog(),
            new ManualPlacementScheduler(),
            new PlacementCandidateLoader(nodeRegistry),
            TimeProvider.System);

        await sut.HandleAsync(
            new CreateVmCommand(
                cluster.Id,
                "vm-cmd",
                FlavorName: "small",
                TargetNodeId: nodeId),
            CancellationToken.None);

        var enqueued = await db.Commands.AsNoTracking()
            .FirstOrDefaultAsync(c => c.NodeId.Value == nodeId.Value);
        enqueued.ShouldNotBeNull();
        enqueued.Type.ShouldBe("workload.create");
        enqueued.Status.ShouldBe(NodeCommandStatus.Pending);
        enqueued.PayloadJson.ShouldContain("\"Kind\":{\"Name\":\"vm\"}");
        enqueued.PayloadJson.ShouldContain("\"Spec\":");
    }

    [Fact(DisplayName = "Given no candidate nodes (scheduler returns null), when HandleAsync, then no NodeCommand is enqueued")]
    public async Task HandleAsyncDoesNotEnqueueWhenSchedulerCantPlaceAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var cluster = await SeedClusterAsync(db);
        var nodeRegistry = Substitute.For<INodeRegistry>();
        nodeRegistry.ListNodesAsync(cluster.Id, Arg.Any<CancellationToken>())
            .Returns([]);  // no candidates

        var sut = new CreateVmHandler(
            db,
            new DefaultFlavorCatalog(),
            new DefaultImageCatalog(),
            new ManualPlacementScheduler(),
            new PlacementCandidateLoader(nodeRegistry),
            TimeProvider.System);

        var result = await sut.HandleAsync(
            new CreateVmCommand(cluster.Id, "vm-unassigned"),
            CancellationToken.None);

        result.AssignedNodeId.ShouldBeNull();
        result.WorkloadId.Value.ShouldNotBe(Guid.Empty);

        var enqueued = await db.Commands.AsNoTracking()
            .FirstOrDefaultAsync(static c => c.NodeId.Value != Guid.Empty);
        enqueued.ShouldBeNull();
    }

    private static CreateVmHandler NewHandler(ClusterDbContext db)
    {
        var nodeRegistry = Substitute.For<INodeRegistry>();
        nodeRegistry.ListNodesAsync(Arg.Any<ClusterId>(), Arg.Any<CancellationToken>())
            .Returns([]);
        return new CreateVmHandler(
            db,
            new DefaultFlavorCatalog(),
            new DefaultImageCatalog(),
            new ManualPlacementScheduler(),
            new PlacementCandidateLoader(nodeRegistry),
            TimeProvider.System);
    }

    private static async Task<Cluster> SeedClusterAsync(ClusterDbContext db)
    {
        var now = DateTimeOffset.UtcNow;
        var cluster = new Cluster
        {
            Id = IdGenerator.NewClusterId(),
            OrgId = Guid.NewGuid(),
            Name = $"cluster-{Guid.NewGuid():N}"[..16],
            Region = "eu-central-1",
            Status = ClusterStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await db.Clusters.AddAsync(cluster);
        await db.SaveChangesAsync();
        return cluster;
    }

    private static async Task SeedWorkloadAsync(ClusterDbContext db, ClusterId clusterId, string name)
    {
        var now = DateTimeOffset.UtcNow;
        await db.Workloads.AddAsync(new Workload
        {
            Id = IdGenerator.NewWorkloadId(),
            ClusterId = clusterId,
            Name = name,
            Kind = "vm",
            SpecJson = "{}",
            State = WorkloadState.Provisioning,
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync();
    }
}

/// <summary>
///     Test helper — builds a minimal <see cref="Plexor.Modules.Outpost.Application.NodeRecord" />
///     with Status=Ready. Lives in the test file because no other
///     test needs it; promoting to a shared builder is a future
///     sprint when more Outpost-shaped unit tests appear.
/// </summary>
file static class NodeRecordHelper
{
    public static Plexor.Modules.Outpost.Application.NodeRecord NewReadyNode(NodeId id)
    {
        var now = DateTimeOffset.UtcNow;
        return new Plexor.Modules.Outpost.Application.NodeRecord
        {
            Id = id,
            ClusterId = new ClusterId(Guid.Empty),
            Hostname = $"node-{Guid.NewGuid():N}"[..14],
            IpAddress = "10.0.0.1",
            Role = NodeRole.Compute,
            Status = Outpost.Application.NodeStatus.Ready,
            Spec = new Plexor.Modules.Outpost.Application.NodeSpec(
                Vcpu: 16,
                RamGb: 64,
                DiskGb: 1024,
                Providers: ["kvm"]),
            VmCount = 0,
            WireguardPublicKey = string.Empty,
            LastHeartbeatAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }
}
