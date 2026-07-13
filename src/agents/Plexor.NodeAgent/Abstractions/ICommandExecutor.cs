// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ICommandExecutor — one impl per (command.type, command.kind) pair.
// The agent's command dispatcher looks the right executor up
// based on the command's wire type string and routes the payload
// to it. Executors return a CommandResult that the agent's
// transport submits back to the host.
//
// Resolver is separate from the executors themselves so the
// dispatch table can be configured per-deployment (skip certain
// commands, alias old names, etc.) without touching the work.
// ============================================================================

using Plexor.Shared.NodeApi;

namespace Plexor.NodeAgent.Abstractions;

/// <summary>
///     Handles one kind of <see cref="CommandEnvelope" /> payload. The
///     dispatcher looks the right executor up at runtime by the
///     envelope's <c>Type</c> field ("workload.create", "workload.start",
///     etc.). Executors deserialize <c>PayloadJson</c> themselves; the
///     dispatcher does not know the payload shape.
/// </summary>
public interface ICommandExecutor
{
    /// <summary>
    ///     Wire command type this executor handles (e.g.
    ///     <c>"workload.create"</c>). Matched against
    ///     <see cref="CommandEnvelope.Type" />.
    /// </summary>
    public string Type { get; }

    /// <summary>
    ///     Run the command and return the result. The
    ///     dispatcher wraps the return value into a
    ///     <see cref="CommandResult" /> with the envelope's id and
    ///     node id.
    /// </summary>
    /// <param name="envelope">
    ///     Full envelope so the executor can
    ///     see the issued timestamp and the node id (used for
    ///     result metadata).
    /// </param>
    /// <param name="cancellationToken">
    ///     Cancellation tied to the agent's
    ///     shutdown token.
    /// </param>
    public Task<ExecutorResult> ExecuteAsync(CommandEnvelope envelope, CancellationToken cancellationToken);
}

/// <summary>
///     Outcome of a single command. The dispatcher promotes
///     this to a <see cref="CommandResult" /> by attaching the
///     envelope's <c>CommandId</c>, <c>NodeId</c>, and
///     <c>CompletedAt = UtcNow</c>.
/// </summary>
/// <param name="Status">Whether the work succeeded or failed.</param>
/// <param name="ErrorMessage">
///     Required when <paramref name="Status" />
///     is <see cref="CommandResultStatus.Failed" />; null otherwise.
/// </param>
public sealed record ExecutorResult(
    CommandResultStatus Status,
    string? ErrorMessage)
{
    /// <summary>Convenience factory for a successful execution.</summary>
    public static ExecutorResult Ok()
    {
        return new ExecutorResult(CommandResultStatus.Succeeded, null);
    }

    /// <summary>Convenience factory for a failed execution.</summary>
    /// <param name="errorMessage"></param>
    public static ExecutorResult Fail(string errorMessage)
    {
        return new ExecutorResult(CommandResultStatus.Failed, errorMessage);
    }
}
