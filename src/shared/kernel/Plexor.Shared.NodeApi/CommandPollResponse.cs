// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// CommandPollResponse — reply to a long-poll. Extracted from
// NodeContracts.cs (Sprint 3, item 5) per folder-organization.md §1.
// ============================================================================

namespace Plexor.Shared.NodeApi;

/// <summary>
///     Reply to a long-poll. <see cref="NextCursor" /> is the cursor
///     the agent sends on the next poll so it sees only newer
///     commands.
/// </summary>
/// <param name="Commands"></param>
/// <param name="NextCursor"></param>
public sealed record CommandPollResponse(
    IReadOnlyList<CommandEnvelope> Commands,
    long NextCursor);