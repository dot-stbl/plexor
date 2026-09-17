// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ListBucketsEndpoint — GET /api/v1/storage/buckets. Tenant-scoped;
// ordering is CreatedAt DESC (newest first). Mirrors ListVolumesEndpoint.
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
///     Minimal-API endpoint that surfaces the tenant-scoped bucket
///     list to admin UI hooks.
/// </summary>
public static class ListBucketsEndpoint
{
    /// <summary>Stable route name referenced by OpenAPI + the
    /// controller group registration.</summary>
    public const string RouteName = "storage-buckets-list";

    /// <summary>
    ///     Register <c>GET /api/v1/storage/buckets</c>. Returns the
    ///     same <paramref name="builder" /> for chaining.
    /// </summary>
    public static IEndpointRouteBuilder MapListBucketsEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapGet(BucketEndpoints.GroupRoute, HandleAsync)
            .WithName(RouteName)
            .WithTags("storage")
            .RequireAuthorization(AuthorizationPolicyNames.For(StoragePermissions.Read));
        return builder;
    }

    [EndpointSummary("List buckets for the caller's tenant")]
    [ProducesResponseType<IReadOnlyList<BucketSummary>>(StatusCodes.Status200OK)]
    internal static async Task<IResult> HandleAsync(
        IBucketService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var rows = await service.ListAsync(currentUser.TenantId, cancellationToken);
        var summaries = rows
            .Select(static bucket => new BucketSummary
            {
                Id = bucket.Id,
                Name = bucket.Name,
                Region = bucket.Region,
                SizeBytes = bucket.SizeBytes,
                ObjectCount = bucket.ObjectCount,
            })
            .ToArray();
        return Results.Ok(summaries);
    }
}

