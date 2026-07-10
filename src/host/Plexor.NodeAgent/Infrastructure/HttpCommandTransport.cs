// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// HttpCommandTransport — ICommandTransport implementation that talks
// to Plexor.Host over HTTP. Backed by IHttpClientFactory + the
// resilience pipeline (retry + circuit breaker + timeout) wired by
// AddStandardResilienceHandler at DI registration time.
//
// The transport is responsible only for the request/response shape
// and the resilience behavior. It does NOT own the join/heartbeat/
// poll/submit loop — that lives in NodeAgentWorker. This split lets
// the worker swap in a different transport (in-process for tests,
// gRPC when we add it) without rewriting the loop.
// ============================================================================

using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Plexor.NodeAgent.Abstractions;
using Plexor.Shared.NodeApi;

namespace Plexor.NodeAgent.Infrastructure;

/// <summary>
/// HTTP client for the Plexor.NodeAgent control loop. Uses a typed
/// <see cref="HttpClient"/> (named-client pattern) so the resilience
/// handler applies per-transport, not globally.
/// </summary>
internal sealed class HttpCommandTransport : ICommandTransport
{
    /// <summary>Name used for the typed <see cref="HttpClient"/>
    /// registration. Referenced from
    /// <c>PlexorNodeAgentServiceCollectionExtensions.AddHttpTransport</c>.</summary>
    public const string HttpClientName = "plexor-nodeagent";

    private readonly HttpClient http;
    private readonly ILogger<HttpCommandTransport> logger;

    /// <summary>Build a transport over the given <see cref="HttpClient"/>.</summary>
    public HttpCommandTransport(HttpClient http, ILogger<HttpCommandTransport> logger)
    {
        this.http = http;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task<JoinResponse> JoinAsync(JoinRequest request, CancellationToken ct)
    {
        // POST /api/v1/nodes/join
        using var response = await PostAsJsonAsync("nodes/join", request, ct);
        await EnsureSuccessAsync(response, "join", ct);
        return await ReadAsync<JoinResponse>(response, ct);
    }

    /// <inheritdoc />
    public async Task HeartbeatAsync(HeartbeatRequest request, CancellationToken ct)
    {
        // POST /api/v1/nodes/{nodeId}/heartbeat
        using var response = await PostAsJsonAsync(
            $"nodes/{request.NodeId}/heartbeat", request, ct);
        await EnsureSuccessAsync(response, "heartbeat", ct);
    }

    /// <inheritdoc />
    public async Task<CommandPollResponse> PollAsync(
        CommandPollRequest request, CancellationToken ct)
    {
        // POST /api/v1/nodes/{nodeId}/commands/poll
        using var response = await PostAsJsonAsync(
            $"nodes/{request.NodeId}/commands/poll", request, ct);
        await EnsureSuccessAsync(response, "poll", ct);
        return await ReadAsync<CommandPollResponse>(response, ct);
    }

    /// <inheritdoc />
    public async Task SubmitResultAsync(CommandResult result, CancellationToken ct)
    {
        // POST /api/v1/nodes/{nodeId}/commands/{commandId}/result
        using var response = await PostAsJsonAsync(
            $"nodes/{result.NodeId}/commands/{result.CommandId}/result",
            result,
            ct);
        await EnsureSuccessAsync(response, "submit", ct);
    }

    private Task<HttpResponseMessage> PostAsJsonAsync<T>(
        string relativePath, T payload, CancellationToken ct)
    {
        // BaseAddress is set by the typed-client registration in
        // AddHttpTransport. Path is relative to the base.
        return http.PostAsJsonAsync(relativePath, payload, ct);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        // v0.1: every endpoint returns a JSON body. ReadAsync
        // throws on non-2xx via EnsureSuccessAsync, so callers see
        // a clean value-or-throw contract.
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct)
            ?? throw new InvalidOperationException(
                $"Empty response body from {response.RequestMessage?.RequestUri}.");
    }

    /// <summary>
    /// Throw a <see cref="HttpRequestException"/> for non-2xx
    /// responses. The resilience pipeline (retry + circuit
    /// breaker) is the right place to handle transient failures;
    /// non-2xx after retries means the host rejected the request
    /// and the agent should log + skip.
    /// </summary>
    private async Task EnsureSuccessAsync(
        HttpResponseMessage response, string operation, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        logger.LogWarning(
            "Control plane {Operation} returned {Status} ({Code}): {Body}",
            operation,
            (int)response.StatusCode,
            response.StatusCode,
            body);
        throw new HttpRequestException(
            $"Control plane {operation} returned {(int)response.StatusCode} {response.StatusCode}.",
            inner: null,
            statusCode: response.StatusCode);
    }
}