// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// WorkloadActionPayload — payload for workload.start / stop / delete
// commands. Extracted from NodeContracts.cs (Sprint 3, item 5) per
// folder-organization.md §1.
// ============================================================================

namespace Plexor.Shared.NodeApi;

/// <summary>
///     Payload for <c>workload.start</c>, <c>workload.stop</c>, and
///     <c>workload.delete</c> commands. The <c>LocalId</c> is the
///     runtime handle the provider assigned at create-time (libvirt
///     domain UUID, container id, k3s pod name) — populated from
///     <c>forge.workloads.local_id</c> which the agent first wrote
///     via the Tier-4 heartbeat reconciliation. Stable across
///     start/stop/delete on the same workload. Stored as a
///     <c>string</c> on the wire because provider-assigned ids are
///     not always valid Guids (e.g. <c>i-abc123</c> for AWS-style
///     providers or containername for docker).
/// </summary>
/// <param name="LocalId">Runtime handle — string shape to match <c>forge.workloads.local_id</c>.</param>
public sealed record WorkloadActionPayload(
    string LocalId);
