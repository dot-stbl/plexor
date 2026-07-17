// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodeCommand — durable record of a command the control plane
// wants a specific node to execute. Lives in forge.commands
// (one table per node; the per-node queue is the standard
// pattern for worker-side long-poll — the node polls for its
// own queue, not a global broadcast table).
//
// The agent's command-poll loop calls /nodes/{nodeId}/commands/poll
// to fetch pending entries, executes them via the registered
// ICommandExecutor, and posts back via /nodes/{nodeId}/commands/{cmdId}/result.
// Status moves Pending → Sent → Acked → Completed/Failed.
//
// v0.1: synchronous from the control-plane's perspective — the
// action endpoint enqueues, the agent polls within seconds, and
// the control plane returns once a result is recorded (long-poll
// or short-poll depending on the agent's interval). v0.2+ can
// switch to async-fire-and-forget + heartbeat-driven state.
// ==========================================================================

using Plexor.Shared.Identifiers;

namespace Plexor.Modules.Clusters.Domain.Entities;

/// <summary>
///     Lifecycle of a single <see cref="NodeCommand" /> entry.
/// </summary>
public enum NodeCommandStatus
{
    /// <summary>Enqueued by the control plane; the node hasn't pulled it yet.</summary>
    Pending = 0,

    /// <summary>The node has polled and received the command; result not yet posted.</summary>
    Sent = 1,

    /// <summary>The node has executed the command and posted a result.</summary>
    Acked = 2,

    /// <summary>The node reported a failure (see <c>ResultJson</c>).</summary>
    Failed = 3
}

/// <summary>
///     One command the control plane wants a node to execute.
///     Persisted in <c>forge.commands</c>; the agent's long-poll
///     reads Pending entries, the result is written back via
///     <see cref="ResultJson" />.
/// </summary>
/// <param name="Id">Surrogate id (UUIDv7). Used for idempotent retries from the agent side.</param>
/// <param name="NodeId">Target node.</param>
/// <param name="CommandId">
///     Wire-format command id (UUIDv7).
///     Stable across retries; the control plane uses it for
///     idempotency when the agent re-posts a result after a
///     network blip.
/// </param>
/// <param name="Type">
///     Wire command type — matches <see cref="Shared.NodeApi.CommandType" />
///     (e.g. <c>"workload.start"</c>, <c>"workload.stop"</c>).
/// </param>
/// <param name="PayloadJson">
///     Command-specific JSON body. The
///     agent's executor deserializes into its own strongly-typed
///     args record; the shared contract is opaque JSON.
/// </param>
public sealed record NodeCommand(
    Guid Id,
    NodeId NodeId,
    Guid CommandId,
    string Type,
    string PayloadJson)
{
    /// <summary>Lifecycle state.</summary>
    public NodeCommandStatus Status { get; set; }

    /// <summary>
    ///     Result the node posted back, or <c>null</c> until the
    ///     command reaches <see cref="NodeCommandStatus.Acked" /> or
    ///     <see cref="NodeCommandStatus.Failed" />. Shape depends on
    ///     <see cref="Type" />.
    /// </summary>
    public string? ResultJson { get; set; }

    /// <summary>When the control plane enqueued.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    ///     When the node posted the result. Null while still
    ///     Pending / Sent.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }
}
