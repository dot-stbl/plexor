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

using Microsoft.Extensions.Logging;
using Plexor.NodeAgent.Abstractions;
using Plexor.Shared.NodeApi;
using Refit;

namespace Plexor.NodeAgent.Infrastructure;

/// <summary>
/// Refit-backed implementation of <see cref="ICommandTransport"/>.
/// Calls the corresponding <see cref="INodeApi"/> method and
/// throws <see cref="HttpRequestException"/> on non-2xx so the
/// worker loop can treat all failures the same way.
/// </summary>
internal sealed class HttpCommandTransport : ICommandTransport
{
    private readonly INodeApi api;
    private readonly ILogger<HttpCommandTransport> logger;

    /// <summary>Build a transport over the Refit-generated
    /// <see cref="INodeApi"/> typed client.</summary>
    public HttpCommandTransport(INodeApi api, ILogger<HttpCommandTransport> logger)
    {
        this.api = api;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task<JoinResponse> JoinAsync(JoinRequest request, CancellationToken ct)
    {
        return await CallAsync(
            () => api.JoinAsync(request, ct),
            "join",
            ct);
    }

    /// <inheritdoc />
    public async Task HeartbeatAsync(HeartbeatRequest request, CancellationToken ct)
    {
        await CallAsync<object?>(
            async () =>
            {
                await api.HeartbeatAsync(request.NodeId, request, ct);
                return null;
            },
            "heartbeat",
            ct);
    }

    /// <inheritdoc />
    public async Task<CommandPollResponse> PollAsync(
        CommandPollRequest request, CancellationToken ct)
    {
        return await CallAsync(
            () => api.PollAsync(request.NodeId, request, ct),
            "poll",
            ct);
    }

    /// <inheritdoc />
    public async Task SubmitResultAsync(CommandResult result, CancellationToken ct)
    {
        await CallAsync<object?>(
            async () =>
            {
                await api.SubmitResultAsync(result.NodeId, result.CommandId, result, ct);
                return null;
            },
            "submit",
            ct);
    }

    /// <summary>Run a Refit call and translate
    /// <see cref="ApiException"/> into the transport's
    /// <see cref="HttpRequestException"/> contract. The worker
    /// loop catches the latter and decides what to do (skip the
    /// cycle, restart, etc.).</summary>
    private async Task<T> CallAsync<T>(
        Func<Task<T>> call, string operation, CancellationToken ct)
    {
        try
        {
            return await call();
        }
        catch (ApiException ex)
        {
            logger.LogWarning(
                "Control plane {Operation} returned {Status} ({StatusCode}): {Content}",
                operation,
                (int)ex.StatusCode,
                ex.StatusCode,
                ex.HasContent ? "(body available — see Refit diagnostics)" : "(no body)");

            throw new HttpRequestException(
                $"Control plane {operation} returned " +
                $"{(int)ex.StatusCode} {ex.StatusCode}.",
                inner: ex,
                statusCode: ex.StatusCode);
        }
    }
}