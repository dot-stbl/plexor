// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// CreateVolumeEndpoint — POST /api/v1/storage/volumes. Validates the
// request body via FluentValidation (cluster id non-empty, name 1-128
// chars, size 1-65536 GiB), then calls IVolumeService.CreateAsync.
// ============================================================================

using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Plexor.Modules.Storage.Api.Models.Requests;
using Plexor.Modules.Storage.Api.Models.Responses;
using Plexor.Modules.Storage.Application.Volumes;
using Plexor.Shared.Authorization;
using Plexor.Shared.Kernel.Identity;
using Plexor.Shared.Kernel.Storage;

namespace Plexor.Modules.Storage.Api.Endpoints;

/// <summary>
///     Minimal-API endpoint that creates a new volume row in the
///     caller's tenant scope.
/// </summary>
/// <remarks>
///     <para><b>Validation.</b> The
///     <see cref="Plexor.Modules.Storage.Api.Validation.CreateVolumeRequestValidator" />
///     runs first; failures surface as 400 ValidationProblemDetails. A
///     201 Created response carries the full <see cref="VolumeDetail" />
///     body and a <c>Location</c> header pointing at the new resource.</para>
///     <para><b>Quota enforcement (4.5.d follow-up).</b> v0.1 does
///     NOT call <c>IQuotaEnforcer.CheckAndReserveAsync</c> on the
///     create path; the quota key wiring in commit 1 closes the gap
///     at the read / future-write boundary. A follow-up commit adds
///     the create-path enforcer check + the audited
///     <see cref="Plexor.Modules.Storage.Application.Volumes.IVolumeService" />
///     variant that runs inside the resource-create transaction.</para>
/// </remarks>
public static class CreateVolumeEndpoint
{
    /// <summary>
    ///     Register <c>POST /api/v1/storage/volumes</c>. Returns the
    ///     same <paramref name="builder" /> for chaining.
    /// </summary>
    public static IEndpointRouteBuilder MapCreateVolumeEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapPost(VolumeEndpoints.GroupRoute, HandleAsync)
            .WithName(VolumeEndpoints.RouteNames.Create)
            .WithTags("storage")
            .RequireAuthorization(AuthorizationPolicyNames.For(StoragePermissions.Write));
        return builder;
    }

    [EndpointSummary("Create a volume in the caller's tenant")]
    [ProducesResponseType<VolumeDetail>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    internal static async Task<IResult> HandleAsync(
        CreateVolumeRequest request,
        IVolumeService service,
        ICurrentUser currentUser,
        IValidator<CreateVolumeRequest> validator,
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
            new NewVolumeInput(
                OrgId: currentUser.TenantId,
                ClusterId: request.ClusterId,
                Name: request.Name,
                SizeGb: request.SizeGb),
            cancellationToken);

        var detail = new VolumeDetail
        {
            Id = created.Id,
            OrgId = created.OrgId,
            ClusterId = created.ClusterId,
            Name = created.Name,
            SizeGb = created.SizeGb,
            Status = created.Status.ToString(),
            CreatedAt = created.CreatedAt,
            UpdatedAt = created.UpdatedAt,
        };

        return Results.Created($"/api/v1/storage/volumes/{created.Id}", detail);
    }
}
