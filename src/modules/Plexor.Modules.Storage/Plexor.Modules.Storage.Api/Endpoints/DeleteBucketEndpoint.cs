// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// DeleteBucketEndpoint — DELETE /api/v1/storage/buckets/{bucketId}.
// Tenant-scoped idempotent delete; returns 204 on success, 404 when
// no row matched (id + org).
// ============================================================================

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Plexor.Modules.Storage.Application.Buckets;
using Plexor.Shared.Authorization;
using Plexor.Shared.Kernel.Identity;
using Plexor.Shared.Kernel.Storage;

namespace Plexor.Modules.Storage.Api.Endpoints;

/// <summary>
///     Minimal-API endpoint that deletes a bucket by id, scoped to
///     the caller's tenant. Idempotent — same shape as
///     <see cref="DeleteVolumeEndpoint" />.
/// </summary>
public static class DeleteBucketEndpoint
{
    /// <summary>
    ///     Register <c>DELETE /api/v1/storage/buckets/{bucketId}</c>.
    ///     Returns the same <paramref name="builder" /> for chaining.
    /// </summary>
    public static IEndpointRouteBuilder MapDeleteBucketEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapDelete(BucketEndpoints.GroupRoute + "/{bucketId:guid}", HandleAsync)
            .WithName(BucketEndpoints.RouteNames.Delete)
            .WithTags("storage")
            .RequireAuthorization(AuthorizationPolicyNames.For(StoragePermissions.Write));
        return builder;
    }

    [EndpointSummary("Delete a bucket by id")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    internal static async Task<IResult> HandleAsync(
        Guid bucketId,
        IBucketService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var deleted = await service.DeleteAsync(bucketId, currentUser.TenantId, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    }
}

