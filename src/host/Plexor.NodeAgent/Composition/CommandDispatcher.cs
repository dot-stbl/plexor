// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// CommandDispatcher — ICommandExecutor lookup. Looks up an executor
// by command.type, deserializes the payload, runs the executor,
// wraps the result in a CommandResult envelope.
//
// Why a dedicated dispatcher:
//   - Decouples the loop from the dispatch table
//   - Lets the worker handle "no executor for this type" the
//     same way for every command type (mark as failed with a
//     reason)
//   - Future: per-deployment command policy (skip, alias, rate
//     limit) sits in the resolver, not in the loop
// ============================================================================

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Plexor.NodeAgent.Abstractions;
using Plexor.Shared.NodeApi;

namespace Plexor.NodeAgent.Composition;

/// <summary>
/// Routes a <see cref="CommandEnvelope"/> to its
/// <see cref="ICommandExecutor"/> by the wire type, executes it,
/// and wraps the outcome in a <see cref="CommandResult"/>. Singleton
/// in DI — the dispatch table is built once at startup.
/// </summary>
internal sealed class CommandDispatcher
{
    private readonly Dictionary<string, ICommandExecutor> executors;
    private readonly ILogger<CommandDispatcher> logger;

    /// <summary>Build a dispatcher from a set of registered
    /// executors. Duplicate <see cref="ICommandExecutor.Type"/>
    /// values throw at startup — the dispatch table must be a
    /// function from type to executor.</summary>
    public CommandDispatcher(
        IEnumerable<ICommandExecutor> executors,
        ILogger<CommandDispatcher> logger)
    {
        var byType = new Dictionary<string, ICommandExecutor>(StringComparer.Ordinal);
        foreach (var executor in executors)
        {
            if (!byType.TryAdd(executor.Type, executor))
            {
                throw new InvalidOperationException(
                    $"CommandDispatcher: duplicate executor for type " +
                    $"'{executor.Type}' (existing: {byType[executor.Type].GetType().Name}, " +
                    $"new: {executor.GetType().Name}).");
            }
        }

        this.executors = byType;
        this.logger = logger;
    }

    /// <summary>Execute one command envelope. Returns a
    /// <see cref="CommandResult"/> with the outcome — never
    /// throws; a missing executor or a payload parse error
    /// becomes a <see cref="CommandResultStatus.Failed"/>
    /// result so the host can see the failure cleanly.</summary>
    public async Task<CommandResult> DispatchAsync(
        CommandEnvelope envelope, CancellationToken ct)
    {
        if (!executors.TryGetValue(envelope.Type, out var executor))
        {
            logger.LogWarning(
                "No executor for command type '{Type}' (id {CommandId}); failing it",
                envelope.Type,
                envelope.CommandId);
            return Failed(envelope, $"no executor for type '{envelope.Type}'");
        }

        try
        {
            var result = await executor.ExecuteAsync(envelope, ct);
            return new CommandResult(
                envelope.CommandId,
                envelope.NodeId,
                result.Status,
                result.ErrorMessage,
                DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Executor {Executor} threw for command {CommandId} (type {Type})",
                executor.GetType().Name,
                envelope.CommandId,
                envelope.Type);
            return Failed(envelope, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>Construct a failed <see cref="CommandResult"/>
    /// for the given envelope.</summary>
    private static CommandResult Failed(CommandEnvelope envelope, string error)
    {
        return new CommandResult(
            envelope.CommandId,
            envelope.NodeId,
            CommandResultStatus.Failed,
            error,
            DateTimeOffset.UtcNow);
    }
}