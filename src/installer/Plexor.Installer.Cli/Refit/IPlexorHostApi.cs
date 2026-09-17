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
}