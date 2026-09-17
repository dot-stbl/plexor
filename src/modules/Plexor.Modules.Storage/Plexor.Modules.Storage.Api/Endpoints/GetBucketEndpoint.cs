// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// GetBucketEndpoint — GET /api/v1/storage/buckets/{bucketId}. Tenant
// scoped; returns 404 (not 403) on cross-tenant — see the same
// rationale in GetVolumeEndpoint.
// ============================================================================

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Plexor.Modules.Storage.Api.Models.Responses;
using Plexor.Modules.Storage.Application.Buckets;
using Plexor.Shared.Authorization;
using Plexor.Shared.Kernel.Identity;
using Plexor.Shared.Kernel.Storage;

namespace Plexor.Modules.Storage.Api.Endpoints;

/// <summary>
///     Minimal-API endpoint that returns a single bucket by id, scoped
///     to the caller's tenant.
/// </summary>
public static class GetBucketEndpoint
{
    /// <summary>
    ///     Register <c>GET /api/v1/storage/buckets/{bucketId}</c>.
    ///     Returns the same <paramref name="builder" /> for chaining.
    /// </summary>
    public static IEndpointRouteBuilder MapGetBucketEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapGet(BucketEndpoints.GroupRoute + "/{bucketId:guid}", HandleAsync)
            .WithName(BucketEndpoints.RouteNames.Get)
            .WithTags("storage")
            .RequireAuthorization(AuthorizationPolicyNames.For(StoragePermissions.Read));
        return builder;
    }

    [EndpointSummary("Get a bucket by id")]
    [ProducesResponseType<BucketSummary>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    internal static async Task<IResult> HandleAsync(
        Guid bucketId,
        IBucketService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var row = await service.GetAsync(bucketId, currentUser.TenantId, cancellationToken);
        if (row is null)
        {
            return Results.NotFound();
        }

        var summary = new BucketSummary
        {
            Id = row.Id,
            Name = row.Name,
            Region = row.Region,
            SizeBytes = row.SizeBytes,
            ObjectCount = row.ObjectCount,
        };
        return Results.Ok(summary);
    }
}

