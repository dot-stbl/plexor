// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// HostClusterContractsExtended — additional wire shapes for the
// `plx cluster *` commands that don't fit into HostClusterContracts
// (the cluster-list / detail / token-summary read-side shapes).
//
// Split per folder-organization.md §1 (one type per file when types
// are unrelated). Create payload + paged envelope live here.
// ============================================================================

namespace Plexor.Installer.Cli.Refit;

/// <summary>
///     Wire shape for <c>POST /api/v1/compute/clusters</c>. Mirrors
///     <c>Plexor.Modules.Clusters.Api.Models.CreateClusterRequest</c>.
///     <see cref="Region" /> is left empty when the operator passes
///     no <c>--region</c> — the host's controller treats empty
///     region as "default".
/// </summary>
/// <param name="Name">Cluster name (unique per org, 1–128 chars).</param>
/// <param name="Region">Operator-assigned region (e.g. <c>eu-central-1</c>).</param>
/// <param name="InitialNodeRole">
///     Role the first joining node will take — <c>"Control"</c> or
///     <c>"Compute"</c> (lowercase accepted; default <c>Compute</c>).
/// </param>
public sealed record HostCreateClusterRequest(
    string Name,
    string Region,
    HostNodeRole InitialNodeRole);

/// <summary>
///     Wire shape for the <c>GET /api/v1/compute/clusters</c>
///     response envelope. Mirrors
///     <c>Plexor.Shared.Contracts.Pagination.PageResult&lt;T&gt;</c>
///     but kept local so the installer doesn't depend on the
///     Pagination contract assembly (consistent with the rest of
///     the CLI's Refit surface — see HostNodeResponse.cs).
/// </summary>
/// <param name="Items">The page slice — never null, may be empty.</param>
/// <param name="Total">Total matching rows across all pages.</param>
/// <param name="Page">The page number returned (1-based).</param>
/// <param name="PageSize">The page size used.</param>
public sealed record HostClusterPage(
    IReadOnlyList<HostClusterSummary> Items,
    int Total,
    int Page,
    int PageSize);