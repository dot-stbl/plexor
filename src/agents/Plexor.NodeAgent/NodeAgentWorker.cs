// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodeAgentWorker — BackgroundService that owns the Plexor.NodeAgent
// control loop:
//
//   1. Join   — POST /api/v1/nodes/register (via NodeJoiner), get
//                NodeId + ClusterId back.
//   2. Loop   — periodic heartbeat (every 30s) + long-poll (every 5s).
//   3. On poll — for each command envelope, dispatch via
//                CommandDispatcher, post the result back via
//                ICommandTransport.SubmitResultAsync.
//   4. Stop   — graceful on host shutdown (stoppingToken);
//                host already logged the join, so the host will
//                time us out at Offline after 3 missed heartbeats.
//
// Concurrency: the heartbeat and poll loops run as two
// independent Tasks. Each Task exits cleanly when stoppingToken
// trips; the worker waits for both to complete before returning
// from ExecuteAsync (per the BackgroundService contract).
//
// Refactor (Sep 2026, NodeAgent wire-format alignment):
//   - JoinOnceAsync / HeartbeatLoopAsync / PollLoopAsync /
//     DispatchOneAsync extracted to NodeJoiner, NodeHeartbeatLoop,
//     NodePollLoop. §1a forbids private orchestration methods on
//     production classes; this worker now contains only the
//     top-level orchestration.
//   - NodeHardware replaced by NodeHardwareSpec (the Outpost wire
//     shape); conversion lives in NodeHardwareSpecBuilder (shared
//     by NodeJoiner + NodeHeartbeatLoop).
//   - RegisterNodeRequest / NodeHeartbeatRequest now carry the
//     Outpost wire shape (Hostname / IpAddress / Role / Hardware /
//     IsoVersion / WireguardPublicKey on register; NodeId /
//     ClusterId / Hardware / IpAddress on heartbeat).
// ============================================================================

namespace Plexor.NodeAgent;

/// <summary>
///     BackgroundService that runs the join / heartbeat / poll /
///     dispatch / submit loop. The transport carries envelopes; the
///     dispatcher routes them to executors; the loop's only job is
///     "tick at the right interval and forward".
/// </summary>
/// <param name="joiner">Encapsulates a single registration attempt.</param>
/// <param name="heartbeat">Encapsulates the 30 s heartbeat loop.</param>
/// <param name="poll">Encapsulates the long-poll + dispatch loop.</param>
/// <param name="state">Shared state (current identity); written by
/// <see cref="NodeJoiner" />, read by both loops.</param>
/// <param name="logger">Structured logger.</param>
/// <remarks>
///     Build the worker. Hardware and control-plane URL
///     come from configuration (Plexor:Node:* keys).
/// </remarks>
internal sealed class NodeAgentWorker(
    NodeJoiner joiner,
    NodeHeartbeatLoop heartbeat,
    NodePollLoop poll,
    NodeAgentState state,
    ILogger<NodeAgentWorker> logger) : BackgroundService
{
    /// <summary>Backoff after a failed join attempt (network blip,
    /// host restarting, missing config).</summary>
    public static readonly TimeSpan JoinRetryInterval = TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "plexor-nodeagent starting (awaiting first join)");

        // Phase 1: join. Retry on failure with a fixed backoff; the
        // host's health monitor will see us as missing until we
        // join successfully, which is the right behavior.
        while (!stoppingToken.IsCancellationRequested && state.Current is null)
        {
            try
            {
                await joiner.JoinOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Join failed; retrying in {Backoff}", JoinRetryInterval);

                try
                {
                    await Task.Delay(JoinRetryInterval, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
            }
        }

        if (state.Current is null)
        {
            // Got cancelled during the join loop.
            return;
        }

        // Phase 2: heartbeat + poll loops run concurrently. Each
        // Task exits cleanly on stoppingToken; we wait for both to
        // finish before returning from ExecuteAsync.
        var heartbeatTask = Task.Run(() => heartbeat.RunAsync(stoppingToken), stoppingToken);
        var pollTask = Task.Run(() => poll.RunAsync(stoppingToken), stoppingToken);

        try
        {
            await Task.WhenAll(heartbeatTask, pollTask);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // expected on shutdown
        }

        logger.LogInformation("plexor-nodeagent stopping");
    }
}