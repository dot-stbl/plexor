// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodeController — REST endpoints for the Plexor.NodeAgent control loop.
//
//   POST /api/v1/nodes/join
//       Body : { joinToken, hardware }
//       Resp : 201 Created { nodeId, controlPlaneUrl }
//
//   POST /api/v1/nodes/{nodeId}/heartbeat
//       Body : { nodeId, sentAt, hardware, runningVmCount }
//       Resp : 200 OK
//
//   POST /api/v1/nodes/{nodeId}/commands/poll
//       Body : { nodeId, maxBatch, waitCursor? }
//       Resp : 200 OK { commands: [...], nextCursor }
//
//   POST /api/v1/nodes/{nodeId}/commands/{commandId}/result
//       Body : { commandId, nodeId, status, errorMessage?, completedAt }
//       Resp : 200 OK
//
// v0.1: no auth, no rate-limit. /join accepts any non-empty token;
// /heartbeat and /result return 200 silently for unknown nodes
// (the registry logs the warning and the endpoint stays no-op).
// ============================================================================

using Microsoft.AspNetCore.Mvc;
using Plexor.Host.Abstractions;
using Plexor.Shared.Contracts.Routes;
using Plexor.Shared.NodeApi;

namespace Plexor.Host.Controllers;

/// <summary>
///     REST surface for the Plexor.NodeAgent control loop. Mounted at
///     <c>/api/v1/nodes</c> via the <see cref="ApiRoutes.Base" />
///     constant; the same prefix every other Plexor controller uses.
/// </summary>
/// <param name="registry"></param>
/// <remarks>
///     DI-injected registry. Per-update synchronization
///     is owned by the in-memory implementation (v0.1) or
///     whatever store backs the interface in v0.2+.
/// </remarks>
[ApiController]
[Route($"{ApiRoutes.Base}/nodes")]
[Tags("nodes")]
public sealed class NodeController(INodeRegistry registry) : ControllerBase
{
    /// <summary>
    ///     POST /api/v1/nodes/join — register a compute node
    ///     with the control plane. First call the agent makes.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    [HttpPost("join", Name = "node-join")]
    [EndpointSummary("Register a compute node with the control plane")]
    [EndpointDescription(
        "First message the agent sends. Returns the canonical NodeId and the URL the agent " +
        "should call back to. The join token is not verified in v0.1 (every join gets a fresh " +
        "Guid); verification lands with the auth iteration.")]
    [ProducesResponseType(typeof(JoinResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<JoinResponse>> JoinAsync(
        [FromBody] JoinRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.JoinToken))
        {
            ModelState.AddModelError(
                nameof(request.JoinToken),
                "join_token is required");

            return ValidationProblem(ModelState);
        }

        if (request.Hardware.CpuCores <= 0 || request.Hardware.RamBytes <= 0)
        {
            ModelState.AddModelError(
                nameof(request.Hardware),
                "hardware snapshot is invalid (cpu_cores and ram_bytes must be > 0)");

            return ValidationProblem(ModelState);
        }

        var response = await registry.RegisterAsync(request, cancellationToken);
        return CreatedAtAction(
            null,
            new { nodeId = response.NodeId },
            response);
    }

    /// <summary>
    ///     POST /api/v1/nodes/{nodeId}/heartbeat — record
    ///     liveness from a registered node. Called on a fixed
    ///     interval (default 30s); three missed intervals flips the
    ///     node to Offline in the future health monitor.
    /// </summary>
    /// <param name="nodeId"></param>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    [HttpPost("{nodeId:guid}/heartbeat", Name = "node-heartbeat")]
    [EndpointSummary("Record liveness from a registered node")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> HeartbeatAsync(
        [FromRoute] Guid nodeId,
        [FromBody] HeartbeatRequest request,
        CancellationToken cancellationToken)
    {
        // Reject route/body id mismatches so the agent can't
        // accidentally update a different node's heartbeat.
        if (request.NodeId != nodeId)
        {
            ModelState.AddModelError(
                nameof(request.NodeId),
                "route nodeId does not match body nodeId");

            return ValidationProblem(ModelState);
        }

        await registry.TouchHeartbeatAsync(request, cancellationToken);
        return Ok();
    }

    /// <summary>
    ///     POST /api/v1/nodes/{nodeId}/commands/poll —
    ///     long-poll for commands targeted at this node. The agent
    ///     sends a new request on a fixed interval (default 5s) and
    ///     includes the <c>NextCursor</c> from the previous
    ///     response.
    /// </summary>
    /// <param name="nodeId"></param>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    [HttpPost("{nodeId:guid}/commands/poll", Name = "node-command-poll")]
    [EndpointSummary("Dequeue any commands the control plane has for this node")]
    [ProducesResponseType(typeof(CommandPollResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CommandPollResponse>> PollAsync(
        [FromRoute] Guid nodeId,
        [FromBody] CommandPollRequest request,
        CancellationToken cancellationToken)
    {
        // Force nodeId onto the request from the route so the
        // registry keys the queue by the path-bound id, not the
        // (potentially stale) body field.
        var fixedRequest = request with { NodeId = nodeId };

        var response = await registry.DequeueCommandsAsync(fixedRequest, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    ///     POST /api/v1/nodes/{nodeId}/commands/{commandId}/result —
    ///     report completion status for a previously-dequeued
    ///     command. v0.1 logs the result; future iterations route
    ///     it to the audit module for persistence.
    /// </summary>
    /// <param name="nodeId"></param>
    /// <param name="commandId"></param>
    /// <param name="result"></param>
    /// <param name="cancellationToken"></param>
    [HttpPost("{nodeId:guid}/commands/{commandId:guid}/result", Name = "node-command-result")]
    [EndpointSummary("Report completion status for a previously-dequeued command")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ResultAsync(
        [FromRoute] Guid nodeId,
        [FromRoute] Guid commandId,
        [FromBody] CommandResult result,
        CancellationToken cancellationToken)
    {
        if (result.NodeId != nodeId || result.CommandId != commandId)
        {
            ModelState.AddModelError(
                nameof(result.NodeId),
                "route nodeId/commandId do not match body fields");

            return ValidationProblem(ModelState);
        }

        await registry.SubmitResultAsync(result, cancellationToken);
        return Ok();
    }
}
