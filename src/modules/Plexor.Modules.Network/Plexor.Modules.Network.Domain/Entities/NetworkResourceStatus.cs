// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NetworkResourceStatus — lifecycle states for a Plexor network
// resource (floating IP or load balancer). The status transitions
// reflect the operational phases: a new resource is Pending; the
// runtime begins the attach dance (Attaching); on success the
// resource is Attached; the operator may detach (Detached); failures
// land in Error.
//
// The enum is stored as a varchar in the network.*.status column
// via HasConversion<string> so a future member addition does not
// require a schema migration.
// ============================================================================

namespace Plexor.Modules.Network.Domain.Entities;

/// <summary>
///     Lifecycle states for a <see cref="FloatingIp" /> or
///     <see cref="LoadBalancer" />. Transitions:
///     <c>Pending</c> → <c>Attaching</c> → <c>Attached</c> ↔
///     <c>Detached</c>; any state may transition to <c>Error</c>.
/// </summary>
public enum NetworkResourceStatus
{
    /// <summary>Resource row exists but no attach attempt has run yet.</summary>
    Pending = 0,

    /// <summary>The runtime is currently attaching the resource to its node.</summary>
    Attaching = 1,

    /// <summary>Resource is bound to the target node.</summary>
    Attached = 2,

    /// <summary>Resource exists but is detached from the node (kept for re-attach).</summary>
    Detached = 3,

    /// <summary>The last attach / detach attempt failed; row is retained for operator triage.</summary>
    Error = 4,
}
