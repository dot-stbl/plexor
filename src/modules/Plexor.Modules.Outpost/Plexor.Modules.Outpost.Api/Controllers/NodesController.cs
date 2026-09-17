// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodesController — operator + NodeAgent facing endpoints for the host-side
// node registry.
//
// Mounted at <c>/api/v1/nodes</c> via ApiRoutes.Resource("nodes").
// Five endpoints per the v0.1 spec:
//   * GET    /nodes          — list nodes in one cluster
//   * GET    /nodes/{id}     — detail
//   * POST   /nodes/register — NodeAgent's first call (anonymous; join token is the credential)
//   * POST   /nodes/heartbeat— periodic keepalive (anonymous; bearer token is the credential)
//   * GET    /nodes/{id}/health — derived health classification
//
// Authorization: register + heartbeat are [AllowAnonymous] (the join
// token / node-bearer token in the body is the credential). Read
// endpoints require an authenticated caller with the matching
// permission claim (Phase 5+).
// ============================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Plexor.Modules.Clusters.Domain;
using Plexor.Modules.Outpost.Api.Models;
using Plexor.Modules.Outpost.Application;
using Plexor.Modules.Outpost.Application.Abstractions;
using Plexor.Modules.Outpost.Application.NodeCommands;
using Plexor.Shared.Contracts.Routes;
using Plexor.Shared.Identifiers;
using Plexor.Shared.NodeApi;
using ClusterStatus = Plexor.Modules.Clusters.Domain.ClusterStatus;

namespace Plexor.Modules.Outpost.Api.Controllers;

/// <summary>
///     Host-side node registry endpoints. Mounted at
///     <c>/api/v1/nodes</c>.
/// </summary>
/// <param name="registerHandler">Register-node command handler.</param>
/// <param name="heartbeatHandler">Heartbeat command handler.</param>
/// <param name="listHandler">List-nodes query handler.</param>
/// <param name="getHandler">Get-node query handler.</param>
/// <param name="evaluator">Heartbeat evaluator (Health classification).</param>
/// <param name="clock">Wall-clock source for ServerTime stamping.</param>
[ApiController]
[Route($"{ApiRoutes.Base}/nodes")]
[Tags(["nodes"])]
[Authorize]
public sealed class NodesController(
    ICommandHandler<RegisterNodeCommand, RegisterNodeResult> registerHandler,
    ICommandHandler<HeartbeatCommand, HeartbeatResult> heartbeatHandler,
    ICommandHandler<ListNodesQuery, IReadOnlyList<NodeRecord>> listHandler,
    ICommandHandler<GetNodeQuery, NodeRecord?> getHandler,
    INodeHeartbeatEvaluator evaluator,
    TimeProvider clock) : ControllerBase
{
    /// <summary>
    ///     <c>GET /api/v1/nodes?clusterId=X</c> — list nodes in the
    ///     supplied cluster, filtered by the caller's org.
    ///     <c>clusterId</c> is required for v0.1 — Phase 5+ adds
    ///     org-wide listing.
    /// </summary>
    /// <param name="clusterId">Filter to this cluster (wire format).</param>
    /// <param name="cancellationToken">Forwarded to the DB read.</param>
    [HttpGet(Name = "nodes-list")]
    [EndpointSummary("List nodes in a cluster")]
    [ProducesResponseType<NodeListResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<NodeListResponse>> ListAsync(
        [FromQuery] string clusterId,
        CancellationToken cancellationToken)
    {
        var parsedClusterId = IdParse.ParseClusterId(clusterId);
        var nodes = await listHandler.HandleAsync(
            new ListNodesQuery(parsedClusterId),
            cancellationToken);
        return Ok(new NodeListResponse(nodes.Select(NodeRecordResponseMapper.ToResponse).ToArray()));
    }

    /// <summary>
    ///     <c>GET /api/v1/nodes/{nodeId}</c> — fetch one node by id.
    /// </summary>
    /// <param name="nodeId">Target node id (wire format).</param>
    /// <param name="cancellationToken">Forwarded to the DB read.</param>
    [HttpGet("{nodeId}", Name = "nodes-get")]
    [EndpointSummary("Fetch one node by id")]
    [ProducesResponseType<NodeResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<NodeResponse>> GetAsync(
        [FromRoute] NodeId nodeId,
        CancellationToken cancellationToken)
    {
        var node = await getHandler.HandleAsync(new GetNodeQuery(nodeId), cancellationToken);
        return node is null ? NotFound() : Ok(NodeRecordResponseMapper.ToResponse(node));
    }

    /// <summary>
    ///     <c>POST /api/v1/nodes/register</c> — NodeAgent's first call.
    ///     Validates the join token, creates the outpost.node_records
    ///     row, returns a node-bearer token + cluster endpoint.
    /// </summary>
    /// <param name="request">Join payload (token + hardware + role).</param>
    /// <param name="cancellationToken">Forwarded to the DB writes.</param>
    [HttpPost("register", Name = "nodes-register")]
    [EndpointSummary("NodeAgent redeems a join token")]
    [AllowAnonymous]
    [ProducesResponseType<RegisterNodeResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RegisterNodeResponse>> RegisterAsync(
        [FromBody] RegisterNodeRequest request,
        CancellationToken cancellationToken)
    {
        var hardware = new Plexor.Modules.Outpost.Application.NodeSpec(
            request.Hardware.Vcpu,
            request.Hardware.RamGb,
            request.Hardware.DiskGb,
            request.Hardware.Providers);

        var result = await registerHandler.HandleAsync(
            new RegisterNodeCommand(
                request.JoinToken,
                request.Hostname,
                request.IpAddress,
                request.Role,
                hardware,
                request.IsoVersion,
                request.WireguardPublicKey),
            cancellationToken);

        return Ok(new RegisterNodeResponse(
            result.NodeRecord.Id.ToString(),
            result.NodeRecord.ClusterId.ToString(),
            result.ClusterEndpoint));
    }

    /// <summary>
    ///     <c>POST /api/v1/nodes/heartbeat</c> — periodic keepalive from
    ///     a joined node (every 30 s). The body carries the node id +
    ///     cluster id + fresh hardware snapshot + refreshed IP address.
    /// </summary>
    /// <param name="request">Heartbeat payload.</param>
    /// <param name="cancellationToken">Forwarded to the DB write.</param>
    [HttpPost("heartbeat", Name = "nodes-heartbeat")]
    [EndpointSummary("NodeAgent keepalive")]
    [AllowAnonymous]
    [ProducesResponseType<HeartbeatResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<HeartbeatResponse>> HeartbeatAsync(
        [FromBody] NodeHeartbeatRequest request,
        CancellationToken cancellationToken)
    {
        var nodeId = IdParse.ParseNodeId(request.NodeId);
        var clusterId = IdParse.ParseClusterId(request.ClusterId);
        var hardware = new Plexor.Modules.Outpost.Application.NodeSpec(
            request.Hardware.Vcpu,
            request.Hardware.RamGb,
            request.Hardware.DiskGb,
            request.Hardware.Providers);

        var result = await heartbeatHandler.HandleAsync(
            new HeartbeatCommand(nodeId, clusterId, hardware, request.IpAddress),
            cancellationToken);

        return Ok(new HeartbeatResponse(
            result.NodeId,
            result.ClusterStatus,
            result.ServerTime));
    }

    /// <summary>
    ///     <c>GET /api/v1/nodes/{nodeId}/health</c> — derived health
    ///     classification. Not persisted; computed by the evaluator
    ///     from the row's LastHeartbeatAt + the configured
    ///     Healthy/Unhealthy windows.
    /// </summary>
    /// <param name="nodeId">Target node id (wire format).</param>
    /// <param name="cancellationToken">Forwarded to the DB read.</param>
    [HttpGet("{nodeId}/health", Name = "nodes-health")]
    [EndpointSummary("Derived node health (Healthy / Stale / Unhealthy)")]
    [ProducesResponseType<NodeHealthResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<NodeHealthResponse>> HealthAsync(
        [FromRoute] NodeId nodeId,
        CancellationToken cancellationToken)
    {
        var node = await getHandler.HandleAsync(new GetNodeQuery(nodeId), cancellationToken);
        if (node is null)
        {
            return NotFound();
        }

        var now = clock.GetUtcNow();
        var health = evaluator.Evaluate(node.Id, node.LastHeartbeatAt, now);
        return Ok(new NodeHealthResponse(node.Id, node.Status, health, node.LastHeartbeatAt, now));
    }
}