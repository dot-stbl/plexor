// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodeHeartbeatLoop — periodic liveness ping. Extracted from
// NodeAgentWorker.HeartbeatLoopAsync (Sep 2026) per §9 (no private
// orchestration methods on BackgroundService classes).
//
// Waits for <see cref="NodeAgentState.Current" /> to be non-null
// (i.e. join has succeeded) then sends a NodeHeartbeatRequest every
// 30 s. Failures are logged and the next tick retries; the host
// flips the node to Offline after three missed heartbeats.
// ============================================================================

using Plexor.NodeAgent.Abstractions;
using Plexor.NodeAgent.Composition;
using Plexor.Shared.NodeApi;

namespace Plexor.NodeAgent;

/// <summary>
    ///     Background-style loop that sends a heartbeat every
    /// <see cref="Interval" />. Reads the current identity from
    /// <see cref="NodeAgentState.Current" />.
    /// </summary>
    /// <param name="transport">Refit-backed HTTP transport.</param>
    /// <param name="state">Shared mutable state; the loop reads
    /// <see cref="NodeAgentState.Current" /> on every tick.</param>
    /// <param name="config">Hardware (Vcpu / RamGb / DiskGb) echoed on
    /// every heartbeat.</param>
    /// <param name="logger">Structured logger.</param>
    internal sealed class NodeHeartbeatLoop(
        ICommandTransport transport,
        NodeAgentState state,
        NodeConfig config,
        ILogger<NodeHeartbeatLoop> logger)
    {
        /// <summary>Heartbeat interval (matches the host's
        /// <c>Offline</c>-after-3-misses contract).</summary>
        public static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     Run the heartbeat loop until <paramref name="stoppingToken" />
    ///     trips. The loop exits early (without throwing) if
    ///     <see cref="NodeAgentState.Current" /> is null — that should
    ///     only happen if the worker is shutting down before join.
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
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            if (state.Current is null)
            {
                return;
            }

            try
            {
                await transport.HeartbeatAsync(
                    new NodeHeartbeatRequest(
                        NodeId: state.Current.NodeId,
                        ClusterId: state.Current.ClusterId,
                        Hardware: NodeHardwareSpecBuilder.Build(
                            config.CpuCores,
                            config.RamBytes,
                            config.DiskBytes),
                        IpAddress: string.Empty),
                    stoppingToken);
                logger.LogDebug("Heartbeat sent for {NodeId}", state.Current.NodeId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Heartbeat failed for {NodeId}; will retry next tick",
                    state.Current.NodeId);
            }
        }
    }
}