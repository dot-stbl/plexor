// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// HostWorkloadContracts — wire-shape DTOs for `plx vm *` commands.
// Mirrors the host's Plexor.Modules.Clusters.Api + Application types
// (`WorkloadSummary`, `CreateWorkloadRequest`, `WorkloadActionResult`,
// the paged list envelope). Declared locally in the CLI so the
// installer never depends on the host's Clusters assemblies — same
// pattern as HostNodeResponse.cs / HostClusterContracts.cs.
//
// AOT: Refit 12.x + System.Text.Json source-gen handles record types
// with init-only string/DateTimeOffset/nullable primitives without
// reflection. No [JsonPropertyName] overrides — the host's JSON keys
// match C# PascalCase property names via the framework's default
// case-insensitive matching.
//
// `SpecJson` (the body of CreateWorkloadRequest) is opaque to the
// control plane — it's forwarded verbatim to the assigned
// NodeAgent's runtime provider, which interprets it per kind
// (vm / lxc / k8s.pod / container). The CLI emits PascalCase JSON
// matching the LibvirtKvmConfig record that LibvirtKvmProvider
// deserialises on the agent side.
//
// ClusterId / WorkloadId / NodeId wire formats (`cluster_<uuidv7>`,
// `wl_<uuidv7>`, `node_<uuidv7>`) are passed as plain strings to keep
// this file free of a dependency on Plexor.Shared.Identifiers.
// ============================================================================

using System.Text.Json.Serialization;

namespace Plexor.Installer.Cli.Refit;

/// <summary>Wire shape for <c>GET /api/v1/compute/clusters/{id}/workloads</c>
/// entries and the response of <c>POST .../workloads</c>. Mirrors
/// <c>Plexor.Modules.Clusters.Application.Clusters.WorkloadSummary</c>.</summary>
/// <param name="Id">Workload id (wire string, e.g. <c>wl_&lt;UUIDv7&gt;</c>).</param>
/// <param name="ClusterId">Parent cluster id.</param>
/// <param name="AssignedNodeId">Assigned node id, or null while pending placement.</param>
/// <param name="LocalId">Per-runtime handle (libvirt UUID, container id). Null until the agent reports back.</param>
/// <param name="Name">Operator-facing name.</param>
/// <param name="Kind">Runtime identifier — <c>vm</c> / <c>lxc</c> / <c>k8s.pod</c> / <c>container</c>.</param>
/// <param name="State">Lifecycle state reported by the agent.</param>
/// <param name="LastReportedAt">Last keepalive timestamp (UTC), null if never.</param>
/// <param name="CreatedAt">Workload creation time (UTC).</param>
/// <param name="UpdatedAt">Last modification time (UTC).</param>
public sealed record HostWorkloadSummary(
    string Id,
    string ClusterId,
    string? AssignedNodeId,
    string? LocalId,
    string Name,
    string Kind,
    HostWorkloadState State,
    DateTimeOffset? LastReportedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
///     Lifecycle state reported by the agent. Mirrors
///     <c>Plexor.Shared.Workloads.WorkloadState</c>. The wire is
///     integer-valued (host-stable); the CLI renders it via the
///     status-color helpers in <c>VmStatusRenderer</c>.
/// </summary>
public enum HostWorkloadState
{
    /// <summary>Provider is creating the workload (image download, etc.).</summary>
    Provisioning = 0,

    /// <summary>Workload is booted and accepting traffic.</summary>
    Running = 1,

    /// <summary>Workload is gracefully shut down (resources stay allocated).</summary>
    Stopped = 2,

    /// <summary>Last lifecycle operation failed.</summary>
    Failed = 3,

    /// <summary>Provider can't determine the state.</summary>
    Unknown = 4
}

/// <summary>Wire shape for <c>POST /api/v1/compute/clusters/{id}/workloads</c>.
/// Mirrors <c>Plexor.Modules.Clusters.Api.Models.CreateWorkloadRequest</c>.</summary>
/// <param name="Name">Workload name (unique per cluster).</param>
/// <param name="Kind">Runtime identifier — <c>vm</c> for VMs, <c>lxc</c> for LXC, etc.</param>
/// <param name="SpecJson">Provider-specific JSON (image, CPU, RAM, network). Opaque to the control plane; forwarded to the assigned NodeAgent's runtime provider verbatim.</param>
public sealed record HostCreateWorkloadRequest(
    string Name,
    string Kind,
    string SpecJson);

/// <summary>
///     Wire shape for <c>POST .../workloads/{id}/actions/start</c>
///     and <c>POST .../workloads/{id}/actions/stop</c>. Mirrors
///     <c>Plexor.Modules.Clusters.Application.Clusters.WorkloadActionResult</c>.
///     Carries the post-action state the agent reported back plus
///     the idempotency key the control plane uses to correlate the
///     long-poll result with the originally-enqueued command.
/// </summary>
/// <param name="CommandId">Wire-format command id (UUIDv7). Stable across retries.</param>
/// <param name="State">Workload state after the action executed.</param>
public sealed record HostWorkloadActionResult(
    Guid CommandId,
    HostWorkloadState State);

/// <summary>
///     Paged response envelope for <c>GET /api/v1/compute/clusters/{id}/workloads</c>.
///     Mirrors <c>Plexor.Shared.Contracts.Pagination.PageResult&lt;T&gt;</c>
///     but kept local so the installer doesn't depend on the
///     Pagination contract assembly (same pattern as
///     HostClusterPage in HostClusterContractsExtended.cs).
/// </summary>
/// <param name="Items">The page slice — never null, may be empty.</param>
/// <param name="Total">Total matching rows across all pages.</param>
/// <param name="Page">The page number returned (1-based).</param>
/// <param name="PageSize">The page size used.</param>
public sealed record HostWorkloadPage(
    [property: JsonPropertyName("items")] IReadOnlyList<HostWorkloadSummary> Items,
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("pageSize")] int PageSize);
