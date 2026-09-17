// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// RegisterNodeRequest — wire shape for POST /api/v1/nodes/register.
//
// Plexor.NodeAgent's first call. Anonymous at the HTTP layer — the
// join token in the body is the credential. The handler redeems the
// token, validates it, creates the outpost.node_records row, and
// returns a node-bearer token + cluster endpoint. Moved here from
// Plexor.Modules.Outpost.Api.Models in the NodeAgent wire-format
// alignment (Sep 2026) so the agent doesn't have to reference
// host-side types.
// ============================================================================

using Plexor.Shared.Identifiers;

namespace Plexor.Shared.NodeApi;

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