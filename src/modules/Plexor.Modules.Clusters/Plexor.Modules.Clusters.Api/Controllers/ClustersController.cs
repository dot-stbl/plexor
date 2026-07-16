// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ClustersController — /api/v1/compute/clusters CRUD under
// [RequirePermission]. Every endpoint except /join requires an
// authenticated caller with the matching permission claim.
// ============================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Plexor.Modules.Clusters.Api.Models;
using Plexor.Modules.Clusters.Application.Abstractions;
using Plexor.Modules.Clusters.Application.Authorization;
using Plexor.Modules.Clusters.Application.Clusters;
using Plexor.Modules.Clusters.Infrastructure.Clusters;
using Plexor.Shared.Authorization;
using Plexor.Shared.Contracts.Pagination;
using Plexor.Shared.Contracts.Routes;
using Plexor.Shared.Filtering.Query;
using Plexor.Shared.Identifiers;

namespace Plexor.Modules.Clusters.Api.Controllers;

/// <summary>
///     Cluster management endpoints. Mounted at
///     <c>/api/v1/compute/clusters</c> via <see cref="ApiRoutes.Base" />.
/// </summary>
/// <param name="createHandler"></param>
/// <param name="updateHandler"></param>
/// <param name="deleteHandler"></param>
/// <param name="getHandler"></param>
/// <param name="listHandler"></param>
/// <param name="rotateHandler"></param>
/// <param name="listNodesHandler"></param>
[ApiController]
[Route($"{ApiRoutes.Base}/compute/clusters")]
[Tags(["compute", "clusters"])]
[Authorize]
public sealed class ClustersController(
    ICommandHandler<CreateClusterCommand, JoinTokenResult> createHandler,
    ICommandHandler<UpdateClusterCommand, ClusterSummary> updateHandler,
    ICommandHandler<DeleteClusterCommand, Unit> deleteHandler,
    ICommandHandler<GetClusterQuery, ClusterDetail> getHandler,
    ICommandHandler<ListClustersQuery, PageResult<ClusterSummary>> listHandler,
    ICommandHandler<RotateJoinTokenCommand, JoinTokenResult> rotateHandler,
    ICommandHandler<ListNodesQuery, IReadOnlyList<NodeSummary>> listNodesHandler) : ControllerBase
{
    /// <summary>
    ///     <c>POST /api/v1/compute/clusters</c> — provision a new cluster.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    [HttpPost(Name = "clusters-create")]
    [EndpointSummary("Provision a new cluster + initial join token")]
    [RequirePermission(ClusterPermissions.Create)]
    [ProducesResponseType<JoinTokenResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<JoinTokenResult>> CreateAsync(
        [FromBody] CreateClusterRequest request,
        CancellationToken cancellationToken)
    {

        // OrgId comes from the caller's claims in v0.2+; for v0.1 we
        // derive it from the request (single-tenant MVP — every caller
        // belongs to the same org). Phase 5 follow-up: read org from ICurrentUser.
        var orgId = Guid.Empty;
        var result = await createHandler.HandleAsync(
            new CreateClusterCommand(orgId, request.Name, request.Region, request.InitialNodeRole),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>
    ///     <c>GET /api/v1/compute/clusters</c> — list clusters in the
    ///     caller's org, paged + filtered via <see cref="FilterQuery" />
    ///     URL envelope.
    /// </summary>
    [HttpGet(Name = "clusters-list")]
    [EndpointSummary("List clusters in the caller's org")]
    [RequirePermission(ClusterPermissions.Read)]
    [ProducesResponseType<PageResult<ClusterSummary>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PageResult<ClusterSummary>>> ListAsync(
        [FromQuery] FilterQuery query,
        CancellationToken cancellationToken)
    {
        // Phase 5 follow-up: read org from ICurrentUser (v0.2+). v0.1 single-tenant.
        var orgId = Guid.Empty;
        return Ok(await listHandler.HandleAsync(
            new ListClustersQuery(orgId, query),
            cancellationToken));
    }

    /// <summary>
    ///     <c>GET /api/v1/compute/clusters/{clusterId}</c> — fetch one
    ///     cluster with its child nodes loaded.
    /// </summary>
    /// <param name="clusterId"></param>
    /// <param name="cancellationToken"></param>
    [HttpGet("{clusterId}", Name = "clusters-get")]
    [EndpointSummary("Fetch one cluster with its nodes")]
    [RequirePermission(ClusterPermissions.Read)]
    [ProducesResponseType<ClusterDetail>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ClusterDetail>> GetAsync(
        [FromRoute] ClusterId clusterId,
        CancellationToken cancellationToken)
    {
        return Ok(await getHandler.HandleAsync(new GetClusterQuery(clusterId), cancellationToken));
    }

    /// <summary>
    ///     <c>PATCH /api/v1/compute/clusters/{clusterId}</c> — rename
    ///     or change region.
    /// </summary>
    /// <param name="clusterId"></param>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    [HttpPatch("{clusterId}", Name = "clusters-update")]
    [EndpointSummary("Rename or change region for a cluster")]
    [RequirePermission(ClusterPermissions.Update)]
    [ProducesResponseType<ClusterSummary>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ClusterSummary>> UpdateAsync(
        [FromRoute] ClusterId clusterId,
        [FromBody] UpdateClusterRequest request,
        CancellationToken cancellationToken)
    {

        return Ok(await updateHandler.HandleAsync(
            new UpdateClusterCommand(clusterId, request.Name, request.Region),
            cancellationToken));
    }

    /// <summary>
    ///     <c>DELETE /api/v1/compute/clusters/{clusterId}</c> — soft-
    ///     delete a cluster (cascades node status to Gone).
    /// </summary>
    /// <param name="clusterId"></param>
    /// <param name="cancellationToken"></param>
    [HttpDelete("{clusterId}", Name = "clusters-delete")]
    [EndpointSummary("Soft-delete a cluster")]
    [RequirePermission(ClusterPermissions.Delete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> DeleteAsync(
        [FromRoute] ClusterId clusterId,
        CancellationToken cancellationToken)
    {
        await deleteHandler.HandleAsync(new DeleteClusterCommand(clusterId), cancellationToken);
        return NoContent();
    }

    /// <summary>
    ///     <c>POST /api/v1/compute/clusters/{clusterId}/rotate-join-token</c>
    ///     — revoke the old token, issue a new one (7-day TTL).
    /// </summary>
    /// <param name="clusterId"></param>
    /// <param name="cancellationToken"></param>
    [HttpPost("{clusterId}/rotate-join-token", Name = "clusters-rotate-join-token")]
    [EndpointSummary("Rotate the cluster's join token")]
    [RequirePermission(ClusterPermissions.Update)]
    [ProducesResponseType<JoinTokenResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<JoinTokenResult>> RotateJoinTokenAsync(
        [FromRoute] ClusterId clusterId,
        CancellationToken cancellationToken)
    {
        return Ok(await rotateHandler.HandleAsync(
            new RotateJoinTokenCommand(clusterId),
            cancellationToken));
    }

    /// <summary>
    ///     <c>GET /api/v1/compute/clusters/{clusterId}/nodes</c> —
    ///     list nodes joined to this cluster.
    /// </summary>
    /// <param name="clusterId"></param>
    /// <param name="cancellationToken"></param>
    [HttpGet("{clusterId}/nodes", Name = "clusters-list-nodes")]
    [EndpointSummary("List nodes in one cluster")]
    [RequirePermission(ClusterPermissions.NodesRead)]
    [ProducesResponseType<IReadOnlyList<NodeSummary>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<NodeSummary>>> ListNodesAsync(
        [FromRoute] ClusterId clusterId,
        CancellationToken cancellationToken)
    {
        return Ok(await listNodesHandler.HandleAsync(
            new ListNodesQuery(clusterId),
            cancellationToken));
    }
}
