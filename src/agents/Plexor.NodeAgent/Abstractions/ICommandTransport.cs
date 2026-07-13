// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ICommandTransport — abstract transport for the Plexor.Host control
// loop. The agent uses this abstraction to:
//   - join the control plane once at startup
//   - send periodic heartbeats
//   - long-poll for new commands
//   - submit command results
//
// Implementations:
//   - HttpCommandTransport (Plexor.Shared.Http + IHttpClientFactory) —
//     the production v0.1 transport; the agent runs alongside the
//     control plane over HTTP.
//   - InProcessTransport (tests) — a no-op transport that resolves
//     directly against an INodeRegistry instance for end-to-end
//     tests without a real HTTP server.
// ============================================================================

using Plexor.Shared.NodeApi;

namespace Plexor.NodeAgent.Abstractions;

/// <summary>
///     HTTP client surface for the Plexor.NodeAgent control loop. One
///     instance per agent process; registered as a singleton in DI so the
///     underlying <see cref="HttpClient" /> is shared across calls (and
///     reconnects are bounded by the resilience pipeline).
/// </summary>
public interface ICommandTransport
{
    /// <summary>
    ///     POST /api/v1/nodes/join. Returns the canonical
    ///     <c>NodeId</c> and the control-plane URL the agent should
    ///     call back to (used for logging only — the actual endpoints
    ///     are pinned at registration time).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    public Task<JoinResponse> JoinAsync(JoinRequest request, CancellationToken cancellationToken);

    /// <summary>
    ///     POST /api/v1/nodes/{nodeId}/heartbeat. Idempotent
    ///     (the host silently ignores heartbeats for unknown nodes).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    public Task HeartbeatAsync(HeartbeatRequest request, CancellationToken cancellationToken);

    /// <summary>
    ///     POST /api/v1/nodes/{nodeId}/commands/poll. Returns
    ///     any pending commands newer than <c>request.WaitCursor</c>
    ///     plus the next cursor the agent should send on the next
    ///     poll.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    public Task<CommandPollResponse> PollAsync(CommandPollRequest request, CancellationToken cancellationToken);

    /// <summary>
    ///     POST /api/v1/nodes/{nodeId}/commands/{commandId}/result.
    ///     v0.1: the host logs the result; future iterations persist
    ///     it to the audit log.
    /// </summary>
    /// <param name="result"></param>
    /// <param name="cancellationToken"></param>
    public Task SubmitResultAsync(CommandResult result, CancellationToken cancellationToken);
}
