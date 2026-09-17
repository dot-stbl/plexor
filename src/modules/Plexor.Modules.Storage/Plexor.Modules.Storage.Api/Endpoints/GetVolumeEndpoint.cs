// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// GetVolumeEndpoint — GET /api/v1/storage/volumes/{volumeId}. Tenant
// scoped; returns 404 (not 403) when the volume doesn't exist in
// the caller's tenant — leaking 403 would disclose the existence of
// a row in a different org.
// ============================================================================

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Plexor.Modules.Storage.Api.Models.Responses;
using Plexor.Modules.Storage.Application.Volumes;
using Plexor.Shared.Authorization;
using Plexor.Shared.Kernel.Identity;
using Plexor.Shared.Kernel.Storage;

namespace Plexor.Modules.Storage.Api.Endpoints;

/// <summary>
///     Minimal-API endpoint that returns a single volume by id, scoped
///     to the caller's tenant.
/// </summary>
/// <remarks>
///     <para><b>404 vs 403.</b> The endpoint passes
///     <c>orgId = currentUser.TenantId</c> as the org filter on the
///     read query. A caller from Org X asking for a volume id that
///     belongs to Org Y gets null back from the service (the
///     (id, org_id) predicate never matches) and the endpoint returns
///     404 — not 403. Returning 403 would leak the existence of the
///     row to the caller.</para>
/// </remarks>
public static class GetVolumeEndpoint
{
    /// <summary>
    ///     Register <c>GET /api/v1/storage/volumes/{volumeId}</c>.
    ///     Returns the same <paramref name="builder" /> for chaining.
    /// </summary>
    public static IEndpointRouteBuilder MapGetVolumeEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapGet(VolumeEndpoints.GroupRoute + "/{volumeId:guid}", HandleAsync)
            .WithName(VolumeEndpoints.RouteNames.Get)
            .WithTags("storage")
            .RequireAuthorization(AuthorizationPolicyNames.For(StoragePermissions.Read));
        return builder;
    }

    [EndpointSummary("Get a volume by id")]
    [ProducesResponseType<VolumeDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    internal static async Task<IResult> HandleAsync(
        Guid volumeId,
        IVolumeService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var row = await service.GetAsync(volumeId, currentUser.TenantId, cancellationToken);
        if (row is null)
        {
            return Results.NotFound();
        }

        var detail = new VolumeDetail
        {
            Id = row.Id,
            OrgId = row.OrgId,
            ClusterId = row.ClusterId,
            Name = row.Name,
            SizeGb = row.SizeGb,
            Status = row.Status.ToString(),
            CreatedAt = row.CreatedAt,
            UpdatedAt = row.UpdatedAt,
        };
        return Results.Ok(detail);
    }
}

