// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// CreateFloatingIpEndpoint — POST /api/v1/network/floating-ips.
// ============================================================================

using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Plexor.Modules.Network.Api.Models.Requests;
using Plexor.Modules.Network.Api.Models.Responses;
using Plexor.Modules.Network.Application.FloatingIps;
using Plexor.Shared.Authorization;
using Plexor.Shared.Kernel.Identity;
using Plexor.Shared.Kernel.Network;

namespace Plexor.Modules.Network.Api.Endpoints;

/// <summary>
///     Minimal-API endpoint that creates a new floating IP row in the
///     caller's tenant scope.
/// </summary>
/// <remarks>
///     <para><b>Quota enforcement (4.5.d follow-up).</b> v0.1 does
///     NOT call <c>IQuotaEnforcer.CheckAndReserveAsync</c> on the
///     create path; the quota key wiring in commit 3 closes the gap
///     at the read boundary. A follow-up commit adds the create-path
///     enforcer check.</para>
/// </remarks>
public static class CreateFloatingIpEndpoint
{
    /// <summary>
    ///     Register <c>POST /api/v1/network/floating-ips</c>. Returns
    ///     the same <paramref name="builder" /> for chaining.
    /// </summary>
    public static IEndpointRouteBuilder MapCreateFloatingIpEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapPost(FloatingIpEndpoints.GroupRoute, HandleAsync)
            .WithName(FloatingIpEndpoints.RouteNames.Create)
            .WithTags("network")
            .RequireAuthorization(AuthorizationPolicyNames.For(NetworkPermissions.Write));
        return builder;
    }

    [EndpointSummary("Create a floating IP in the caller's tenant")]
    [ProducesResponseType<FloatingIpDetail>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    internal static async Task<IResult> HandleAsync(
        CreateFloatingIpRequest request,
        IFloatingIpService service,
        ICurrentUser currentUser,
        IValidator<CreateFloatingIpRequest> validator,
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
            new NewFloatingIpInput(
                OrgId: currentUser.TenantId,
                ClusterId: request.ClusterId,
                Address: request.Address),
            cancellationToken);

        var detail = new FloatingIpDetail
        {
            Id = created.Id,
            OrgId = created.OrgId,
            ClusterId = created.ClusterId,
            Address = created.Address,
            Status = created.Status.ToString(),
            CreatedAt = created.CreatedAt,
            UpdatedAt = created.UpdatedAt,
        };

        return Results.Created($"/api/v1/network/floating-ips/{created.Id}", detail);
    }
}
