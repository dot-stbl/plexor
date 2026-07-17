// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// WorkloadsController — nested under /api/v1/compute/clusters/{clusterId}/workloads.
// Phase D Tier 2 surface: Create / List / Get / Delete. List is paged
// + filtered via the standard FilterQuery envelope; Get / Delete
// resolve the (clusterId, workloadId) tuple through the handler.
// Tier 5 adds action endpoints (start / stop / restart) that
// enqueue a per-node command via WorkloadActionCommandHandler.
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

// Route names — referenced by [HttpGet/Post/Patch/Delete(..., Name = ...)]
// and CreatedAtAction(...). CreatedAtAction looks up the action by
// its routing name (the value of Name =), NOT by the C# method name;
// nameof(GetAsync) would fail with 'Cannot resolve action'. The
// file-scope static class keeps the string in one place per file so
// refactors are safe and the compiler verifies both call sites match.
file static class WorkloadRouteNames
{
    /// <summary>POST /clusters/{clusterId}/workloads — provision a workload.</summary>
    public const string Create = "workloads-create";

    /// <summary>GET /clusters/{clusterId}/workloads — list paged.</summary>
    public const string List = "workloads-list";

    /// <summary>GET /clusters/{clusterId}/workloads/{workloadId} — fetch one.</summary>
    public const string Get = "workloads-get";

    /// <summary>DELETE /clusters/{clusterId}/workloads/{workloadId} — soft-delete.</summary>
    public const string Delete = "workloads-delete";

    /// <summary>POST .../workloads/{workloadId}/actions/start — start a workload (Tier 5).</summary>
    public const string ActionStart = "workloads-action-start";

    /// <summary>POST .../workloads/{workloadId}/actions/stop — stop a workload (Tier 5).</summary>
    public const string ActionStop = "workloads-action-stop";

    /// <summary>POST .../workloads/{workloadId}/actions/restart — restart a workload (Tier 5).</summary>
    public const string ActionRestart = "workloads-action-restart";
}

/// <summary>
///     Workload management endpoints. Mounted at
///     <c>/api/v1/compute/clusters/{clusterId}/workloads</c> —
///     nested resource, parent cluster id is part of the route
///     (matches the Azure YC URL convention for child resources).
///     All endpoints require the cluster.* permissions; workload
///     operations inherit the parent cluster's scope because the
///     cluster id is in the URL and the workload row is FK'd to it.
/// </summary>
/// <param name="createHandler">Create workload (POST).</param>
/// <param name="listHandler">Paged list (GET).</param>
/// <param name="getHandler">Single workload (GET by id).</param>
/// <param name="deleteHandler">Soft-delete (DELETE).</param>
/// <param name="actionHandler">Tier 5 start / stop / restart (POST action endpoints).</param>
[ApiController]
[Route($"{ApiRoutes.Base}/compute/clusters/{{clusterId}}/workloads")]
[Tags(["compute", "workloads"])]
[Authorize]
public sealed class WorkloadsController(
    ICommandHandler<CreateWorkloadCommand, WorkloadSummary> createHandler,
    ICommandHandler<ListWorkloadsQuery, PageResult<WorkloadSummary>> listHandler,
    ICommandHandler<GetWorkloadQuery, WorkloadSummary> getHandler,
    ICommandHandler<DeleteWorkloadCommand, Unit> deleteHandler,
    ICommandHandler<WorkloadActionCommand, WorkloadActionResult> actionHandler) : ControllerBase
{
    /// <summary>
    ///     <c>POST /api/v1/compute/clusters/{clusterId}/workloads</c> —
    ///     provision a new workload in the cluster. The control
    ///     plane creates the durable row; the NodeAgent's
    ///     drift-detection job (Phase D Tier 4) picks it up on its
    ///     next poll and reconciles with the local runtime.
    /// </summary>
    /// <param name="clusterId">Parent cluster.</param>
    /// <param name="request">Operator-supplied spec.</param>
    /// <param name="cancellationToken"></param>
    [HttpPost(Name = WorkloadRouteNames.Create)]
    [EndpointSummary("Provision a new workload in the cluster")]
    [RequirePermission(ClusterPermissions.Update)]
    [ProducesResponseType<WorkloadSummary>(StatusCodes.Status201Created)]
    public async Task<ActionResult<WorkloadSummary>> CreateAsync(
        [FromRoute] ClusterId clusterId,
        [FromBody] CreateWorkloadRequest request,
        CancellationToken cancellationToken)
    {
        var summary = await createHandler.HandleAsync(
            new CreateWorkloadCommand(clusterId, request.Name, request.Kind, request.SpecJson),
            cancellationToken);
        return CreatedAtAction(
            WorkloadRouteNames.Get,
            new { clusterId, workloadId = summary.Id },
            summary);
    }

    /// <summary>
    ///     <c>GET /api/v1/compute/clusters/{clusterId}/workloads</c> —
    ///     list workloads in the cluster, paged + filtered via
    ///     <see cref="FilterQuery" /> URL envelope (filter DSL +
    ///     sort + paging).
    /// </summary>
    /// <param name="clusterId">Parent cluster.</param>
    /// <param name="query">URL envelope.</param>
    /// <param name="cancellationToken"></param>
    [HttpGet(Name = WorkloadRouteNames.List)]
    [EndpointSummary("List workloads in the cluster")]
    [RequirePermission(ClusterPermissions.Read)]
    [ProducesResponseType<PageResult<WorkloadSummary>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PageResult<WorkloadSummary>>> ListAsync(
        [FromRoute] ClusterId clusterId,
        [FromQuery] FilterQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await listHandler.HandleAsync(
            new ListWorkloadsQuery(clusterId, query),
            cancellationToken));
    }

    /// <summary>
    ///     <c>GET /api/v1/compute/clusters/{clusterId}/workloads/{workloadId}</c> —
    ///     fetch one workload.
    /// </summary>
    /// <param name="clusterId">Parent cluster.</param>
    /// <param name="workloadId">Target workload.</param>
    /// <param name="cancellationToken"></param>
    [HttpGet("{workloadId}", Name = WorkloadRouteNames.Get)]
    [EndpointSummary("Fetch one workload")]
    [RequirePermission(ClusterPermissions.Read)]
    [ProducesResponseType<WorkloadSummary>(StatusCodes.Status200OK)]
    public async Task<ActionResult<WorkloadSummary>> GetAsync(
        [FromRoute] ClusterId clusterId,
        [FromRoute] WorkloadId workloadId,
        CancellationToken cancellationToken)
    {
        return Ok(await getHandler.HandleAsync(
            new GetWorkloadQuery(clusterId, workloadId),
            cancellationToken));
    }

    /// <summary>
    ///     <c>DELETE /api/v1/compute/clusters/{clusterId}/workloads/{workloadId}</c> —
    ///     soft-delete a workload. The NodeAgent's next drift poll
    ///     tears down the local runtime handle.
    /// </summary>
    /// <param name="clusterId">Parent cluster.</param>
    /// <param name="workloadId">Target workload.</param>
    /// <param name="cancellationToken"></param>
    [HttpDelete("{workloadId}", Name = WorkloadRouteNames.Delete)]
    [EndpointSummary("Soft-delete a workload")]
    [RequirePermission(ClusterPermissions.Update)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> DeleteAsync(
        [FromRoute] ClusterId clusterId,
        [FromRoute] WorkloadId workloadId,
        CancellationToken cancellationToken)
    {
        await deleteHandler.HandleAsync(
            new DeleteWorkloadCommand(clusterId, workloadId),
            cancellationToken);
        return NoContent();
    }

    /// <summary>
    ///     <c>POST /api/v1/compute/clusters/{clusterId}/workloads/{workloadId}/actions/start</c>
    ///     — start a previously provisioned workload. The control
    ///     plane enqueues a <c>workload.start</c> command on the
    ///     assigned node's queue; the agent's long-poll picks it
    ///     up, executes, and posts back the result. The endpoint
    ///     returns the post-action state when the agent acknowledges
    ///     (typically under 10 seconds; capped at 30 seconds before
    ///     a 500 response).
    /// </summary>
    /// <param name="clusterId">Parent cluster.</param>
    /// <param name="workloadId">Target workload.</param>
    /// <param name="cancellationToken"></param>
    [HttpPost("{workloadId}/actions/start", Name = WorkloadRouteNames.ActionStart)]
    [EndpointSummary("Start a workload")]
    [RequirePermission(ClusterPermissions.Update)]
    [ProducesResponseType<WorkloadActionResult>(StatusCodes.Status200OK)]
    public async Task<WorkloadActionResult> StartAsync(
        [FromRoute] ClusterId clusterId,
        [FromRoute] WorkloadId workloadId,
        CancellationToken cancellationToken)
    {
        return await actionHandler.HandleAsync(
            new WorkloadActionCommand(clusterId, workloadId, WorkloadAction.Start),
            cancellationToken);
    }

    /// <summary>
    ///     <c>POST /api/v1/compute/clusters/{clusterId}/workloads/{workloadId}/actions/stop</c>
    ///     — gracefully shut down a running workload. Resources
    ///     stay allocated (the workload is stopped, not deleted).
    /// </summary>
    /// <param name="clusterId">Parent cluster.</param>
    /// <param name="workloadId">Target workload.</param>
    /// <param name="cancellationToken"></param>
    [HttpPost("{workloadId}/actions/stop", Name = WorkloadRouteNames.ActionStop)]
    [EndpointSummary("Stop a workload")]
    [RequirePermission(ClusterPermissions.Update)]
    [ProducesResponseType<WorkloadActionResult>(StatusCodes.Status200OK)]
    public async Task<WorkloadActionResult> StopAsync(
        [FromRoute] ClusterId clusterId,
        [FromRoute] WorkloadId workloadId,
        CancellationToken cancellationToken)
    {
        return await actionHandler.HandleAsync(
            new WorkloadActionCommand(clusterId, workloadId, WorkloadAction.Stop),
            cancellationToken);
    }

    /// <summary>
    ///     <c>POST /api/v1/compute/clusters/{clusterId}/workloads/{workloadId}/actions/restart</c>
    ///     — stop then start. Convenience for the UI; the agent
    ///     handles the pair in two long-poll cycles.
    /// </summary>
    /// <param name="clusterId">Parent cluster.</param>
    /// <param name="workloadId">Target workload.</param>
    /// <param name="cancellationToken"></param>
    [HttpPost("{workloadId}/actions/restart", Name = WorkloadRouteNames.ActionRestart)]
    [EndpointSummary("Restart a workload")]
    [RequirePermission(ClusterPermissions.Update)]
    [ProducesResponseType<WorkloadActionResult>(StatusCodes.Status200OK)]
    public async Task<WorkloadActionResult> RestartAsync(
        [FromRoute] ClusterId clusterId,
        [FromRoute] WorkloadId workloadId,
        CancellationToken cancellationToken)
    {
        return await actionHandler.HandleAsync(
            new WorkloadActionCommand(clusterId, workloadId, WorkloadAction.Restart),
            cancellationToken);
    }
}
