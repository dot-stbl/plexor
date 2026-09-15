// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// CommandPollRequest — long-poll request body. Extracted from
// NodeContracts.cs (Sprint 3, item 5) per folder-organization.md §1.
// ============================================================================

namespace Plexor.Shared.NodeApi;

/// <summary>
///     Long-poll request body. Returns immediately if no commands
///     are queued; otherwise returns the next batch. The agent
///     issues another poll as soon as the response is received.
/// </summary>
/// <param name="NodeId"></param>
/// <param name="MaxBatch"></param>
/// <param name="WaitCursor"></param>
public sealed record CommandPollRequest(
    Guid NodeId,
    int MaxBatch,
    long? WaitCursor);
