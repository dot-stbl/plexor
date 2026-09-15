// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// CommandResult — result envelope posted by the agent after it
// executes a command. Extracted from NodeContracts.cs (Sprint 3,
// item 5) per folder-organization.md §1.
// ============================================================================

namespace Plexor.Shared.NodeApi;

/// <summary>
///     Result envelope posted by the agent after it executes a
///     command. On failure, <c>ErrorMessage</c> carries a one-line
///     human-readable detail; the control plane may log it and
///     surface it in the UI.
/// </summary>
/// <param name="CommandId"></param>
/// <param name="NodeId"></param>
/// <param name="Status"></param>
/// <param name="ErrorMessage"></param>
/// <param name="LocalId">
///     Provider-assigned runtime id (libvirt domain UUID, container
///     id, etc.). Populated by the agent for <c>workload.create</c>
///     results so the control plane can write it into
///     <c>forge.workloads.local_id</c>; null for commands that
///     don't return a runtime handle (start / stop / delete ack,
///     heartbeat).
/// </param>
/// <param name="CompletedAt"></param>
public sealed record CommandResult(
    Guid CommandId,
    Guid NodeId,
    CommandResultStatus Status,
    string? ErrorMessage,
    Guid? LocalId,
    DateTimeOffset CompletedAt);
