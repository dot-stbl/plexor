// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodeEnvelopeDispatcher — extracts the per-envelope dispatch + result
// submission out of NodePollLoop so the loop body stays free of
// private methods (§1a). Constructor-injected dependencies make this
// trivial to mock in tests and to share with any future caller that
// needs to dispatch envelopes received out-of-band.
// ============================================================================

using Plexor.NodeAgent.Abstractions;
using Plexor.NodeAgent.Composition;
using Plexor.Shared.NodeApi;

namespace Plexor.NodeAgent;

/// <summary>
///     Runs one envelope through the registered executor and posts
///     the result back. Result-submission failure is logged as Error
///     (more serious than a poll failure — the host will eventually
///     time out the command) but doesn't abort the caller.
/// </summary>
/// <param name="dispatcher">Routes envelopes to executors.</param>
/// <param name="transport">Refit-backed HTTP transport.</param>
/// <param name="logger">Structured logger.</param>
internal sealed class NodeEnvelopeDispatcher(
    CommandDispatcher dispatcher,
    ICommandTransport transport,
    ILogger<NodeEnvelopeDispatcher> logger)
{
    /// <summary>
    ///     Dispatch one envelope via the registered executor and post
    ///     the result back.
    /// </summary>
    /// <param name="envelope">Command envelope received from the host.</param>
    /// <param name="stoppingToken">Worker shutdown token.</param>
    public async Task DispatchAsync(
        CommandEnvelope envelope,
        CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Dispatching command {CommandId} ({Type})",
            envelope.CommandId,
            envelope.Type);

        var result = await dispatcher.DispatchAsync(envelope, stoppingToken);

        try
        {
            await transport.SubmitResultAsync(result, stoppingToken);
            logger.LogInformation(
                "Submitted result for {CommandId} -> {Status}",
                result.CommandId,
                result.Status);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to submit result for {CommandId} ({Status})",
                result.CommandId,
                result.Status);
        }
    }
}