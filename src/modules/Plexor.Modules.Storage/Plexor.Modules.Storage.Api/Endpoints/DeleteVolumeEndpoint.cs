// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// DeleteVolumeEndpoint — DELETE /api/v1/storage/volumes/{volumeId}.
// Tenant-scoped idempotent delete; returns 204 on success, 404 when
// no row matched (id + org).
// ============================================================================

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Plexor.Modules.Storage.Application.Volumes;
using Plexor.Shared.Authorization;
using Plexor.Shared.Kernel.Identity;
using Plexor.Shared.Kernel.Storage;

namespace Plexor.Modules.Storage.Api.Endpoints;

/// <summary>
///     Minimal-API endpoint that deletes a volume by id, scoped to
///     the caller's tenant.
/// </summary>
/// <remarks>
///     <para><b>Idempotent.</b> A missing row is a 404 (the caller
///     can re-issue the delete and see the same response), not a
///     204. Same status code regardless of whether the row existed
///     keeps the operator's automation simple — callers branch on
///     the response, not on whether the row was deleted by an
///     earlier request.</para>
///     <para><b>Quota impact.</b> Deleting a volume does NOT
///     decrement the <c>storage.volumes.gb</c> counter — the
///     counter tracks cumulative GiB at the org scope. The
///     physical-row COUNT(*) + SUM(size_gb) aggregate
///     (<see cref="Plexor.Modules.Storage.Application.Storage.IStorageQuotaReader" />)
///     shrinks on delete because the row is gone.</para>
/// </remarks>
public static class DeleteVolumeEndpoint
{
    /// <summary>
    ///     Register <c>DELETE /api/v1/storage/volumes/{volumeId}</c>.
    ///     Returns the same <paramref name="builder" /> for chaining.
    /// </summary>
    public static IEndpointRouteBuilder MapDeleteVolumeEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapDelete(VolumeEndpoints.GroupRoute + "/{volumeId:guid}", HandleAsync)
            .WithName(VolumeEndpoints.RouteNames.Delete)
            .WithTags("storage")
            .RequireAuthorization(AuthorizationPolicyNames.For(StoragePermissions.Write));
        return builder;
    }

    [EndpointSummary("Delete a volume by id")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    internal static async Task<IResult> HandleAsync(
        Guid volumeId,
        IVolumeService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var deleted = await service.DeleteAsync(volumeId, currentUser.TenantId, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    }
}

