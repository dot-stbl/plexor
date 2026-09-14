// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// CommandEnvelope — a command the agent should execute. Extracted
// from NodeContracts.cs (Sprint 3, item 5) per folder-organization.md §1.
// ============================================================================

namespace Plexor.Shared.NodeApi;

/// <summary>
///     A command envelope. <see cref="Type" /> selects the executor
///     on the agent side; <see cref="PayloadJson" /> is the raw JSON
///     body the executor deserializes into its own strongly-typed
///     args record. The agent doesn't need to know the full command
///     catalog — it just dispatches on <c>Type</c>.
/// </summary>
/// <param name="CommandId"></param>
/// <param name="NodeId"></param>
/// <param name="Type"></param>
/// <param name="PayloadJson"></param>
/// <param name="IssuedAt"></param>
public sealed record CommandEnvelope(
    Guid CommandId,
    Guid NodeId,
    string Type,
    string PayloadJson,
    DateTimeOffset IssuedAt);