// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// DeleteLoadBalancerEndpoint — DELETE /api/v1/network/load-balancers/{id}.
// Tenant-scoped idempotent delete; returns 204 on success, 404 when
// no row matched (id + org).
// ============================================================================

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Plexor.Modules.Network.Application.LoadBalancers;
using Plexor.Shared.Authorization;
using Plexor.Shared.Kernel.Identity;
using Plexor.Shared.Kernel.Network;

namespace Plexor.Modules.Network.Api.Endpoints;

/// <summary>
///     Minimal-API endpoint that deletes a load balancer by id,
///     scoped to the caller's tenant.
/// </summary>
public static class DeleteLoadBalancerEndpoint
{
    /// <summary>
    ///     Register <c>DELETE /api/v1/network/load-balancers/{loadBalancerId}</c>.
    ///     Returns the same <paramref name="builder" /> for chaining.
    /// </summary>
    public static IEndpointRouteBuilder MapDeleteLoadBalancerEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapDelete(LoadBalancerEndpoints.GroupRoute + "/{loadBalancerId:guid}", HandleAsync)
            .WithName(LoadBalancerEndpoints.RouteNames.Delete)
            .WithTags("network")
            .RequireAuthorization(AuthorizationPolicyNames.For(NetworkPermissions.Write));
        return builder;
    }

    [EndpointSummary("Delete a load balancer by id")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    internal static async Task<IResult> HandleAsync(
        Guid loadBalancerId,
        ILoadBalancerService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var deleted = await service.DeleteAsync(loadBalancerId, currentUser.TenantId, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    }
}
