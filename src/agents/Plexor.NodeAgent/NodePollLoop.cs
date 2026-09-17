// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodePollLoop — long-poll for new commands + hand each envelope to
// the NodeEnvelopeDispatcher + update the cursor in
// NodeAgentState. Extracted from NodeAgentWorker.PollLoopAsync
// (Sep 2026) per §9 (no private orchestration methods on
// BackgroundService classes) and §1a (no private methods on
// production classes).
//
// The poll returns a new cursor; the loop updates
// <see cref="NodeAgentState.Current" /> on every successful response.
// ============================================================================

using Plexor.NodeAgent.Abstractions;
using Plexor.NodeAgent.Composition;
using Plexor.Shared.NodeApi;

namespace Plexor.NodeAgent;

/// <summary>
///     Long-poll loop: pull commands from the host, hand them to
///     <see cref="NodeEnvelopeDispatcher" />, update the cursor.
///     Reads + writes <see cref="NodeAgentState.Current" /> (cursor
///     field).
/// </summary>
/// <param name="transport">Refit-backed HTTP transport.</param>
/// <param name="state">Shared mutable state; this loop reads
/// <see cref="NodeAgentState.Current" /> and updates its
/// <see cref="NodeIdentity.Cursor" /> field on every successful
/// poll.</param>
/// <param name="envelopeDispatcher">Runs each envelope.</param>
/// <param name="logger">Structured logger.</param>
internal sealed class NodePollLoop(
    ICommandTransport transport,
    NodeAgentState state,
    NodeEnvelopeDispatcher envelopeDispatcher,
    ILogger<NodePollLoop> logger)
{
    /// <summary>How many commands to ask for per poll request.</summary>
    public const int MaxBatchSize = 16;

    /// <summary>Backoff after a transient poll failure (network blip,
    /// host restarting, circuit breaker open).</summary>
    public static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(5);

    /// <summary>
    ///     Run the poll loop until <paramref name="stoppingToken" />
    ///     trips. Exits early (without throwing) if
    ///     <see cref="NodeAgentState.Current" /> is null — should
    ///     only happen on shutdown before join.
    /// </summary>
    /// <param name="stoppingToken">Worker shutdown token.</param>
    public async Task RunAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (state.Current is null)
            {
                return;
            }

            try
            {
                var response = await transport.PollAsync(
                    new CommandPollRequest(
                        NodeId: WireNodeIdParser.ParseNodeId(state.Current.NodeId),
                        MaxBatch: MaxBatchSize,
                        WaitCursor: state.Current.Cursor),
                    stoppingToken);

                state.Current = state.Current with { Cursor = response.NextCursor };

                foreach (var envelope in response.Commands)
                {
                    await envelopeDispatcher.DispatchAsync(envelope, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                if (state.Current is null)
                {
                    return;
                }

                logger.LogWarning(
                    ex,
                    "Poll failed for {NodeId}; retrying in {Backoff}",
                    state.Current.NodeId,
                    RetryInterval);

                try
                {
                    await Task.Delay(RetryInterval, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
            }
        }
    }
}