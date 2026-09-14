// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// JoinRequest — first message the node sends. Extracted from
// NodeContracts.cs (Sprint 3, item 5) per folder-organization.md §1.
// ============================================================================

namespace Plexor.Shared.NodeApi;

/// <summary>
///     First message the node sends. The control plane issues a join
///     token out-of-band (UI or installer); the agent presents it
///     here. Returns the assigned <c>NodeId</c> and the canonical
///     control-plane URL the agent should use for subsequent
///     heartbeats and command polls. The URL may differ from what
///     the agent derived (e.g. behind a reverse proxy, a private
///     network, or a HA failover).
/// </summary>
/// <param name="JoinToken"></param>
/// <param name="Hardware"></param>
public sealed record JoinRequest(
    string JoinToken,
    NodeHardware Hardware);