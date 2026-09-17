// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// CreateBucketEndpoint — POST /api/v1/storage/buckets. Validates the
// request body (S3-style bucket name + region) then calls
// IBucketService.CreateAsync.
// ============================================================================

using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Plexor.Modules.Storage.Api.Models.Requests;
using Plexor.Modules.Storage.Api.Models.Responses;
using Plexor.Modules.Storage.Application.Buckets;
using Plexor.Shared.Authorization;
using Plexor.Shared.Kernel.Identity;
using Plexor.Shared.Kernel.Storage;

namespace Plexor.Modules.Storage.Api.Endpoints;

/// <summary>
///     Minimal-API endpoint that creates a new bucket row in the
///     caller's tenant scope.
/// </summary>
/// <remarks>
///     <para><b>Validation.</b> The
///     <see cref="Plexor.Modules.Storage.Api.Validation.CreateBucketRequestValidator" />
///     runs first; failures surface as 400 ValidationProblemDetails.
///     A 201 Created response carries the <see cref="BucketSummary" />
///     projection + a <c>Location</c> header pointing at the new
///     resource.</para>
///     <para><b>Quotas.</b> Buckets are NOT quota-affecting in v0.1
///     — the catalog seeds a <c>storage.volumes.count</c> +
///     <c>storage.volumes.gb</c> pair but no <c>storage.buckets.count</c>.
///     A future addition would extend the catalog + reader.</para>
/// </remarks>
public static class CreateBucketEndpoint
{
    /// <summary>
    ///     Register <c>POST /api/v1/storage/buckets</c>. Returns the
    ///     same <paramref name="builder" /> for chaining.
    /// </summary>
    public static IEndpointRouteBuilder MapCreateBucketEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapPost(BucketEndpoints.GroupRoute, HandleAsync)
            .WithName(BucketEndpoints.RouteNames.Create)
            .WithTags("storage")
            .RequireAuthorization(AuthorizationPolicyNames.For(StoragePermissions.Write));
        return builder;
    }

    [EndpointSummary("Create a bucket in the caller's tenant")]
    [ProducesResponseType<BucketSummary>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    internal static async Task<IResult> HandleAsync(
        CreateBucketRequest request,
        IBucketService service,
        ICurrentUser currentUser,
        IValidator<CreateBucketRequest> validator,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            var problem = new ValidationProblemDetails(validation.ToDictionary())
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed",
                Detail = "One or more request fields failed validation.",
            };
            return Results.BadRequest(problem);
        }

        var created = await service.CreateAsync(
            new NewBucketInput(
                OrgId: currentUser.TenantId,
                Name: request.Name,
                Region: request.Region),
            cancellationToken);

        var summary = new BucketSummary
        {
            Id = created.Id,
            Name = created.Name,
            Region = created.Region,
            SizeBytes = created.SizeBytes,
            ObjectCount = created.ObjectCount,
        };

        return Results.Created($"/api/v1/storage/buckets/{created.Id}", summary);
    }
}
