// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IVSphereClient — typed Refit surface for the vCenter REST API.
//
// The vSphere provider talks to vCenter directly from Plexor.Host
// (control-plane-side; no agent runs on ESXi). This interface is the
// only place that names the vCenter endpoints — the inventory mapper
// + provisioning service consume the typed methods here and never see
// the wire JSON shape.
//
// All endpoints under /api/vcenter/* (the vSphere 8 API). Auth is HTTP
// basic with the configured service account; the bearer/polly handler
// chain lives in VSphereClientInstaller.
//
// Cancellation is the last parameter on every method (VSTHRD +
// async-and-tasks.md §4). Endpoints return typed DTOs from
// VCentreInventoryModels — never raw HttpResponseMessage.
// ============================================================================

using Plexor.Providers.VSphere.Inventory;
using Plexor.Providers.VSphere.Provisioning;
using Refit;

namespace Plexor.Providers.VSphere;

/// <summary>
///     Refit-typed client for the vCenter REST API. Registered by
///     <see cref="Installers.VSphereClientInstaller" /> against the
///     <see cref="VSphereOptions.VCenterUrl" /> base URL.
/// </summary>
public interface IVSphereClient
{
    /// <summary>
    ///     List every datacenter visible to the configured
    ///     service account. Inventory iteration starts here — the
    ///     cluster / host / VM lists below filter by datacenter
    ///     mo-ref.
    /// </summary>
    /// <param name="cancellationToken"></param>
    [Get("/api/vcenter/datacenter")]
    public Task<IReadOnlyList<DatacenterSummary>> ListDatacentersAsync(
        CancellationToken cancellationToken);

    /// <summary>
    ///     List every cluster in the vCenter inventory. The
    ///     Plexor UI uses the result to pick a placement target
    ///     for new VMs.
    /// </summary>
    /// <param name="cancellationToken"></param>
    [Get("/api/vcenter/cluster")]
    public Task<IReadOnlyList<ClusterSummary>> ListClustersAsync(
        CancellationToken cancellationToken);

    /// <summary>
    ///     List every ESXi host visible to the configured service
    ///     account. Hosts are scoped by cluster in the inventory
    ///     mapper — the API surface returns the flat list and the
    ///     caller filters.
    /// </summary>
    /// <param name="cancellationToken"></param>
    [Get("/api/vcenter/host")]
    public Task<IReadOnlyList<HostSummary>> ListHostsAsync(
        CancellationToken cancellationToken);

    /// <summary>
    ///     List every VM (non-template) in the vCenter inventory.
    ///     The UI uses the result to show tenant-owned VMs + to
    ///     reject name collisions before provisioning.
    /// </summary>
    /// <param name="cancellationToken"></param>
    [Get("/api/vcenter/vm")]
    public Task<IReadOnlyList<VirtualMachineSummary>> ListVirtualMachinesAsync(
        CancellationToken cancellationToken);

    /// <summary>
    ///     List every VM template in the vCenter inventory.
    ///     Templates are the source side of every clone operation
    ///     the provisioning service performs.
    /// </summary>
    /// <param name="cancellationToken"></param>
    [Get("/api/vcenter/vm-template")]
    public Task<IReadOnlyList<TemplateSummary>> ListTemplatesAsync(
        CancellationToken cancellationToken);

    /// <summary>
    ///     List every inventory folder. Plexor maps tenant
    ///     Folders to <c>kind="VM"</c> inventory folders for
    ///     clone placement.
    /// </summary>
    /// <param name="cancellationToken"></param>
    [Get("/api/vcenter/folder")]
    public Task<IReadOnlyList<InventoryFolderSummary>> ListFoldersAsync(
        CancellationToken cancellationToken);

    /// <summary>
    ///     List every resource pool. Resource pools are a
    ///     finer-grained placement target than clusters; surfaced
    ///     in the inventory so future iterations can target
    ///     pools directly.
    /// </summary>
    /// <param name="cancellationToken"></param>
    [Get("/api/vcenter/resource-pool")]
    public Task<IReadOnlyList<ResourcePoolSummary>> ListResourcePoolsAsync(
        CancellationToken cancellationToken);

    /// <summary>
    ///     List every datastore. Surfaced for inventory
    ///     completeness only — the v1 provisioning flow does not
    ///     consume this list (vCenter picks the cluster default
    ///     datastore when no explicit placement is given).
    /// </summary>
    /// <param name="cancellationToken"></param>
    [Get("/api/vcenter/datastore")]
    public Task<IReadOnlyList<DatastoreSummary>> ListDatastoresAsync(
        CancellationToken cancellationToken);

    /// <summary>
    ///     Clone a VM template into a target VM folder and power
    ///     it on. The body shape follows the vCenter documented
    ///     <c>POST /api/vcenter/vm-template/library/item-id?</c> +
    ///     custom <c>/action/clone</c> surface — Plexor wraps the
    ///     simpler <c>POST /api/vcenter/vm?action=clone</c>
    ///     endpoint that accepts a template mo-ref directly.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <remarks>
    ///     <b>Synchronous wait.</b> The call returns once vCenter
    ///     reports <c>SUCCESS</c> for the clone task; the
    ///     returned mo-ref is the new VM. A long clone (multi-TiB
    ///     disks) blocks the request — a future iteration moves
    ///     this to a background job (Iteration 3 follow-up).
    /// </remarks>
    [Post("/api/vcenter/vm?action=clone")]
    public Task<VCentreTaskHandle> CloneTemplateAsync(
        [Body] VSphereCloneRequest request,
        CancellationToken cancellationToken);
}
