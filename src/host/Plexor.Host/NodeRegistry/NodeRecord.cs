// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodeRecord — in-memory mutable state for one registered node.
// Heartbeat updates mutate Hardware / LastHeartbeatAt /
// RunningVmCount / NodeName. Per-update synchronization uses
// lock(this); fine for in-memory v0.1; production store would
// swap to row-versioned Postgres.
//
// Lives in the same folder as InMemoryNodeRegistry (internal
// detail of the registry implementation; not a public API).
// ============================================================================

using Plexor.Shared.NodeApi;

namespace Plexor.Host.NodeRegistry;

/// <summary>
///     Per-node mutable state held by the in-memory registry. Heartbeats
///     refresh the hardware snapshot and the running-VM count; the join
///     timestamp is set once at registration and never changes.
/// </summary>
internal sealed class NodeRecord
{
    /// <summary>Stable node id assigned at registration.</summary>
    public required Guid NodeId { get; init; }

    /// <summary>
    ///     Hardware snapshot at the most recent join or
    ///     heartbeat.
    /// </summary>
    public required NodeHardware Hardware { get; set; }

    /// <summary>UTC time the node first joined.</summary>
    public required DateTimeOffset JoinedAt { get; init; }

    /// <summary>
    ///     UTC time of the most recent heartbeat. Used by
    ///     the future health monitor to flip nodes to
    ///     <c>Offline</c>.
    /// </summary>
    public DateTimeOffset LastHeartbeatAt { get; set; }

    /// <summary>
    ///     Count of workloads the node reports running.
    ///     Used by the future scheduler as a load input.
    /// </summary>
    public int RunningVmCount { get; set; }

    /// <summary>
    ///     Join-token hash. v0.1 stores the token as-is (no
    ///     hashing). Real impl uses SHA-256.
    /// </summary>
    public required string JoinTokenHash { get; init; }

    /// <summary>
    ///     Optional friendly name (set via heartbeat, not
    ///     registration). Future impl: stable across restarts.
    /// </summary>
    public string? NodeName { get; set; }
}
