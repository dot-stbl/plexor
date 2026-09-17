// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// HostRegisterNodeRequest — wire shape for `POST /api/v1/nodes/register`.
// The CLI fills a minimal payload (token + hostname + IP + role +
// zero hardware spec) and lets the host's normal flow mint a node
// row + node-bearer token. We don't probe hardware from the CLI —
// the operator running `plx host nodes add` is filling out a join
// by hand from the host side (e.g. for a node whose NodeAgent
// can't auto-register).
// ============================================================================

namespace Plexor.Installer.Cli.Refit;

/// <summary>
///     Wire shape for <c>POST /api/v1/nodes/register</c>. The CLI
///     uses this to pre-create a node row from the operator's host
///     session — distinct from the NodeAgent's normal join path.
/// </summary>
/// <param name="JoinToken">Opaque JWT-format token from <c>RotateJoinToken</c>.</param>
/// <param name="Hostname">OS-reported hostname (operator-verifiable).</param>
/// <param name="IpAddress">Address the agent will be reached at.</param>
/// <param name="Role">Requested role (Control or Compute).</param>
/// <param name="Hardware">Hardware snapshot (zero-filled when unknown).</param>
/// <param name="IsoVersion">ISO image version the agent booted from.</param>
/// <param name="WireguardPublicKey">WireGuard public key (empty for non-mesh v0.1).</param>
public sealed record HostRegisterNodeRequest(
    string JoinToken,
    string Hostname,
    string IpAddress,
    HostNodeRole Role,
    HostNodeHardware Hardware,
    string IsoVersion,
    string WireguardPublicKey);

/// <summary>
///     Wire shape for the join response. The <c>NodeToken</c> is the
///     only thing the CLI surfaces — paste it into the NodeAgent's
///     install.sh so the agent can authenticate its heartbeat loop.
///     <c>ClusterEndpoint</c> is the post-join rendezvous (mTLS +
///     WireGuard).
/// </summary>
/// <param name="Node">The freshly-minted node row.</param>
/// <param name="NodeToken">Node-bearer token; sensitive; shown once.</param>
/// <param name="ClusterEndpoint">Post-join rendezvous point.</param>
public sealed record HostRegisterNodeResponse(
    HostNodeResponse Node,
    string NodeToken,
    string ClusterEndpoint);