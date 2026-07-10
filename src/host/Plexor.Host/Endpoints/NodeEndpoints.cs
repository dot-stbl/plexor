// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodeEndpoints — four minimal-API mappings that implement the
// Plexor.NodeAgent control loop:
//
//   POST /api/v1/nodes/join
//       Body : { joinToken, hardware }
//       Resp : 201 Created { nodeId, controlPlaneUrl }
//
//   POST /api/v1/nodes/{nodeId}/heartbeat
//       Body : { nodeId, sentAt, hardware, runningVmCount }
//       Resp : 200 OK (no body)
//
//   POST /api/v1/nodes/{nodeId}/commands/poll
//       Body : { nodeId, maxBatch, waitCursor? }
//       Resp : 200 OK { commands: [...], nextCursor }
//
//   POST /api/v1/nodes/{nodeId}/commands/{commandId}/result
//       Body : { commandId, nodeId, status, errorMessage?, completedAt }
//       Resp : 200 OK (no body)
//
// v0.1: no auth, no rate-limit. /join accepts any non-empty token;
// /heartbeat and /result return 200 silently for unknown nodes
// (the registry logs the warning and the endpoint stays no-op).
// ============================================================================

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plexor.Host.Abstractions;
using Plexor.Shared.NodeApi;

namespace Plexor.Host.Endpoints;

/// <summary>
/// Minimal-API mappings for the Plexor.NodeAgent control loop.
/// Composed via <c>PlexorHostServiceCollectionExtensions.AddNodeCore</c>.
/// </summary>
internal static class NodeEndpoints
{
    /// <summary>Map the four node endpoints under <c>/api/v1/nodes</c>.</summary>
    public static IEndpointRouteBuilder MapNodeEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes
            .MapGroup("/api/v1/nodes")
            .WithTags("nodes");

        group.MapPost("/join", JoinAsync)
            .WithName("NodeJoin")
            .WithSummary("Register a compute node with the control plane.");

        group.MapPost("/{nodeId:guid}/heartbeat", HeartbeatAsync)
            .WithName("NodeHeartbeat")
            .WithSummary("Record liveness from a registered node.");

        group.MapPost("/{nodeId:guid}/commands/poll", PollAsync)
            .WithName("NodeCommandPoll")
            .WithSummary("Dequeue any commands the control plane has for this node.");

        group.MapPost(
                "/{nodeId:guid}/commands/{commandId:guid}/result",
                ResultAsync)
            .WithName("NodeCommandResult")
            .WithSummary("Report completion status for a previously-dequeued command.");

        return routes;
    }

    /// <summary>POST /api/v1/nodes/join.</summary>
    public static async Task<IResult> JoinAsync(
        JoinRequest request,
        INodeRegistry registry,
        CancellationToken ct)
    {
        // v0.1: structural validation only. Real impl routes through
        // a FluentValidation validator (JoinRequestValidator in
        // Plexor.Host.Validation) — see anti-patterns.md rule 4.
        if (string.IsNullOrWhiteSpace(request.JoinToken))
        {
            return Results.BadRequest(new
            {
                type = "https://plexor.io/problems/empty-join-token",
                title = "join_token is required",
                status = StatusCodes.Status400BadRequest,
            });
        }

        if (request.Hardware.CpuCores <= 0 || request.Hardware.RamBytes <= 0)
        {
            return Results.BadRequest(new
            {
                type = "https://plexor.io/problems/invalid-hardware",
                title = "hardware snapshot is invalid (cpu_cores and ram_bytes must be > 0)",
                status = StatusCodes.Status400BadRequest,
            });
        }

        var response = await registry.RegisterAsync(request, ct);
        return Results.Created($"/api/v1/nodes/{response.NodeId}", response);
    }

    /// <summary>POST /api/v1/nodes/{nodeId}/heartbeat.</summary>
    public static async Task<IResult> HeartbeatAsync(
        Guid nodeId,
        HeartbeatRequest request,
        INodeRegistry registry,
        CancellationToken ct)
    {
        // The route param and the body field carry the same id; reject
        // mismatches early so the agent can't accidentally update a
        // different node's heartbeat.
        if (request.NodeId != nodeId)
        {
            return Results.BadRequest(new
            {
                type = "https://plexor.io/problems/heartbeat-node-mismatch",
                title = "route nodeId does not match body nodeId",
                status = StatusCodes.Status400BadRequest,
            });
        }

        await registry.TouchHeartbeatAsync(request, ct);
        return Results.Ok();
    }

    /// <summary>POST /api/v1/nodes/{nodeId}/commands/poll.</summary>
    public static async Task<IResult> PollAsync(
        Guid nodeId,
        CommandPollRequest request,
        INodeRegistry registry,
        CancellationToken ct)
    {
        // Force nodeId onto the request from the route so callers
        // can't mix-and-match ids in the body (the registry keys
        // the queue by id).
        var fixedRequest = request with { NodeId = nodeId };

        var response = await registry.DequeueCommandsAsync(fixedRequest, ct);
        return Results.Ok(response);
    }

    /// <summary>POST /api/v1/nodes/{nodeId}/commands/{commandId}/result.</summary>
    public static async Task<IResult> ResultAsync(
        Guid nodeId,
        Guid commandId,
        CommandResult result,
        INodeRegistry registry,
        CancellationToken ct)
    {
        // Same route/body consistency check as the heartbeat endpoint.
        if (result.NodeId != nodeId || result.CommandId != commandId)
        {
            return Results.BadRequest(new
            {
                type = "https://plexor.io/problems/result-id-mismatch",
                title = "route nodeId/commandId do not match body fields",
                status = StatusCodes.Status400BadRequest,
            });
        }

        await registry.SubmitResultAsync(result, ct);
        return Results.Ok();
    }
}