// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VCentreInventoryModels — wire DTOs returned by the vCenter REST API
// (the official /api/vcenter/* surface, not the legacy SOAP/REST
// inventory tree). The provider only consumes the fields it needs;
// extras are dropped at the DTO layer so adding fields never breaks the
// mapper (JsonExtensionData catches unknown members and they are
// silently ignored by the inventory mapper).
//
// Every external DTO is a sealed record with init-only properties
// (anti-patterns.md §2 — no positional records on wire shapes; Mapperly
// + the inventory mapper object-initialise these).
//
// The "Refit wire" shape matches vCenter's documented JSON: camelCase,
// managed-object reference (mo-ref) as a string, name + folder_path
// pairs for human-readable labels.
// ============================================================================

using System.Text.Json.Serialization;

namespace Plexor.Providers.VSphere.Inventory;

/// <summary>
///     Top-level vCenter inventory container — a datacenter holds
///     clusters, which hold hosts, which hold VMs.
/// </summary>
public sealed record DatacenterSummary
{
    /// <summary>vCenter managed-object reference
    /// (e.g. <c>"datacenter-1"</c>).</summary>
    public required string Moref { get; init; }

    /// <summary>Human-readable datacenter name.</summary>
    public required string Name { get; init; }

    /// <summary>Optional vCenter inventory folder path. Null when the
    /// datacenter lives at the root.</summary>
    public string? FolderPath { get; init; }
}

/// <summary>
///     Compute cluster (vCenter <c>ClusterComputeResource</c>) — a
///     group of ESXi hosts + their shared resource pool. Plexor
///     treats each cluster as a placement target (VMs land in the
///     cluster's default resource pool unless overridden).
/// </summary>
public sealed record ClusterSummary
{
    /// <summary>Cluster mo-ref (e.g. <c>"domain-c7"</c>).</summary>
    public required string Moref { get; init; }

    /// <summary>Cluster name.</summary>
    public required string Name { get; init; }

    /// <summary>Parent datacenter mo-ref.</summary>
    public required string DatacenterMoref { get; init; }

    /// <summary>vSphere API "drs_enabled" — when true, DRS handles
    /// initial placement + load balancing.</summary>
    public bool DrsEnabled { get; init; }
}

/// <summary>
///     ESXi host — a single hypervisor physical server. The Plexor
///     inventory surface exposes host-level CPU + memory totals so the
///     UI can render capacity per host (cluster totals derive from
///     the per-host rows).
/// </summary>
public sealed record HostSummary
{
    /// <summary>Host mo-ref (e.g. <c>"host-21"</c>).</summary>
    public required string Moref { get; init; }

    /// <summary>Host name.</summary>
    public required string Name { get; init; }

    /// <summary>Parent cluster mo-ref (hosts in vCenter live inside
    /// a cluster; standalone hosts still have a cluster wrapper).</summary>
    public required string ClusterMoref { get; init; }

    /// <summary>Connection state — <c>"CONNECTED"</c>,
    /// <c>"DISCONNECTED"</c>, <c>"NOT_RESPONDING"</c>.</summary>
    public required string ConnectionState { get; init; }

    /// <summary>Total logical CPU cores (sockets × cores).</summary>
    public int CpuCores { get; init; }

    /// <summary>Total physical memory in MiB.</summary>
    public long MemoryMib { get; init; }
}

/// <summary>
///     VirtualMachine inventory row — Plexor reads the
///     <c>/api/vcenter/vm</c> list and projects the fields the UI
///     needs (name + state + power state + hardware summary).
/// </summary>
public sealed record VirtualMachineSummary
{
    /// <summary>VM mo-ref (e.g. <c>"vm-1234"</c>).</summary>
    public required string Moref { get; init; }

    /// <summary>VM display name.</summary>
    public required string Name { get; init; }

    /// <summary>Inventory folder path (e.g. <c>"/Datacenter/vm/Templates"</c>).
    /// </summary>
    public string? FolderPath { get; init; }

    /// <summary>Power state — <c>"POWERED_ON"</c>,
    /// <c>"POWERED_OFF"</c>, <c>"SUSPENDED"</c>.</summary>
    public required string PowerState { get; init; }

    /// <summary>CPU count allocated to the VM.</summary>
    public int CpuCount { get; init; }

    /// <summary>Memory in MiB allocated to the VM.</summary>
    public long MemoryMib { get; init; }

    /// <summary>Parent host mo-ref. Null when the VM is a template
    /// (templates are not bound to a host).</summary>
    public string? HostMoref { get; init; }
}

/// <summary>
///     VM-template inventory row — same shape as a regular VM but
///     flagged via the <c>is_template</c> discriminator. The
///     provisioning endpoint accepts either a
///     <see cref="VirtualMachineSummary.Moref" /> of a template or
///     its inventory name.
/// </summary>
public sealed record TemplateSummary
{
    /// <summary>Template mo-ref.</summary>
    public required string Moref { get; init; }

    /// <summary>Template name (e.g. <c>"ubuntu-22.04-base"</c>).</summary>
    public required string Name { get; init; }

    /// <summary>Inventory folder path (templates usually live under
    /// a <c>/Templates/</c> folder).</summary>
    public string? FolderPath { get; init; }

    /// <summary>vCenter library name the template was imported from,
    /// when known. Null for ad-hoc / unmanaged templates.</summary>
    public string? LibraryName { get; init; }
}

/// <summary>
///     vCenter inventory folder — a logical grouping inside a
///     datacenter. Plexor maps each tenant Folder to one vCenter
///     inventory folder so resources land in the right group without
///     requiring the caller to know the underlying mo-ref.
/// </summary>
public sealed record InventoryFolderSummary
{
    /// <summary>Folder mo-ref.</summary>
    public required string Moref { get; init; }

    /// <summary>Folder name.</summary>
    public required string Name { get; init; }

    /// <summary>Folder kind — <c>"DATACENTER"</c>, <c>"DATA_CENTER"</c>,
    /// <c>"FOLDER"</c>, <c>"VM"</c>, <c>"HOST"</c>, <c>"STORAGE"</c>,
    /// <c>"NETWORK"</c>. Plexor uses <c>"VM"</c> for VM-folder
    /// targeting.</summary>
    public required string Kind { get; init; }

    /// <summary>Parent folder mo-ref, null when this is a root folder.</summary>
    public string? ParentMoref { get; init; }
}

/// <summary>
///     Resource pool — vCenter placement target finer-grained than
///     a cluster. Optional in the v1 provisioning flow (cluster is
///     the default); surfaced in the inventory so a future iteration
///     can target pools directly.
/// </summary>
public sealed record ResourcePoolSummary
{
    /// <summary>Resource pool mo-ref.</summary>
    public required string Moref { get; init; }

    /// <summary>Resource pool name.</summary>
    public required string Name { get; init; }

    /// <summary>Parent cluster mo-ref.</summary>
    public required string ClusterMoref { get; init; }
}

/// <summary>
///     Datastore summary — backing storage for VMs. Surfaced for
///     inventory completeness; not consumed by the v1 provisioning
///     flow (vCenter picks the datastore from the cluster default
///     unless the clone request specifies one).
/// </summary>
public sealed record DatastoreSummary
{
    /// <summary>Datastore mo-ref.</summary>
    public required string Moref { get; init; }

    /// <summary>Datastore name.</summary>
    public required string Name { get; init; }

    /// <summary>Type — <c>"VMFS"</c>, <c>"NFS"</c>, <c>"VSAN"</c>,
    /// <c>"VVOL"</c>, etc.</summary>
    public required string Type { get; init; }

    /// <summary>Total capacity in MiB.</summary>
    public long CapacityMib { get; init; }

    /// <summary>Free space in MiB.</summary>
    public long FreeMib { get; init; }
}

/// <summary>
///     Raw JSON object — vCenter returns some payloads as opaque
///     objects (e.g. the task handle for a long-running clone).
///     JsonExtensionData catches every field; the inventory mapper
///     reads only what it needs.
/// </summary>
public sealed record VCentreTaskHandle
{
    /// <summary>vCenter task mo-ref (e.g. <c>"task-9876"</c>). Used
    /// by follow-up <c>GET /api/vcenter/tasks/{task}</c> polls.</summary>
    public required string TaskMoref { get; init; }

    /// <summary>Returned VM mo-ref once the clone completes. Only
    /// populated on the post-clone polling result; absent on the
    /// initial POST response.</summary>
    public string? ResultVmMoref { get; init; }

    /// <summary>Task status string — <c>"QUEUED"</c>, <c>"RUNNING"</c>,
    /// <c>"SUCCESS"</c>, <c>"ERROR"</c>.</summary>
    public string? Status { get; init; }

    /// <summary>Extra fields from the upstream task JSON, preserved
    /// for diagnostic logs.</summary>
    [JsonExtensionData]
    public IDictionary<string, object?>? ExtraFields { get; init; }
}
