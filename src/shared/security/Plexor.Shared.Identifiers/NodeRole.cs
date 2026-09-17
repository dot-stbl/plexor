// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodeRole — wire-stable enum shared between Plexor.NodeAgent (join/heartbeat
// bodies) and Plexor.Modules.Outpost.Api (request validation). The Plexor.
// Modules.Clusters.Domain layer previously owned this; moved to the shared
// identifiers namespace in the NodeAgent wire-format alignment (Sep 2026) so
// the shared Plexor.Shared.NodeApi library can reference it without crossing
// into a module's domain.
// ============================================================================

namespace Plexor.Shared.Identifiers;

/// <summary>
///     Role a Plexor.NodeAgent fills when joining a cluster. The control
///     role is reserved for the Plexor.Host itself; workers run the
///     compute role. Per-cluster role pinning is done at
///     <c>JoinToken.IntendedRole</c>.
/// </summary>
public enum NodeRole
{
    /// <summary>
    ///     The Plexor.Host control plane. Only one node per cluster can
    ///     redeem a control-plane join token.
    /// </summary>
    Control = 0,

    /// <summary>
    ///     Worker node — runs Plexor.NodeAgent + user workloads.
    ///     Multiple compute nodes per cluster are expected.
    /// </summary>
    Compute = 1,
}