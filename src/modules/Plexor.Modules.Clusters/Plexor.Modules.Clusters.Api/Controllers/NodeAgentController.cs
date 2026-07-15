// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodeAgentController — NodeAgent-facing endpoints. /join is
// anonymous (the join token in the body is the credential); /heartbeat
// requires a valid node-bearer token (Phase 5+: wire the Sigil bearer
// handler with a node-token claim). For v0.1 both run without [Authorize]
// — the join token + node-bearer token are validated in the handler.
// ============================================================================

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Plexor.Modules.Clusters.Api.Models;
using Plexor.Modules.Clusters.Application.Abstractions;
using Plexor.Modules.Clusters.Application.Clusters;
using Plexor.Modules.Clusters.Domain;
using Plexor.Shared.Contracts.Routes;

namespace Plexor.Modules.Clusters.Api.Controllers;

/// <summary>
///     NodeAgent control-plane endpoints. Mounted at
///     <c>/api/v1/compute/clusters</c> alongside the cluster CRUD, but
///     kept in a separate controller because the caller is a machine
///     (NodeAgent), not a human operator — different auth model, no
///     <c>[RequirePermission]</c>.
/// </summary>
/// <param name="joinHandler"></param>
/// <param name="heartbeatHandler"></param>
[ApiController]
[Route($"{ApiRoutes.Base}/compute/clusters")]
[Tags(["compute", "node-agent"])]
public sealed class NodeAgentController(
    ICommandHandler<NodeJoinCommand, NodeJoinResult> joinHandler,
    ICommandHandler<NodeHeartbeatCommand, NodeHeartbeatResult> heartbeatHandler) : ControllerBase
{
    /// <summary>
    ///     <c>POST /api/v1/compute/clusters/join</c> — NodeAgent's first
    ///     call. Validates the join token, creates the Node row, returns
    ///     a node-bearer token + WireGuard config blob.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    [HttpPost("join", Name = "clusters-node-join")]
    [EndpointSummary("NodeAgent redeems a join token")]
    [ProducesResponseType<NodeJoinResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<NodeJoinResult>> JoinAsync(
        [FromBody] NodeJoinRequest request,
        CancellationToken cancellationToken)
    {

        var hardware = new NodeSpec(
            request.Hardware.Vcpu,
            request.Hardware.RamGb,
            request.Hardware.DiskGb,
            request.Hardware.Providers);
        return Ok(await joinHandler.HandleAsync(
            new NodeJoinCommand(request.JoinToken, request.Hostname, request.Role, hardware),
            cancellationToken));
    }

    /// <summary>
    ///     <c>POST /api/v1/compute/clusters/{clusterId}/heartbeat</c>
    ///     — periodic keepalive from a joined node (every 30 s).
    /// </summary>
    /// <param name="clusterId"></param>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    [HttpPost("{clusterId:guid}/heartbeat", Name = "clusters-node-heartbeat")]
    [EndpointSummary("NodeAgent keepalive")]
    [ProducesResponseType<NodeHeartbeatResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<NodeHeartbeatResult>> HeartbeatAsync(
        Guid clusterId,
        [FromBody] NodeHeartbeatRequest request,
        CancellationToken cancellationToken)
    {

        var hardware = new NodeSpec(
            request.Hardware.Vcpu,
            request.Hardware.RamGb,
            request.Hardware.DiskGb,
            request.Hardware.Providers);
        return Ok(await heartbeatHandler.HandleAsync(
            new NodeHeartbeatCommand(request.NodeId, clusterId, hardware),
            cancellationToken));
    }
}
