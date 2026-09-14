// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// HeartbeatRequest — periodic liveness signal. Extracted from
// NodeContracts.cs (Sprint 3, item 5) per folder-organization.md §1.
// ============================================================================

namespace Plexor.Shared.NodeApi;

/// <summary>
///     Periodic liveness signal. The interval is set by the agent
///     (30s for v0.1); the control plane flips the node to
///     <c>Offline</c> if it hasn't seen a heartbeat in 3 intervals.
///     The <see cref="Reports" /> list drives Phase D Tier 4 drift
///     detection — the control plane reconciles each report
///     against its durable <c>forge.workloads</c> view.
/// </summary>
/// <param name="NodeId"></param>
/// <param name="SentAt">
///     UTC time the heartbeat was sent (so the control plane can
///     spot clock-skew across many nodes).
/// </param>
/// <param name="Hardware"></param>
/// <param name="RunningVmCount">
///     Convenience aggregate — the number of workloads the agent
///     currently has in <c>Running</c> state. Equal to
///     <c>reports.Count(r =&gt; r.State == Running)</c>; included so
///     the control plane's per-node dashboard can show a number
///     without parsing the full report list.
/// </param>
/// <param name="Reports">
///     Per-workload state reports. Empty when the node hasn't
///     provisioned any workloads yet.
/// </param>
public sealed record HeartbeatRequest(
    Guid NodeId,
    DateTimeOffset SentAt,
    NodeHardware Hardware,
    int RunningVmCount,
    IReadOnlyList<WorkloadReport> Reports);