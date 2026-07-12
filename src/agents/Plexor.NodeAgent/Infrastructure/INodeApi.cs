// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// INodeApi — Refit interface for the Plexor.NodeAgent control loop.
// Each method maps 1:1 to an endpoint on Plexor.Host. Refit
// generates a typed HttpClient implementation at startup; the
// transport depends on the interface, not on the raw HttpClient,
// so unit tests can substitute an in-process implementation.
//
// Path conventions match the Plexor.Host controllers exactly:
//   POST nodes/join
//   POST nodes/{nodeId}/heartbeat
//   POST nodes/{nodeId}/commands/poll
//   POST nodes/{nodeId}/commands/{commandId}/result
//
// The Host's controllers live under Plexor.Shared.Contracts.Routes.
// ApiRoutes.Base (api/v1), which is folded into the typed client's
// BaseAddress at DI registration time.
//
// Refit 8+ behavior: methods returning Task<T> throw
// ApiException on non-2xx; the transport catches and rethrows as
// HttpRequestException for the worker loop.
// ============================================================================

using Plexor.Shared.NodeApi;
using Refit;

namespace Plexor.NodeAgent.Infrastructure;

/// <summary>
///     Refit-typed HTTP client for the Plexor.NodeAgent control loop.
///     Body / response bodies are the wire DTOs from
///     <see cref="Plexor.Shared.NodeApi" />; the BaseAddress on the
///     typed client already includes the <c>api/v1</c> segment.
/// </summary>
public interface INodeApi
{
    /// <summary>POST /api/v1/nodes/join.</summary>
    [Post("/nodes/join")]
    public Task<JoinResponse> JoinAsync(
        [Body] JoinRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    ///     POST /api/v1/nodes/{nodeId}/heartbeat. The
    ///     <c>nodeId</c> in the route must match the body's
    ///     <c>NodeId</c> (the host rejects mismatches with 400).
    ///     No response body.
    /// </summary>
    [Post("/nodes/{nodeId}/heartbeat")]
    public Task HeartbeatAsync(
        Guid nodeId,
        [Body] HeartbeatRequest request,
        CancellationToken cancellationToken);

    /// <summary>POST /api/v1/nodes/{nodeId}/commands/poll.</summary>
    [Post("/nodes/{nodeId}/commands/poll")]
    public Task<CommandPollResponse> PollAsync(
        Guid nodeId,
        [Body] CommandPollRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    ///     POST /api/v1/nodes/{nodeId}/commands/{commandId}/result.
    ///     No response body.
    /// </summary>
    [Post("/nodes/{nodeId}/commands/{commandId}/result")]
    public Task SubmitResultAsync(
        Guid nodeId,
        Guid commandId,
        [Body] CommandResult result,
        CancellationToken cancellationToken);
}
