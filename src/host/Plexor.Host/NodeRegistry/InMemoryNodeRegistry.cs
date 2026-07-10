// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// InMemoryNodeRegistry — v0.1 single-host implementation of
// INodeRegistry. State is process-local; restarting Plexor.Host
// forgets every node, its heartbeat history, and pending commands.
//
// Concurrency model:
//   - nodes   : ConcurrentDictionary<Guid, NodeRecord>.
//   - queues  : ConcurrentDictionary<Guid, ConcurrentQueue<CommandEnvelope>>.
//   - cursors : ConcurrentDictionary<Guid, long> monotonic per-node.
//
// Reads (poll, submit) can be lock-free via ConcurrentDictionary/Queue
// semantics. Writes (heartbeat) take a per-record lock so the
// hardware snapshot can't tear.
// ============================================================================

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Plexor.Host.Abstractions;
using Plexor.Shared.NodeApi;

namespace Plexor.Host.NodeRegistry;

/// <summary>
/// v0.1 in-memory implementation of <see cref="INodeRegistry"/>.
/// Single-process, no persistence, no auth. v0.2+ moves to
/// Postgres (node state, result audit log) and a durable queue
/// (NATS or Postgres LISTEN/NOTIFY) so commands survive restarts.
/// </summary>
internal sealed class InMemoryNodeRegistry : INodeRegistry
{
    private readonly ConcurrentDictionary<Guid, NodeRecord> nodes = new();
    private readonly ConcurrentDictionary<Guid, ConcurrentQueue<CommandEnvelope>> queues = new();
    private readonly ConcurrentDictionary<Guid, long> cursors = new();
    private readonly ILogger<InMemoryNodeRegistry> logger;
    private readonly Uri defaultControlPlaneUrl;

    /// <summary>Build an in-memory registry.</summary>
    /// <param name="logger">For join / heartbeat / result /
    /// queue-full diagnostics.</param>
    /// <param name="defaultControlPlaneUrl">URL returned in
    /// <see cref="JoinResponse.ControlPlaneUrl"/>. v0.1 is a
    /// placeholder — production wires this from
    /// <c>IConfiguration["Plexor:PublicUrl"]</c>.</param>
    public InMemoryNodeRegistry(
        ILogger<InMemoryNodeRegistry> logger,
        Uri? defaultControlPlaneUrl = null)
    {
        this.logger = logger;
        this.defaultControlPlaneUrl = defaultControlPlaneUrl
            ?? new Uri("http://localhost:5000/");
    }

    /// <inheritdoc />
    public Task<JoinResponse> RegisterAsync(JoinRequest request, CancellationToken ct)
    {
        // v0.1: every join gets a fresh node id. Real impl: verify
        // request.JoinToken against the pool issued by plexor init,
        // then either create a new record or update the existing one
        // (so the agent can recover its NodeId after a restart).
        var nodeId = Guid.NewGuid();
        var record = new NodeRecord
        {
            NodeId = nodeId,
            Hardware = request.Hardware,
            JoinedAt = DateTimeOffset.UtcNow,
            LastHeartbeatAt = DateTimeOffset.UtcNow,
            JoinTokenHash = request.JoinToken,
        };

        if (!nodes.TryAdd(nodeId, record))
        {
            throw new InvalidOperationException(
                $"InMemoryNodeRegistry: collision adding node {nodeId}.");
        }

        queues[nodeId] = new ConcurrentQueue<CommandEnvelope>();
        cursors[nodeId] = 0;

        logger.LogInformation(
            "Node {NodeId} joined from {Host}: {Cores} cores / {Ram} bytes RAM / {Disk} bytes disk",
            nodeId,
            request.Hardware.Hostname,
            request.Hardware.CpuCores,
            request.Hardware.RamBytes,
            request.Hardware.DiskBytes);

        return Task.FromResult(new JoinResponse(nodeId, defaultControlPlaneUrl));
    }

    /// <inheritdoc />
    public Task TouchHeartbeatAsync(HeartbeatRequest request, CancellationToken ct)
    {
        if (!nodes.TryGetValue(request.NodeId, out var record))
        {
            // Unknown node id is a no-op in v0.1 (the node may have
            // been forgotten across a restart). Real impl will
            // return NotFound so the agent knows to re-register.
            logger.LogWarning(
                "Heartbeat from unknown node {NodeId} ignored", request.NodeId);
            return Task.CompletedTask;
        }

        // lock(record) so a concurrent heartbeat doesn't tear the
        // Hardware read against the RunningVmCount update.
        lock (record)
        {
            record.LastHeartbeatAt = DateTimeOffset.UtcNow;
            record.Hardware = request.Hardware;
            record.RunningVmCount = request.RunningVmCount;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task EnqueueCommandAsync(CommandEnvelope envelope, CancellationToken ct)
    {
        if (!queues.TryGetValue(envelope.NodeId, out var queue))
        {
            // Unknown target node — drop + log. Real impl: route
            // to a "dead-letter" or surface the failure to the
            // caller (admin / scheduler).
            logger.LogWarning(
                "EnqueueCommand {CommandId} targets unknown node {NodeId}; dropping",
                envelope.CommandId,
                envelope.NodeId);
            return Task.CompletedTask;
        }

        queue.Enqueue(envelope);

        // Bump the per-node cursor so the next poll returns the
        // updated sequence number.
        cursors.AddOrUpdate(envelope.NodeId, 1L, (_, current) => current + 1);

        logger.LogInformation(
            "Enqueued command {CommandId} ({Type}) for node {NodeId}",
            envelope.CommandId,
            envelope.Type,
            envelope.NodeId);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<CommandPollResponse> DequeueCommandsAsync(
        CommandPollRequest request,
        CancellationToken ct)
    {
        // v0.1 simplification: drain everything queued for this
        // node regardless of the cursor. The returned cursor is
        // the total ever-enqueued count, which strictly increases
        // — the agent treats it as a session token.
        //
        // Real impl will compare WaitCursor against a per-envelope
        // sequence number so concurrent enqueues don't replay
        // commands the agent has already seen.
        var batch = new List<CommandEnvelope>(request.MaxBatch);
        var cursor = request.WaitCursor ?? 0;

        if (queues.TryGetValue(request.NodeId, out var queue))
        {
            while (batch.Count < request.MaxBatch && queue.TryDequeue(out var envelope))
            {
                batch.Add(envelope);
            }

            cursors.TryGetValue(request.NodeId, out cursor);
        }

        return Task.FromResult(new CommandPollResponse(batch, cursor));
    }

    /// <inheritdoc />
    public Task SubmitResultAsync(CommandResult result, CancellationToken ct)
    {
        // v0.1: log only. The audit module will pick this up
        // (Audit.Infrastructure subscribes to result events once
        // the queue layer lands).
        var status = result.Status == CommandResultStatus.Succeeded ? "ok" : "fail";
        logger.LogInformation(
            "Node {NodeId} command {CommandId} -> {Status}{Error}",
            result.NodeId,
            result.CommandId,
            status,
            string.IsNullOrEmpty(result.ErrorMessage)
                ? string.Empty
                : $": {result.ErrorMessage}");

        return Task.CompletedTask;
    }
}