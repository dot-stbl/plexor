// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// HttpCommandTransport — ICommandTransport implementation backed by
// a Refit-typed HttpClient (INodeApi). The transport catches the
// Refit-thrown ApiException for non-2xx responses and rethrows as
// HttpRequestException so the worker loop can treat all failures
// the same way regardless of the underlying HTTP library.
//
// The resilience pipeline (retry + circuit breaker + per-attempt
// timeout) is wired on the Refit typed-client registration in
// Program.cs via AddStandardResilienceHandler. The agent does not
// add its own retry logic on top — transient failures are
// expected to recover within the configured retry budget.
// ============================================================================

using Plexor.NodeAgent.Abstractions;
using Plexor.Shared.NodeApi;
using Refit;

namespace Plexor.NodeAgent.Infrastructure;

/// <summary>
///     Refit-backed implementation of <see cref="ICommandTransport" />.
///     Calls the corresponding <see cref="INodeApi" /> method and
///     throws <see cref="HttpRequestException" /> on non-2xx so the
///     worker loop can treat all failures the same way.
/// </summary>
/// <param name="api"></param>
/// <param name="logger"></param>
/// <remarks>
///     Build a transport over the Refit-generated
///     <see cref="INodeApi" /> typed client.
/// </remarks>
internal sealed class HttpCommandTransport(INodeApi api, ILogger<HttpCommandTransport> logger) : ICommandTransport
{
    /// <inheritdoc />
    public Task<RegisterNodeResponse> JoinAsync(RegisterNodeRequest request, CancellationToken cancellationToken)
    {
        return ApiCallRunner.RunAsync(
            () => api.JoinAsync(request, cancellationToken),
            operation: "register",
            logger: logger);
    }

    /// <inheritdoc />
    public Task HeartbeatAsync(NodeHeartbeatRequest request, CancellationToken cancellationToken)
    {
        return ApiCallRunner.RunAsync<object?>(
            async () =>
            {
                await api.HeartbeatAsync(request, cancellationToken);
                return null;
            },
            operation: "heartbeat",
            logger: logger);
    }

    /// <inheritdoc />
    public Task<CommandPollResponse> PollAsync(
        CommandPollRequest request,
        CancellationToken cancellationToken)
    {
        return ApiCallRunner.RunAsync(
            () => api.PollAsync(request.NodeId, request, cancellationToken),
            operation: "poll",
            logger: logger);
    }

    /// <inheritdoc />
    public Task SubmitResultAsync(CommandResult result, CancellationToken cancellationToken)
    {
        return ApiCallRunner.RunAsync<object?>(
            async () =>
            {
                await api.SubmitResultAsync(result.NodeId, result.CommandId, result, cancellationToken);
                return null;
            },
            operation: "submit",
            logger: logger);
    }
}