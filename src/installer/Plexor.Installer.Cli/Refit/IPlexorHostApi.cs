// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IPlexorHostApi — Refit-typed HTTP client for the `plx host *`
// command tree. Each method maps 1:1 to an endpoint on
// Plexor.Host's Outpost.Api (see Plexor.Modules.Outpost.Api).
//
// The CLI's BaseAddress is set at registration time (the value of
// `--host` or `PlxConfig.Host`); the routes below compose against
// that base. Bearer authentication is added by an auth handler
// reading the `--token` / `PlxConfig.Token` value at request time.
//
// REMOVE endpoint note: the v0.1 host controller does NOT yet
// expose `DELETE /api/v1/nodes/{id}` — it relies on the heartbeat
// evaluator to flip nodes to `Gone` after 90 s of silence. The
// method below is declared so the CLI can be ready the moment the
// host ships the endpoint; until then, calling it surfaces a 404
// from the host which the command translates into a clear "not
// supported on this host version" message.
//
// CLUSTER methods (Group C, v0.3.0): CRUD over
// /api/v1/compute/clusters. The host's controller lives in
// Plexor.Modules.Clusters.Api (ClustersController). List / Get
// return HostClusterPage / HostClusterDetail respectively; Create
// returns a one-shot JoinTokenResult; Rotate mirrors Create;
// Delete is a 204 No Content.
// ============================================================================

using Refit;

namespace Plexor.Installer.Cli.Refit;

/// <summary>
///     Refit-typed HTTP client surface for `plx host` commands.
///     BaseAddress is supplied at registration time; the bearer
///     token is attached per request by an auth handler.
/// </summary>
public interface IPlexorHostApi
{
    /// <summary>
    ///     <c>GET /api/v1/nodes?clusterId=X</c> — list nodes in the
    ///     given cluster. <paramref name="clusterId" /> is the
    ///     wire string the host's controller parses via
    ///     <c>IdParse.ParseClusterId</c>.
    /// </summary>
    /// <param name="clusterId">Filter to this cluster id.</param>
    /// <param name="cancellationToken">Forwarded to the HTTP call.</param>
    [Get("/api/v1/nodes")]
    public Task<HostNodeListResponse> ListNodesAsync(
        [AliasAs("clusterId")] string clusterId,
        CancellationToken cancellationToken);

    /// <summary>
    ///     <c>GET /api/v1/nodes/{nodeId}</c> — fetch one node by
    ///     id (wire string).
    /// </summary>
    /// <param name="nodeId">Target node id.</param>
    /// <param name="cancellationToken">Forwarded to the HTTP call.</param>
    [Get("/api/v1/nodes/{nodeId}")]
    public Task<HostNodeResponse> GetNodeAsync(
        string nodeId,
        CancellationToken cancellationToken);

    /// <summary>
    ///     <c>GET /api/v1/nodes/{nodeId}/health</c> — derived
    ///     health classification (Healthy / Stale / Unhealthy) +
    ///     the host's current UTC now.
    /// </summary>
    /// <param name="nodeId">Target node id.</param>
    /// <param name="cancellationToken">Forwarded to the HTTP call.</param>
    [Get("/api/v1/nodes/{nodeId}/health")]
    public Task<HostNodeHealthResponse> GetNodeHealthAsync(
        string nodeId,
        CancellationToken cancellationToken);

    /// <summary>
    ///     <c>POST /api/v1/nodes/register</c> — operator-driven
    ///     pre-registration of a node. The CLI fills the join
    ///     token + hostname + IP + role + zero hardware and gets
    ///     back a node row + node-bearer token (shown once) +
    ///     cluster endpoint.
    /// </summary>
    /// <param name="request">Join payload.</param>
    /// <param name="cancellationToken">Forwarded to the HTTP call.</param>
    [Post("/api/v1/nodes/register")]
    public Task<HostRegisterNodeResponse> RegisterNodeAsync(
        [Body] HostRegisterNodeRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    ///     <c>DELETE /api/v1/nodes/{nodeId}</c> — explicitly
    ///     unregister a node. The v0.1 host controller does not
    ///     yet expose this endpoint; the CLI surfaces a clear
    ///     "not supported on this host version" message when the
    ///     host returns 404.
    /// </summary>
    /// <param name="nodeId">Target node id.</param>
    /// <param name="cancellationToken">Forwarded to the HTTP call.</param>
    [Delete("/api/v1/nodes/{nodeId}")]
    public Task DeleteNodeAsync(
        string nodeId,
        CancellationToken cancellationToken);

    /// <summary>
    ///     <c>GET /api/v1/compute/clusters</c> — list clusters in
    ///     the caller's org, paged via the host's standard
    ///     <c>FilterQuery</c> envelope. <paramref name="page" />
    ///     and <paramref name="pageSize" /> map to the
    ///     <c>?page=&amp;pageSize=</c> query parameters; the host's
    ///     default for unspecified filter / sort is "no filter,
    ///     default sort".
    /// </summary>
    /// <param name="page">1-based page index.</param>
    /// <param name="pageSize">Items per page (host clamps to [1, 100]).</param>
    /// <param name="cancellationToken">Forwarded to the HTTP call.</param>
    [Get("/api/v1/compute/clusters")]
    public Task<HostClusterPage> ListClustersAsync(
        [AliasAs("page")] int page,
        [AliasAs("pageSize")] int pageSize,
        CancellationToken cancellationToken);

    /// <summary>
    ///     <c>GET /api/v1/compute/clusters/{clusterId}</c> —
    ///     single-cluster detail with embedded child nodes.
    /// </summary>
    /// <param name="clusterId">Target cluster id (wire string).</param>
    /// <param name="cancellationToken">Forwarded to the HTTP call.</param>
    [Get("/api/v1/compute/clusters/{clusterId}")]
    public Task<HostClusterDetail> GetClusterAsync(
        string clusterId,
        CancellationToken cancellationToken);

    /// <summary>
    ///     <c>POST /api/v1/compute/clusters</c> — provision a new
    ///     cluster and receive its first join token (sensitive;
    ///     shown once).
    /// </summary>
    /// <param name="request">Create payload.</param>
    /// <param name="cancellationToken">Forwarded to the HTTP call.</param>
    [Post("/api/v1/compute/clusters")]
    public Task<HostJoinTokenResult> CreateClusterAsync(
        [Body] HostCreateClusterRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    ///     <c>DELETE /api/v1/compute/clusters/{clusterId}</c> —
    ///     soft-delete a cluster (cascades node status to Gone).
    ///     The v0.1 controller is wired but the route is guarded by
    ///     <c>[RequirePermission(ClusterPermissions.Delete)]</c>; a
    ///     403 surfaces a "missing permission" error to the operator.
    /// </summary>
    /// <param name="clusterId">Target cluster id (wire string).</param>
    /// <param name="cancellationToken">Forwarded to the HTTP call.</param>
    [Delete("/api/v1/compute/clusters/{clusterId}")]
    public Task DeleteClusterAsync(
        string clusterId,
        CancellationToken cancellationToken);

    /// <summary>
    ///     <c>POST /api/v1/compute/clusters/{clusterId}/rotate-join-token</c>
    ///     — revoke the existing token, mint a new one (7-day TTL).
    ///     Old token is invalidated immediately on success.
    /// </summary>
    /// <param name="clusterId">Target cluster id (wire string).</param>
    /// <param name="cancellationToken">Forwarded to the HTTP call.</param>
    [Post("/api/v1/compute/clusters/{clusterId}/rotate-join-token")]
    public Task<HostJoinTokenResult> RotateClusterJoinTokenAsync(
        string clusterId,
        CancellationToken cancellationToken);

    /// <summary>
    ///     <c>GET /api/v1/compute/clusters/{clusterId}/workloads</c>
    ///     — list workloads in the cluster, paged via the host's
    ///     <c>FilterQuery</c> envelope. <paramref name="page" /> and
    ///     <paramref name="pageSize" /> map to the
    ///     <c>?page=&amp;pageSize=</c> query parameters; the host
    ///     clamps <c>pageSize</c> to [1, 100]. No filter / sort
    ///     query params are exposed by the CLI in v0.1 (operators
    ///     who need them use the dashboard's filter DSL).
    /// </summary>
    /// <param name="clusterId">Parent cluster id (wire string).</param>
    /// <param name="page">1-based page index (mirrors <c>FilterQuery.Page</c>).</param>
    /// <param name="pageSize">Items per page (host clamps to [1, 100]).</param>
    /// <param name="cancellationToken">Forwarded to the HTTP call.</param>
    [Get("/api/v1/compute/clusters/{clusterId}/workloads")]
    public Task<HostWorkloadPage> ListWorkloadsAsync(
        string clusterId,
        [AliasAs("page")] int page,
        [AliasAs("pageSize")] int pageSize,
        CancellationToken cancellationToken);

    /// <summary>
    ///     <c>POST /api/v1/compute/clusters/{clusterId}/workloads</c>
    ///     — provision a new workload (VM / LXC / container / etc.)
    ///     in the cluster. The control plane persists the row; the
    ///     NodeAgent's drift-detection picks it up on its next poll
    ///     and reconciles with the local runtime.
    /// </summary>
    /// <param name="clusterId">Parent cluster id (wire string).</param>
    /// <param name="request">Workload spec (Name / Kind / SpecJson).</param>
    /// <param name="cancellationToken">Forwarded to the HTTP call.</param>
    [Post("/api/v1/compute/clusters/{clusterId}/workloads")]
    public Task<HostWorkloadSummary> CreateWorkloadAsync(
        string clusterId,
        [Body] HostCreateWorkloadRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    ///     <c>POST .../workloads/{workloadId}/actions/start</c> —
    ///     start a previously provisioned workload. The control
    ///     plane enqueues a <c>workload.start</c> command on the
    ///     assigned node's queue and waits for the agent's
    ///     acknowledgement (typically &lt; 10 s; capped at 30 s).
    /// </summary>
    /// <param name="clusterId">Parent cluster id (wire string).</param>
    /// <param name="workloadId">Target workload id (wire string).</param>
    /// <param name="cancellationToken">Forwarded to the HTTP call.</param>
    [Post("/api/v1/compute/clusters/{clusterId}/workloads/{workloadId}/actions/start")]
    public Task<HostWorkloadActionResult> StartWorkloadAsync(
        string clusterId,
        string workloadId,
        CancellationToken cancellationToken);

    /// <summary>
    ///     <c>POST .../workloads/{workloadId}/actions/stop</c> —
    ///     gracefully shut down a running workload. Resources stay
    ///     allocated (the workload is stopped, not deleted).
    /// </summary>
    /// <param name="clusterId">Parent cluster id (wire string).</param>
    /// <param name="workloadId">Target workload id (wire string).</param>
    /// <param name="cancellationToken">Forwarded to the HTTP call.</param>
    [Post("/api/v1/compute/clusters/{clusterId}/workloads/{workloadId}/actions/stop")]
    public Task<HostWorkloadActionResult> StopWorkloadAsync(
        string clusterId,
        string workloadId,
        CancellationToken cancellationToken);

    /// <summary>
    ///     <c>DELETE .../workloads/{workloadId}</c> — soft-delete
    ///     a workload. The NodeAgent's next drift poll tears down
    ///     the local runtime handle; the control-plane row stays
    ///     in <c>forge.workloads</c> for audit + FK integrity.
    /// </summary>
    /// <param name="clusterId">Parent cluster id (wire string).</param>
    /// <param name="workloadId">Target workload id (wire string).</param>
    /// <param name="cancellationToken">Forwarded to the HTTP call.</param>
    [Delete("/api/v1/compute/clusters/{clusterId}/workloads/{workloadId}")]
    public Task DeleteWorkloadAsync(
        string clusterId,
        string workloadId,
        CancellationToken cancellationToken);
}