// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ListVolumesEndpoint — GET /api/v1/storage/volumes. Tenant-scoped
// (always filters by currentUser.TenantId); ordering is CreatedAt
// DESC (newest first).
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
///     Minimal-API endpoint that surfaces the tenant-scoped volume list
///     to admin UI hooks. Read-only; the create path lives in
///     <see cref="CreateVolumeEndpoint" />.
/// </summary>
/// <remarks>
///     <para><b>Tenant scoping.</b> The endpoint always forces
///     <c>org_id = currentUser.TenantId</c>. A caller in Org X
///     cannot enumerate Org Y's volumes.</para>
///     <para><b>Projection.</b> Returns the compact
///     <see cref="VolumeSummary" /> shape (id + name + size + status)
///     — the admin table view doesn't need the full provenance.</para>
///     <para><b>Authorisation via RequireAuthorization.</b>
///     Minimal APIs can't use the <c>[RequirePermission]</c> attribute
///     directly (the attribute is an MVC <c>IAuthorizeData</c>
///     contract) so we resolve the permission policy name through
///     <see cref="AuthorizationPolicyNames.For(string[])" /> (the same
///     encoder <c>RequirePermissionAttribute</c> uses) and hand it to
///     <c>RequireAuthorization()</c>.</para>
/// </remarks>
public static class ListVolumesEndpoint
{
    /// <summary>Stable route name referenced by OpenAPI + the
    /// controller group registration.</summary>
    public const string RouteName = "storage-volumes-list";

    /// <summary>
    ///     Register <c>GET /api/v1/storage/volumes</c>. Returns the
    ///     same <paramref name="builder" /> for chaining.
    /// </summary>
    /// <param name="builder">The host's endpoint route builder.</param>
    public static IEndpointRouteBuilder MapListVolumesEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapGet(VolumeEndpoints.GroupRoute, HandleAsync)
            .WithName(RouteName)
            .WithTags("storage")
            .RequireAuthorization(AuthorizationPolicyNames.For(StoragePermissions.Read));
        return builder;
    }

    [EndpointSummary("List volumes for the caller's tenant")]
    [ProducesResponseType<IReadOnlyList<VolumeSummary>>(StatusCodes.Status200OK)]
    internal static async Task<IResult> HandleAsync(
        IVolumeService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var rows = await service.ListAsync(currentUser.TenantId, cancellationToken);
        var summaries = rows
            .Select(static volume => new VolumeSummary
            {
                Id = volume.Id,
                Name = volume.Name,
                SizeGb = volume.SizeGb,
                Status = volume.Status.ToString(),
            })
            .ToArray();
        return Results.Ok(summaries);
    }
}

