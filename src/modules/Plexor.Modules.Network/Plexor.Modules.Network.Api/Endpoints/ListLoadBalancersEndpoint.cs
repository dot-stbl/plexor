// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ListLoadBalancersEndpoint — GET /api/v1/network/load-balancers.
// Tenant-scoped; ordering is CreatedAt DESC.
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
///     Minimal-API endpoint that surfaces the tenant-scoped
///     load-balancer list to admin UI hooks.
/// </summary>
public static class ListLoadBalancersEndpoint
{
    /// <summary>Stable route name referenced by OpenAPI + the
    /// controller group registration.</summary>
    public const string RouteName = "network-load-balancers-list";

    /// <summary>
    ///     Register <c>GET /api/v1/network/load-balancers</c>. Returns
    ///     the same <paramref name="builder" /> for chaining.
    /// </summary>
    public static IEndpointRouteBuilder MapListLoadBalancersEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapGet(LoadBalancerEndpoints.GroupRoute, HandleAsync)
            .WithName(RouteName)
            .WithTags("network")
            .RequireAuthorization(AuthorizationPolicyNames.For(NetworkPermissions.Read));
        return builder;
    }

    [EndpointSummary("List load balancers for the caller's tenant")]
    [ProducesResponseType<IReadOnlyList<LoadBalancerSummary>>(StatusCodes.Status200OK)]
    internal static async Task<IResult> HandleAsync(
        ILoadBalancerService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var rows = await service.ListAsync(currentUser.TenantId, cancellationToken);
        var summaries = rows
            .Select(static lb => new LoadBalancerSummary
            {
                Id = lb.Id,
                Name = lb.Name,
                Type = lb.Type.ToString(),
                Algorithm = lb.Algorithm.ToString(),
                Status = lb.Status.ToString(),
            })
            .ToArray();
        return Results.Ok(summaries);
    }
}
