// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// GetLoadBalancerEndpoint — GET /api/v1/network/load-balancers/{id}.
// Tenant scoped; returns 404 (not 403) on cross-tenant.
// ============================================================================

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Plexor.Modules.Network.Api.Models.Responses;
using Plexor.Modules.Network.Application.LoadBalancers;
using Plexor.Shared.Authorization;
using Plexor.Shared.Kernel.Identity;
using Plexor.Shared.Kernel.Network;

namespace Plexor.Modules.Network.Api.Endpoints;

/// <summary>
///     Minimal-API endpoint that returns a single load balancer by
///     id, scoped to the caller's tenant.
/// </summary>
public static class GetLoadBalancerEndpoint
{
    /// <summary>
    ///     Register <c>GET /api/v1/network/load-balancers/{loadBalancerId}</c>.
    ///     Returns the same <paramref name="builder" /> for chaining.
    /// </summary>
    public static IEndpointRouteBuilder MapGetLoadBalancerEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapGet(LoadBalancerEndpoints.GroupRoute + "/{loadBalancerId:guid}", HandleAsync)
            .WithName(LoadBalancerEndpoints.RouteNames.Get)
            .WithTags("network")
            .RequireAuthorization(AuthorizationPolicyNames.For(NetworkPermissions.Read));
        return builder;
    }

    [EndpointSummary("Get a load balancer by id")]
    [ProducesResponseType<LoadBalancerSummary>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    internal static async Task<IResult> HandleAsync(
        Guid loadBalancerId,
        ILoadBalancerService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var row = await service.GetAsync(loadBalancerId, currentUser.TenantId, cancellationToken);
        if (row is null)
        {
            return Results.NotFound();
        }

        var summary = new LoadBalancerSummary
        {
            Id = row.Id,
            Name = row.Name,
            Type = row.Type.ToString(),
            Algorithm = row.Algorithm.ToString(),
            Status = row.Status.ToString(),
        };
        return Results.Ok(summary);
    }
}
