// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Cluster request DTOs — wire shapes for the Clusters HTTP API.
// Separate from controllers per coding/anti-patterns.md §2.
// ============================================================================

using Plexor.Modules.Clusters.Domain;
using Plexor.Shared.NodeApi;

namespace Plexor.Modules.Clusters.Api.Models;

/// <summary>Wire shape for <c>POST /api/v1/compute/clusters</c>.</summary>
/// <param name="Name">Cluster name (unique per org, 1–128 chars).</param>
/// <param name="Region">Operator-assigned region (e.g. <c>eu-central-1</c>).</param>
/// <param name="InitialNodeRole">Role the first joining node will take.</param>
public sealed record CreateClusterRequest(
    string Name,
    string Region,
    NodeRole InitialNodeRole);

/// <summary>Wire shape for <c>PATCH /api/v1/compute/clusters/{id}</c>.</summary>
/// <param name="Name">New name (null = leave unchanged).</param>
/// <param name="Region">New region (null = leave unchanged).</param>
public sealed record UpdateClusterRequest(
    string? Name,
    string? Region);

/// <summary>
///     Wire shape for <c>POST /api/v1/compute/clusters/join</c> —
///     NodeAgent's first call. Anonymous at the HTTP layer; the join
///     token in the body is the credential.
/// </summary>
/// <param name="JoinToken">Opaque token from RotateJoinToken / CreateCluster.</param>
/// <param name="Hostname">OS-reported hostname (operator-verifiable).</param>
/// <param name="Role">Requested node role (must match token's intended role).</param>
/// <param name="Hardware">Hardware snapshot probed at boot.</param>
public sealed record NodeJoinRequest(
    string JoinToken,
    string Hostname,
    NodeRole Role,
    NodeHardware Hardware);

/// <summary>
///     Wire shape for <c>POST /api/v1/compute/clusters/{id}/heartbeat</c>.
/// </summary>
/// <param name="NodeId">Caller's own node id, in wire format
/// (<c>node_&lt;UUIDv7&gt;</c>). Parsed into a strongly-typed
/// <see cref="Plexor.Shared.Identifiers.NodeId" /> on the server
/// side via <see cref="Plexor.Shared.Identifiers.IdParse" />.</param>
/// <param name="Hardware">Fresh hardware snapshot.</param>
public sealed record NodeHeartbeatRequest(
    string NodeId,
    NodeHardware Hardware,
    IReadOnlyList<WorkloadReport> Reports);

/// <summary>
///     Wire shape for the hardware snapshot in node join / heartbeat
/// bodies. Distinct from the domain's <see cref="Domain.NodeSpec" />
/// so the wire shape can evolve independently (e.g. add a GPU-count
/// field before the domain does).
/// </summary>
/// <param name="Vcpu">Virtual CPU cores.</param>
/// <param name="RamGb">RAM in gigabytes.</param>
/// <param name="DiskGb">Disk in gigabytes.</param>
/// <param name="Providers">Install providers present on this node.</param>
public sealed record NodeHardware(
    int Vcpu,
    int RamGb,
    int DiskGb,
    IReadOnlyList<string> Providers);
