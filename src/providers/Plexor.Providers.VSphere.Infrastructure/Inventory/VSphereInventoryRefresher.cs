// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VSphereInventoryRefresher — pulls the vCenter inventory through
// the Refit client and persists it into the outpost schema.
//
// One method, RefreshAsync — read every list endpoint in parallel
// (Refit dispatches them on the typed client; the underlying
// IHttpClientFactory issues concurrent connections), wrap into a
// single snapshot, and replace the prior snapshot rows in one
// transaction. The refresh is the source of truth for the API
// surface — when it hasn't run, the GET endpoints return 503 (the
// caller knows the cache is stale).
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Plexor.Providers.VSphere.Infrastructure.Persistence;

namespace Plexor.Providers.VSphere.Infrastructure.Inventory;

/// <summary>
///     Pulls a fresh inventory from vCenter and writes it to the
///     <c>outpost</c> schema. The handler is stateless — one call
///     per refresh — and the per-refresh snapshot id is the
///     discriminator callers query against.
/// </summary>
/// <param name="client"></param>
/// <param name="db"></param>
/// <param name="clock"></param>
/// <param name="logger"></param>
public sealed class VSphereInventoryRefresher(
    IVSphereClient client,
    VSphereDbContext db,
    TimeProvider clock,
    ILogger<VSphereInventoryRefresher> logger)
{
    /// <summary>
    ///     Read every list endpoint, persist the result as a new
    ///     snapshot (truncating prior snapshot rows in the same
    ///     transaction), and return the new snapshot id. Errors
    ///     from any list endpoint propagate as exceptions — the
    ///     caller decides whether to retry.
    /// </summary>
    /// <param name="vCenterMoref">vCenter identifier to record on
    /// the snapshot header. v1 uses <c>"primary"</c>; a future
    /// multi-vCenter deploy carries the actual origin.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The new snapshot id (UUID v7).</returns>
    public async Task<Guid> RefreshAsync(
        string vCenterMoref,
        CancellationToken cancellationToken)
    {
        var refreshStartedAt = clock.GetUtcNow();

        // Parallel fan-out — Refit + HttpClientFactory issue each
        // list endpoint concurrently; one slow endpoint doesn't
        // stall the others.
        var datacentersTask = client.ListDatacentersAsync(cancellationToken);
        var clustersTask = client.ListClustersAsync(cancellationToken);
        var hostsTask = client.ListHostsAsync(cancellationToken);
        var vmsTask = client.ListVirtualMachinesAsync(cancellationToken);

        await Task.WhenAll(datacentersTask, clustersTask, hostsTask, vmsTask);

        var datacenters = await datacentersTask;
        var clusters = await clustersTask;
        var hosts = await hostsTask;
        var vms = await vmsTask;

        var snapshotId = Guid.CreateVersion7();

        // Atomic replace — the header row + the three child tables
        // are written in one SaveChanges so a partial failure
        // leaves the DB in a clean state.
        //
        // We use a tracked load + RemoveRange rather than
        // ExecuteDeleteAsync so the refresher stays portable
        // across EF Core providers (the InMemory provider used
        // by unit tests does not support ExecuteDeleteAsync).
        // The Postgres production path handles the DELETE via
        // EF's change tracker; perf is irrelevant at the
        // single-snapshot-row scale.
        await using var tx = await db.Database.BeginTransactionAsync(
            cancellationToken);

        var existing = await db.InventorySnapshots.ToListAsync(cancellationToken);
        if (existing.Count > 0)
        {
            db.InventorySnapshots.RemoveRange(existing);
        }

        await db.InventorySnapshots.AddAsync(new VSphereInventorySnapshot
        {
            Id = snapshotId,
            VCenterMoref = vCenterMoref,
            DatacenterCount = datacenters.Count,
            ClusterCount = clusters.Count,
            HostCount = hosts.Count,
            VirtualMachineCount = vms.Count,
            RefreshedAt = refreshStartedAt,
            CreatedAt = refreshStartedAt,
        }, cancellationToken);

        await db.Clusters.AddRangeAsync(clusters.Select(cluster => new VSphereCluster
        {
            Id = Guid.CreateVersion7(),
            SnapshotId = snapshotId,
            Moref = cluster.Moref,
            Name = cluster.Name,
            DatacenterMoref = cluster.DatacenterMoref,
            DrsEnabled = cluster.DrsEnabled,
        }), cancellationToken);

        await db.Hosts.AddRangeAsync(hosts.Select(host => new VSphereHost
        {
            Id = Guid.CreateVersion7(),
            SnapshotId = snapshotId,
            Moref = host.Moref,
            Name = host.Name,
            ClusterMoref = host.ClusterMoref,
            ConnectionState = host.ConnectionState,
            CpuCores = host.CpuCores,
            MemoryMib = host.MemoryMib,
        }), cancellationToken);

        await db.VirtualMachines.AddRangeAsync(vms.Select(vm => new VSphereVirtualMachine
        {
            Id = Guid.CreateVersion7(),
            SnapshotId = snapshotId,
            Moref = vm.Moref,
            Name = vm.Name,
            FolderPath = vm.FolderPath,
            PowerState = vm.PowerState,
            CpuCount = vm.CpuCount,
            MemoryMib = vm.MemoryMib,
            HostMoref = vm.HostMoref,
        }), cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        logger.LogInformation(
            "vSphere inventory refresh completed {SnapshotId} with {DatacenterCount} datacenters, {ClusterCount} clusters, {HostCount} hosts, {VirtualMachineCount} VMs",
            snapshotId,
            datacenters.Count,
            clusters.Count,
            hosts.Count,
            vms.Count);

        return snapshotId;
    }
}
