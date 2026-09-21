// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VSphereInventoryRefreshEndpoint — POST /api/v1/vsphere/inventory/refresh
// — forces a fresh inventory pull from vCenter. Triggers a
// background refresh through the scoped refresher service. Returns
// 202 + the new snapshot id so the caller can poll the read
// endpoint to confirm the cache settled.
// ============================================================================

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Plexor.Providers.VSphere.Infrastructure.Inventory;
using Plexor.Shared.Contracts.Routes;

namespace Plexor.Providers.VSphere.Api.Endpoints;

/// <summary>
///     Minimal-API endpoint that forces a vSphere inventory refresh.
///     Synchronous wait — the read endpoint stays consistent with the
///     refresh call without async-job tracking (out of scope for v1).
/// </summary>
file static class VSphereInventoryRefreshConstants
{
    /// <summary>vCenter identifier recorded on the snapshot header.
    /// v1 assumes a single vCenter per install; <c>"primary"</c> is
    /// the v1 marker. A future multi-vCenter deploy will derive
    /// this from the request.</summary>
    public const string PrimaryVCenterMoref = "primary";
}

file static class VSphereInventoryRefreshRoute
{
    public const string Name = "vsphere-inventory-refresh";
    public const string Path = ApiRoutes.Base + "/vsphere/inventory/refresh";
    public const string SnapshotLocationPath = ApiRoutes.Base + "/vsphere/inventory";
}

/// <summary>
///     Minimal-API endpoint that forces a vSphere inventory refresh.
///     Synchronous wait — the read endpoint stays consistent with the
///     refresh call without async-job tracking (out of scope for v1).
/// </summary>
public static class VSphereInventoryRefreshEndpoint
{

    /// <summary>Map the refresh endpoint.</summary>
    /// <param name="app">The host's endpoint route builder.</param>
    /// <returns>The same <paramref name="app" />, for chaining.</returns>
    public static IEndpointRouteBuilder MapVSphereInventoryRefresh(
        this IEndpointRouteBuilder app)
    {
        app.MapPost(VSphereInventoryRefreshRoute.Path, HandleAsync)
            .WithName(VSphereInventoryRefreshRoute.Name)
            .WithTags("vsphere");
        return app;
    }

    /// <summary>
    ///     Trigger a fresh inventory pull and return the new
    ///     snapshot id. Errors from vCenter propagate as 502 (the
    ///     upstream is unreachable) — see problem-details.md +
    ///     error-mapping.md.
    /// </summary>
    internal static async Task<IResult> HandleAsync(
        VSphereInventoryRefresher refresher,
        IOptions<VSphereOptions> options,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (!options.Value.IsConfigured())
        {
            return TypedResults.Problem(
                detail: "Set PLX_PROVIDERS_VSPHERE_VCENTERURL + PLX_PROVIDERS_VSPHERE_USERNAME + PLX_PROVIDERS_VSPHERE_PASSWORD to enable the vSphere provider.",
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "vSphere is not configured");
        }

        var logger = loggerFactory.CreateLogger("Plexor.Providers.VSphere.InventoryRefresh");

        var snapshotId = await refresher.RefreshAsync(
            VSphereInventoryRefreshConstants.PrimaryVCenterMoref,
            cancellationToken);

        logger.LogInformation(
            "vSphere inventory refresh succeeded: snapshot {SnapshotId}",
            snapshotId);

        return Results.Accepted(
            uri: VSphereInventoryRefreshRoute.SnapshotLocationPath + $"?snapshot={snapshotId}",
            value: new
            {
                snapshot_id = snapshotId,
                status = "SUCCESS",
            });
    }
}
