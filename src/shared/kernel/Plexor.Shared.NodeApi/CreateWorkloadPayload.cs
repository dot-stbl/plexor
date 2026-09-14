// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// CreateWorkloadPayload — payload for a workload.create command.
// Extracted from NodeContracts.cs (Sprint 3, item 5) per
// folder-organization.md §1.
// ============================================================================

namespace Plexor.Shared.NodeApi;

/// <summary>
///     Payload for a <c>workload.create</c> command. Deserialized
///     from <see cref="CommandEnvelope.PayloadJson" /> when the agent
///     dispatches a workload-create envelope. Carries the kind,
///     name, and provider-specific config that the local provider
///     will translate into its native representation (libvirt XML,
///     k3s Pod spec, etc.).
/// </summary>
/// <param name="Spec"></param>
public sealed record CreateWorkloadPayload(
    WorkloadSpec Spec);