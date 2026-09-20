// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VSphereInventoryEndpoint — GET /api/v1/vsphere/inventory — surfaces
// the cached vSphere inventory (datacenters / clusters / hosts /
// VMs) as a single read endpoint.
//
// The endpoint always reads from the latest snapshot; the refresh
// itself is exposed via POST /api/v1/vsphere/inventory/refresh so a
// caller can force a fresh pull without waiting for the background
// refresh tick. When no snapshot has ever been written, the
// endpoint returns 503 (the cache is empty) so the caller knows to
// trigger a refresh first.
// ============================================================================

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Plexor.Providers.VSphere.Infrastructure.Persistence;
using Plexor.Shared.Contracts.Routes;

namespace Plexor.Providers.VSphere.Api.Endpoints;

file static class VSphereInventoryRoute
{
    public const string Name = "vsphere-inventory-get";
    public const string Path = ApiRoutes.Base + "/vsphere/inventory";
}

/// <summary>
///     Minimal-API endpoint that surfaces the cached vSphere
///     inventory to admin + UI callers.
/// </summary>
public static class VSphereInventoryEndpoint
{

    /// <summary>Map the inventory read endpoint.</summary>
    /// <param name="app">The host's endpoint route builder.</param>
    /// <returns>The same <paramref name="app" />, for chaining.</returns>
    public static IEndpointRouteBuilder MapVSphereInventory(this IEndpointRouteBuilder app)
    {
        app.MapGet(VSphereInventoryRoute.Path, HandleAsync)
            .WithName(VSphereInventoryRoute.Name)
            .WithTags("vsphere");
        return app;
    }

    /// <summary>
    ///     Read the most recent snapshot and return its header +
    ///     the per-cluster / per-host / per-VM rows. 503 when no
    ///     snapshot has ever been written or when the vSphere
    ///     section isn't configured.
    /// </summary>
    internal static async Task<IResult> HandleAsync(
        VSphereDbContext db,
        IOptions<VSphereOptions> options,
        CancellationToken cancellationToken)
    {
        if (!options.Value.IsConfigured())
        {
            return TypedResults.Problem(
                detail: "Set PLX_PROVIDERS_VSPHERE_VCENTERURL + PLX_PROVIDERS_VSPHERE_USERNAME + PLX_PROVIDERS_VSPHERE_PASSWORD to enable the vSphere provider.",
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "vSphere is not configured");
        }

        var header = await db.InventorySnapshots
            .AsNoTracking()
            .OrderByDescending(static snapshot => snapshot.RefreshedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (header is null)
        {
            return TypedResults.Problem(
                detail: "POST /api/v1/vsphere/inventory/refresh to pull a snapshot.",
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "vSphere inventory is empty");
        }

        var clusters = await db.Clusters
            .AsNoTracking()
            .Where(row => row.SnapshotId == header.Id)
            .ToListAsync(cancellationToken);

        var hosts = await db.Hosts
            .AsNoTracking()
            .Where(row => row.SnapshotId == header.Id)
            .ToListAsync(cancellationToken);

        var vms = await db.VirtualMachines
            .AsNoTracking()
            .Where(row => row.SnapshotId == header.Id)
            .ToListAsync(cancellationToken);

        return Results.Ok(new
        {
            snapshot = new
            {
                id = header.Id,
                vcenter_moref = header.VCenterMoref,
                datacenter_count = header.DatacenterCount,
                cluster_count = header.ClusterCount,
                host_count = header.HostCount,
                virtual_machine_count = header.VirtualMachineCount,
                refreshed_at = header.RefreshedAt,
            },
            clusters = clusters.Select(static cluster => new
            {
                moref = cluster.Moref,
                name = cluster.Name,
                datacenter_moref = cluster.DatacenterMoref,
                drs_enabled = cluster.DrsEnabled,
            }),
            hosts = hosts.Select(static host => new
            {
                moref = host.Moref,
                name = host.Name,
                cluster_moref = host.ClusterMoref,
                connection_state = host.ConnectionState,
                cpu_cores = host.CpuCores,
                memory_mib = host.MemoryMib,
            }),
            virtual_machines = vms.Select(static vm => new
            {
                moref = vm.Moref,
                name = vm.Name,
                folder_path = vm.FolderPath,
                power_state = vm.PowerState,
                cpu_count = vm.CpuCount,
                memory_mib = vm.MemoryMib,
                host_moref = vm.HostMoref,
            }),
        });
    }
}
