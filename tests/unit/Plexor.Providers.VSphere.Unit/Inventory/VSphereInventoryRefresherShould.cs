// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VSphereInventoryRefresherShould — exercise the inventory refresher
// in isolation. Pins the contract:
//   1. RefreshAsync calls every list endpoint on the Refit client.
//   2. The returned snapshot id matches a row in the snapshots table.
//   3. The per-cluster / per-host / per-VM rows land in their
//      respective tables with the expected counts.
//   4. The header row carries the counts + the wall-clock from the
//      injected TimeProvider.
// ============================================================================

// The test namespace Plexor.Providers.VSphere.Unit.Inventory shadows
// the production namespace Plexor.Providers.VSphere.Inventory when
// resolving "Inventory.*" — disambiguate via global::.
using global::Plexor.Providers.VSphere.Inventory.ReadModels;
using global::Plexor.Providers.VSphere.Inventory.Workloads;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Plexor.Providers.VSphere.Infrastructure.Inventory;
using Shouldly;
using Xunit;

namespace Plexor.Providers.VSphere.Unit.Inventory;

/// <summary>
///     Behavioural tests for
///     <see cref="VSphereInventoryRefresher" />. The refresher pulls
///     every list endpoint through the Refit client, writes the
///     results as a new snapshot row + child cluster / host / VM
///     rows, and returns the snapshot id. The tests substitute the
///     <see cref="IVSphereClient" /> with NSubstitute so the vCenter
///     network calls are bypassed.
/// </summary>
public sealed class VSphereInventoryRefresherShould
{
    /// <summary>
    ///     Given a mocked client that returns one datacenter, two
    ///     clusters, three hosts, four VMs, when RefreshAsync runs,
    ///     then one snapshot header row is written with the
    ///     correct counts.
    /// </summary>
    [Fact(DisplayName = "Given mocked vCenter, when RefreshAsync runs, then writes one snapshot header with the counts")]
    public async Task RefreshAsyncWritesSnapshotHeaderWithCorrectCountsAsync()
    {
        var client = Substitute.For<IVSphereClient>();
        client.ListDatacentersAsync(Arg.Any<CancellationToken>())
            .Returns([new DatacenterSummary { Moref = "datacenter-1", Name = "DC1" }]);
        client.ListClustersAsync(Arg.Any<CancellationToken>())
            .Returns([
                new ClusterSummary { Moref = "domain-c1", Name = "Cluster1", DatacenterMoref = "datacenter-1" },
                new ClusterSummary { Moref = "domain-c2", Name = "Cluster2", DatacenterMoref = "datacenter-1" },
            ]);
        client.ListHostsAsync(Arg.Any<CancellationToken>())
            .Returns([
                new HostSummary { Moref = "host-1", Name = "esxi-1", ClusterMoref = "domain-c1", ConnectionState = "CONNECTED", CpuCores = 32, MemoryMib = 131072 },
                new HostSummary { Moref = "host-2", Name = "esxi-2", ClusterMoref = "domain-c1", ConnectionState = "CONNECTED", CpuCores = 32, MemoryMib = 131072 },
                new HostSummary { Moref = "host-3", Name = "esxi-3", ClusterMoref = "domain-c2", ConnectionState = "CONNECTED", CpuCores = 16, MemoryMib = 65536 },
            ]);
        client.ListVirtualMachinesAsync(Arg.Any<CancellationToken>())
            .Returns([
                new VirtualMachineSummary { Moref = "vm-1", Name = "vm1", PowerState = "POWERED_ON", CpuCount = 4, MemoryMib = 8192, HostMoref = "host-1" },
                new VirtualMachineSummary { Moref = "vm-2", Name = "vm2", PowerState = "POWERED_OFF", CpuCount = 2, MemoryMib = 4096, HostMoref = "host-1" },
                new VirtualMachineSummary { Moref = "vm-3", Name = "vm3", PowerState = "POWERED_ON", CpuCount = 8, MemoryMib = 16384, HostMoref = "host-2" },
                new VirtualMachineSummary { Moref = "vm-4", Name = "vm4", PowerState = "POWERED_ON", CpuCount = 2, MemoryMib = 2048, HostMoref = "host-3" },
            ]);

        await using var db = await VSphereTestDb.CreateAsync();
        var clock = new FakeClock(new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero));
        var sut = new VSphereInventoryRefresher(clock, client, db, NullLogger<VSphereInventoryRefresher>.Instance);

        var snapshotId = await sut.RefreshAsync("primary", CancellationToken.None);

        var header = await db.InventorySnapshots.FindAsync(snapshotId);
        header.ShouldNotBeNull();
        header.DatacenterCount.ShouldBe(1);
        header.ClusterCount.ShouldBe(2);
        header.HostCount.ShouldBe(3);
        header.VirtualMachineCount.ShouldBe(4);
        header.RefreshedAt.ShouldBe(clock.GetUtcNow());
        header.CreatedAt.ShouldBe(clock.GetUtcNow());
    }

    /// <summary>
    ///     Given a mocked client, when RefreshAsync runs, then the
    ///     per-cluster / per-host / per-VM child rows land in
    ///     their respective tables with the expected counts.
    /// </summary>
    [Fact(DisplayName = "Given mocked vCenter, when RefreshAsync runs, then writes per-resource child rows")]
    public async Task RefreshAsyncWritesPerResourceChildRowsAsync()
    {
        var client = Substitute.For<IVSphereClient>();
        client.ListDatacentersAsync(Arg.Any<CancellationToken>())
            .Returns([new DatacenterSummary { Moref = "datacenter-1", Name = "DC1" }]);
        client.ListClustersAsync(Arg.Any<CancellationToken>())
            .Returns([new ClusterSummary { Moref = "domain-c1", Name = "Cluster1", DatacenterMoref = "datacenter-1" }]);
        client.ListHostsAsync(Arg.Any<CancellationToken>())
            .Returns([new HostSummary { Moref = "host-1", Name = "esxi-1", ClusterMoref = "domain-c1", ConnectionState = "CONNECTED" }]);
        client.ListVirtualMachinesAsync(Arg.Any<CancellationToken>())
            .Returns([new VirtualMachineSummary { Moref = "vm-1", Name = "vm1", PowerState = "POWERED_ON" }]);

        await using var db = await VSphereTestDb.CreateAsync();
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var sut = new VSphereInventoryRefresher(clock, client, db, NullLogger<VSphereInventoryRefresher>.Instance);

        var snapshotId = await sut.RefreshAsync("primary", CancellationToken.None);

        (await db.Clusters.CountAsync()).ShouldBe(1);
        (await db.Hosts.CountAsync()).ShouldBe(1);
        (await db.VirtualMachines.CountAsync()).ShouldBe(1);

        var cluster = await db.Clusters.SingleAsync();
        cluster.SnapshotId.ShouldBe(snapshotId);
        cluster.Moref.ShouldBe("domain-c1");
    }

    /// <summary>
    ///     Given a prior snapshot row in the DB, when RefreshAsync
    ///     runs, then the prior snapshot row is replaced (atomic
    ///     refresh — no stale rows).
    /// </summary>
    [Fact(DisplayName = "Given a prior snapshot, when RefreshAsync runs, then replaces it atomically")]
    public async Task RefreshAsyncReplacesPriorSnapshotAtomicAsync()
    {
        var client = Substitute.For<IVSphereClient>();
        client.ListDatacentersAsync(Arg.Any<CancellationToken>())
            .Returns([new DatacenterSummary { Moref = "datacenter-1", Name = "DC1" }]);
        client.ListClustersAsync(Arg.Any<CancellationToken>())
            .Returns([]);
        client.ListHostsAsync(Arg.Any<CancellationToken>())
            .Returns([]);
        client.ListVirtualMachinesAsync(Arg.Any<CancellationToken>())
            .Returns([]);

        await using var db = await VSphereTestDb.CreateAsync();
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var sut = new VSphereInventoryRefresher(clock, client, db, NullLogger<VSphereInventoryRefresher>.Instance);

        var first = await sut.RefreshAsync("primary", CancellationToken.None);
        var second = await sut.RefreshAsync("primary", CancellationToken.None);

        first.ShouldNotBe(second);
        (await db.InventorySnapshots.CountAsync()).ShouldBe(1);
    }
}
