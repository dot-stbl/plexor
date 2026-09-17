// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// HostNodeResponse — wire shape for `GET /api/v1/nodes/{id}` and the
// entries of `GET /api/v1/nodes`. Mirrors the controller's
// `Plexor.Modules.Outpost.Api.Models.NodeResponse`. Declared locally
// in the CLI so the installer never depends on the host's
// Outpost.Api assembly (the CLI is a separate bounded context).
//
// AOT: Refit 12.x + System.Text.Json source-gen handles record
// types with init-only string/DateTimeOffset/nullable primitives
// without reflection. No [JsonPropertyName] overrides — the host's
// JSON keys (Id, ClusterId, Hostname, IpAddress, Role, Status,
// Hardware, IsoVersion, LastHeartbeatAt, CreatedAt, UpdatedAt) match
// C# PascalCase property names via the framework's default
// case-insensitive matching.
// ============================================================================

using System.Text.Json.Serialization;

namespace Plexor.Installer.Cli.Refit;

/// <summary>Wire shape for <c>GET /api/v1/nodes/{id}</c> + list entries.</summary>
/// <param name="Id">Node id (wire string, e.g. <c>node_&lt;UUIDv7&gt;</c>).</param>
/// <param name="ClusterId">Parent cluster id.</param>
/// <param name="OrgId">Tenant scope.</param>
/// <param name="Hostname">OS-reported hostname.</param>
/// <param name="IpAddress">Address the agent wants to be reached at.</param>
/// <param name="Role">Role within the cluster.</param>
/// <param name="Status">Lifecycle status.</param>
/// <param name="Hardware">Hardware snapshot.</param>
/// <param name="IsoVersion">ISO image version.</param>
/// <param name="LastHeartbeatAt">Last keepalive timestamp (UTC), null if never.</param>
/// <param name="CreatedAt">Node creation time (UTC).</param>
/// <param name="UpdatedAt">Last modification time (UTC).</param>
public sealed record HostNodeResponse(
    string Id,
    string ClusterId,
    Guid OrgId,
    string Hostname,
    string IpAddress,
    HostNodeRole Role,
    HostNodeStatus Status,
    HostNodeHardware Hardware,
    string IsoVersion,
    DateTimeOffset? LastHeartbeatAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
///     Wire shape for the hardware snapshot in node payloads.
///     Mirrors the host's <c>NodeHardwareSpec</c>.
/// </summary>
/// <param name="Vcpu">Logical CPU count.</param>
/// <param name="RamGb">Total RAM in GiB.</param>
/// <param name="DiskGb">Total block storage in GiB.</param>
/// <param name="Providers">Install providers present on this node.</param>
public sealed record HostNodeHardware(
    int Vcpu,
    int RamGb,
    int DiskGb,
    IReadOnlyList<string> Providers);

/// <summary>Wire shape for <c>GET /api/v1/nodes</c>.</summary>
/// <param name="Nodes">Flat list of node records; empty when no nodes match.</param>
public sealed record HostNodeListResponse(
    [property: JsonPropertyName("nodes")] IReadOnlyList<HostNodeResponse> Nodes);