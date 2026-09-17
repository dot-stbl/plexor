// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// HostNodeRole — wire-format role string from Plexor.Host
// `GET /api/v1/nodes/{id}`. Mirrors the controller's `NodeRole`
// enum (Control / Compute) but decoded as a string so the CLI stays
// agnostic of the host's domain assembly. The numeric wire form
// ("Control" / "Compute") is stable per the OpenAPI contract.
// ============================================================================

namespace Plexor.Installer.Cli.Refit;

/// <summary>
///     Wire string for the role a node fills in a cluster.
///     <c>"Control"</c> = Plexor.Host itself;
///     <c>"Compute"</c> = worker Plexor.NodeAgent.
/// </summary>
public enum HostNodeRole
{
    /// <summary>Plexor.Host control plane.</summary>
    Control = 0,

    /// <summary>Worker Plexor.NodeAgent.</summary>
    Compute = 1,
}

/// <summary>
///     Wire string for the lifecycle status of a node
///     (mirrors <c>Plexor.Modules.Outpost.Application.NodeStatus</c>).
///     Pending / Ready / Draining / Gone.
/// </summary>
public enum HostNodeStatus
{
    /// <summary>Has redeemed a join token but not yet heartbeat'd.</summary>
    Pending = 0,

    /// <summary>Heartbeating every 30 s; eligible for scheduling.</summary>
    Ready = 1,

    /// <summary>Being drained (workloads migrating off); still heartbeating.</summary>
    Draining = 2,

    /// <summary>Three consecutive missed heartbeats flipped it to gone.</summary>
    Gone = 3,
}

/// <summary>
///     Wire string for the derived health classification of a node
///     (Healthy / Stale / Unhealthy) — computed by the host from the
///     row's last-heartbeat timestamp + the configured threshold
///     windows. Distinct from <see cref="HostNodeStatus" /> which is
///     the persisted lifecycle field.
/// </summary>
public enum HostNodeHealth
{
    /// <summary>Heartbeat within the healthy window.</summary>
    Healthy = 0,

    /// <summary>Heartbeat within the stale window.</summary>
    Stale = 1,

    /// <summary>No heartbeat within the unhealthy window.</summary>
    Unhealthy = 2,
}