// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodeAgentWorker — BackgroundService that owns the Plexor.NodeAgent
// control loop:
//
//   1. Join   — POST /api/v1/nodes/join, get NodeId back.
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
// State: the join happens once at startup. If the join fails
// (host unreachable, missing config, etc.) the worker logs and
// retries every 30s with a fixed backoff. We don't crash — the
// host's health monitor will surface the agent as missing on
// its own.
// ============================================================================

using System.Net;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Plexor.NodeAgent.Abstractions;
using Plexor.NodeAgent.Composition;
using Plexor.Shared.NodeApi;

namespace Plexor.NodeAgent;

/// <summary>
/// BackgroundService that runs the join / heartbeat / poll /
/// dispatch / submit loop. The transport carries envelopes; the
/// dispatcher routes them to executors; the loop's only job is
/// "tick at the right interval and forward".
/// </summary>
internal sealed class NodeAgentWorker : BackgroundService
{
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan JoinRetryInterval = TimeSpan.FromSeconds(30);

    private readonly ICommandTransport transport;
    private readonly CommandDispatcher dispatcher;
    private readonly ILogger<NodeAgentWorker> logger;
    private readonly NodeHardware hardware;
    private readonly Uri controlPlaneUrl;

    private volatile NodeIdentity? current;

    /// <summary>Build the worker. Hardware and control-plane URL
    /// come from configuration (Plexor:Node:* keys).</summary>
    public NodeAgentWorker(
        ICommandTransport transport,
        CommandDispatcher dispatcher,
        ILogger<NodeAgentWorker> logger,
        NodeConfig config)
    {
        this.transport = transport;
        this.dispatcher = dispatcher;
        this.logger = logger;
        this.hardware = new NodeHardware(
            CpuCores: config.CpuCores,
            RamBytes: config.RamBytes,
            DiskBytes: config.DiskBytes,
            Hostname: config.Hostname,
            KernelVersion: Environment.OSVersion.VersionString);
        this.controlPlaneUrl = new Uri(config.ControlPlaneUrl);
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "plexor-nodeagent starting (cp={ControlPlaneUrl}, hostname={Hostname})",
            controlPlaneUrl,
            hardware.Hostname);

        // Phase 1: join. Retry on failure with a fixed backoff; the
        // host's health monitor will see us as missing until we
        // join successfully, which is the right behavior.
        while (!stoppingToken.IsCancellationRequested && current is null)
        {
            try
            {
                await JoinOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Join failed; retrying in {Backoff}",
                    JoinRetryInterval);
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

        if (current is null)
        {
            // Got cancelled during the join loop.
            return;
        }

        // Phase 2: heartbeat + poll loops run concurrently. Each
        // Task exits cleanly on stoppingToken; we wait for both to
        // finish before returning from ExecuteAsync.
        var heartbeat = Task.Run(() => HeartbeatLoopAsync(stoppingToken), stoppingToken);
        var poll = Task.Run(() => PollLoopAsync(stoppingToken), stoppingToken);

        try
        {
            await Task.WhenAll(heartbeat, poll);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // expected on shutdown
        }

        logger.LogInformation("plexor-nodeagent stopping");
    }

    private async Task JoinOnceAsync(CancellationToken cancellationToken)
    {
        // v0.1: the token is a placeholder; the host stores it
        // but does not verify it. Real impl issues the token
        // out-of-band (installer / enrollment) and the host
        // looks it up. We send a non-empty value so the host's
        // structural validation (non-empty join_token) doesn't
        // reject the join.
        var request = new JoinRequest(
            JoinToken: "v0.1-unverified-token",
            Hardware: hardware);

        var response = await transport.JoinAsync(request, cancellationToken);
        current = new NodeIdentity(response.NodeId, 0);
        logger.LogInformation(
            "Joined as node {NodeId} (control plane {ControlPlaneUrl})",
            response.NodeId,
            response.ControlPlaneUrl);
    }

    private async Task HeartbeatLoopAsync(CancellationToken stoppingToken)
    {
        // Periodic liveness ping. Failures are logged and the
        // next tick retries; the host flips us to Offline after
        // three missed heartbeats.
        while (!stoppingToken.IsCancellationRequested)
        {
            if (current is null)
            {
                return;
            }

            try
            {
                await Task.Delay(HeartbeatInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                await transport.HeartbeatAsync(
                    new HeartbeatRequest(
                        NodeId: current.NodeId,
                        SentAt: DateTimeOffset.UtcNow,
                        Hardware: hardware,
                        RunningVmCount: 0),
                    stoppingToken);
                logger.LogDebug("Heartbeat sent for {NodeId}", current.NodeId);
            }
            catch (Exception ex)
            {
                // Transient transport failure (network blip,
                // host restarting). Log and try again next tick.
                logger.LogWarning(
                    ex,
                    "Heartbeat failed for {NodeId}; will retry next tick",
                    current.NodeId);
            }
        }
    }

    private async Task PollLoopAsync(CancellationToken stoppingToken)
    {
        // Long-poll for new commands. The poll returns the
        // new cursor; we save it for the next iteration.
        while (!stoppingToken.IsCancellationRequested)
        {
            if (current is null)
            {
                return;
            }

            try
            {
                var response = await transport.PollAsync(
                    new CommandPollRequest(
                        NodeId: current.NodeId,
                        MaxBatch: 16,
                        WaitCursor: current.Cursor),
                    stoppingToken);

                current = current with { Cursor = response.NextCursor };

                foreach (var envelope in response.Commands)
                {
                    await DispatchOneAsync(envelope, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Transient failure (host unreachable, 503,
                // circuit breaker open). Log and back off one
                // poll interval before trying again.
                logger.LogWarning(
                    ex, "Poll failed for {NodeId}; retrying in {Backoff}",
                    current.NodeId, PollInterval);
                try
                {
                    await Task.Delay(PollInterval, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
            }
        }
    }

    private async Task DispatchOneAsync(
        CommandEnvelope envelope, CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Dispatching command {CommandId} ({Type})",
            envelope.CommandId, envelope.Type);

        var result = await dispatcher.DispatchAsync(envelope, stoppingToken);

        try
        {
            await transport.SubmitResultAsync(result, stoppingToken);
            logger.LogInformation(
                "Submitted result for {CommandId} -> {Status}",
                result.CommandId, result.Status);
        }
        catch (Exception ex)
        {
            // Result-submission failure is more serious than a
            // poll failure: the host will eventually time out the
            // command. Log loudly so the operator sees it.
            logger.LogError(
                ex,
                "Failed to submit result for {CommandId} ({Status})",
                result.CommandId, result.Status);
        }
    }

    /// <summary>Mutable per-node state held in the worker.</summary>
    private sealed record NodeIdentity(Guid NodeId, long Cursor);

    /// <summary>Configuration surface for the worker. Bound from
    /// <c>Plexor:Node</c> in appsettings. v0.1 takes the worker
    /// directly; future moves to IOptions&lt;NodeConfig&gt;.</summary>
    public sealed record NodeConfig(
        int CpuCores,
        long RamBytes,
        long DiskBytes,
        string Hostname,
        string ControlPlaneUrl);
}