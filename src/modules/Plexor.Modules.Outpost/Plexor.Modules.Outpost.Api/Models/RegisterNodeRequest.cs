// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// RegisterNodeRequest — wire shape for POST /api/v1/nodes/register.
//
// NodeAgent's first call. Anonymous at the HTTP layer — the join token
// in the body is the credential. The handler redeems the token,
// validates it, creates the outpost.node_records row, and returns a
// node-bearer token + cluster endpoint.
// ============================================================================

using Plexor.Modules.Clusters.Domain;
using Plexor.Modules.Outpost.Application;

namespace Plexor.Modules.Outpost.Api.Models;

/// <summary>Wire shape for <c>POST /api/v1/nodes/register</c>.</summary>
/// <param name="JoinToken">Opaque JWT-format token from <c>RotateJoinToken</c>.</param>
/// <param name="Hostname">OS-reported hostname (operator-verifiable).</param>
/// <param name="IpAddress">Address the agent wants to be reached at.</param>
/// <param name="Role">Requested role (must match token's intended role).</param>
/// <param name="Hardware">Hardware snapshot probed at boot.</param>
/// <param name="IsoVersion">ISO image version the agent booted from.</param>
/// <param name="WireguardPublicKey">WireGuard public key (empty for non-mesh v0.1).</param>
public sealed record RegisterNodeRequest(
    string JoinToken,
    string Hostname,
    string IpAddress,
    NodeRole Role,
    NodeHardwareSpec Hardware,
    string IsoVersion,
    string WireguardPublicKey);

/// <summary>
/// Wire shape for the hardware snapshot in node join / heartbeat
/// bodies. Distinct from the domain's <see cref="Plexor.Modules.Outpost.Application.NodeSpec" /> so
/// the wire shape can evolve independently (e.g. add a GPU-count
/// field before the domain does).
/// </summary>
/// <param name="Vcpu">Logical CPU count visible to the kernel.</param>
/// <param name="RamGb">Total RAM in gibibytes (rounded up).</param>
/// <param name="DiskGb">Total block storage in gibibytes reachable from the node.</param>
/// <param name="Providers">Install providers present on this node (kvm, lxc, pod, ovs, cilium, ...).</param>
public sealed record NodeHardwareSpec(
    int Vcpu,
    int RamGb,
    int DiskGb,
    IReadOnlyList<string> Providers);