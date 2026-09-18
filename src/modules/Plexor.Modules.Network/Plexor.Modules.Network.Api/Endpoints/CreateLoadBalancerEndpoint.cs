// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// CreateLoadBalancerEndpoint — POST /api/v1/network/load-balancers.
// ============================================================================

using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Plexor.Modules.Network.Api.Models.Requests;
using Plexor.Modules.Network.Api.Models.Responses;
using Plexor.Modules.Network.Application.LoadBalancers;
using Plexor.Modules.Network.Domain.Entities;
using Plexor.Shared.Authorization;
using Plexor.Shared.Kernel.Identity;
using Plexor.Shared.Kernel.Network;

namespace Plexor.Modules.Network.Api.Endpoints;

/// <summary>
///     Minimal-API endpoint that creates a new load balancer row in
///     the caller's tenant scope.
/// </summary>
public static class CreateLoadBalancerEndpoint
{
    /// <summary>
    ///     Register <c>POST /api/v1/network/load-balancers</c>.
    ///     Returns the same <paramref name="builder" /> for chaining.
    /// </summary>
    public static IEndpointRouteBuilder MapCreateLoadBalancerEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapPost(LoadBalancerEndpoints.GroupRoute, HandleAsync)
            .WithName(LoadBalancerEndpoints.RouteNames.Create)
            .WithTags("network")
            .RequireAuthorization(AuthorizationPolicyNames.For(NetworkPermissions.Write));
        return builder;
    }

    [EndpointSummary("Create a load balancer in the caller's tenant")]
    [ProducesResponseType<LoadBalancerSummary>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    internal static async Task<IResult> HandleAsync(
        CreateLoadBalancerRequest request,
        ILoadBalancerService service,
        ICurrentUser currentUser,
        IValidator<CreateLoadBalancerRequest> validator,
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
            new NewLoadBalancerInput(
                OrgId: currentUser.TenantId,
                ClusterId: request.ClusterId,
                Name: request.Name,
                Type: Enum.Parse<LoadBalancerType>(request.Type),
                Algorithm: Enum.Parse<LoadBalancerAlgorithm>(request.Algorithm)),
            cancellationToken);

        var summary = new LoadBalancerSummary
        {
            Id = created.Id,
            Name = created.Name,
            Type = created.Type.ToString(),
            Algorithm = created.Algorithm.ToString(),
            Status = created.Status.ToString(),
        };

        return Results.Created($"/api/v1/network/load-balancers/{created.Id}", summary);
    }
}
