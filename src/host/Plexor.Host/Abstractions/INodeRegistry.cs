// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// INodeRegistry — abstraction for the Plexor.Host side of the
// NodeAgent control loop. Each Plexor.NodeAgent on each compute
// node joins once via RegisterAsync, then sends periodic
// heartbeats, polls for commands targeted at itself, and posts
// results back. The registry holds:
//
//   - Per-node state (hardware, join time, last heartbeat).
//   - Per-node inbound command queue (outbound from Host's POV).
//
// v0.1: in-memory only (single-process, no persistence, no auth).
// Migration to Postgres + NATS is in ROADMAP Phase 4. The interface
// is shaped to match that future: every mutating method takes a
// CancellationToken and returns Task<...>, so swapping the
// implementation for a Postgres-backed one is a no-op at call sites.
// ============================================================================

using Plexor.Shared.NodeApi;

namespace Plexor.Host.Abstractions;

/// <summary>
///     Tracks compute nodes that have joined the control plane and the
///     commands waiting to be processed by each one. Methods are async
///     even when the in-memory implementation is sync — the future
///     Postgres/NATS-backed implementation will be IO-bound.
/// </summary>
public interface INodeRegistry
{
    /// <summary>
    ///     Register a new node with the control plane. Returns
    ///     the canonical <c>NodeId</c> and the control-plane URL the
    ///     agent should call back to.
    /// </summary>
    /// <param name="request">
    ///     The agent's join payload. Includes
    ///     the join token (not verified in v0.1) and its hardware
    ///     snapshot.
    /// </param>
    /// <param name="cancellationToken"></param>
    public Task<JoinResponse> RegisterAsync(JoinRequest request, CancellationToken cancellationToken);

    /// <summary>
    ///     Record liveness from a registered node. Idempotent;
    ///     calls for unknown node ids are a no-op (the node may have
    ///     been forgotten or the call may be a replay).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    public Task TouchHeartbeatAsync(HeartbeatRequest request, CancellationToken cancellationToken);

    /// <summary>
    ///     Enqueue a command targeted at a specific node. The
    ///     caller (admin / scheduler / API endpoint) provides the
    ///     envelope; the registry assigns it a monotonically-increasing
    ///     sequence number that becomes the agent's cursor for the
    ///     next poll.
    /// </summary>
    /// <param name="envelope"></param>
    /// <param name="cancellationToken"></param>
    public Task EnqueueCommandAsync(CommandEnvelope envelope, CancellationToken cancellationToken);

    /// <summary>
    ///     Dequeue any commands newer than the agent's
    ///     supplied cursor and return them along with the new cursor.
    ///     The agent uses the returned cursor on the next poll.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    public Task<CommandPollResponse> DequeueCommandsAsync(CommandPollRequest request, CancellationToken cancellationToken);

    /// <summary>
    ///     Record the outcome of a command the agent executed.
    ///     In v0.1 the result is logged; future iterations persist it
    ///     to the audit log.
    /// </summary>
    /// <param name="result"></param>
    /// <param name="cancellationToken"></param>
    public Task SubmitResultAsync(CommandResult result, CancellationToken cancellationToken);
}
