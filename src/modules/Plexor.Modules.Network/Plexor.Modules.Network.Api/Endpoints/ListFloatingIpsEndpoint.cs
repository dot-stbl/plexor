// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ListFloatingIpsEndpoint — GET /api/v1/network/floating-ips.
// Tenant-scoped; ordering is CreatedAt DESC.
// ============================================================================

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Plexor.Modules.Network.Api.Models.Responses;
using Plexor.Modules.Network.Application.FloatingIps;
using Plexor.Shared.Authorization;
using Plexor.Shared.Kernel.Identity;
using Plexor.Shared.Kernel.Network;

namespace Plexor.Modules.Network.Api.Endpoints;

/// <summary>
///     Minimal-API endpoint that surfaces the tenant-scoped
///     floating-IP list to admin UI hooks.
/// </summary>
public static class ListFloatingIpsEndpoint
{
    /// <summary>Stable route name referenced by OpenAPI + the
    /// controller group registration.</summary>
    public const string RouteName = "network-floating-ips-list";

    /// <summary>
    ///     Register <c>GET /api/v1/network/floating-ips</c>. Returns
    ///     the same <paramref name="builder" /> for chaining.
    /// </summary>
    public static IEndpointRouteBuilder MapListFloatingIpsEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapGet(FloatingIpEndpoints.GroupRoute, HandleAsync)
            .WithName(RouteName)
            .WithTags("network")
            .RequireAuthorization(AuthorizationPolicyNames.For(NetworkPermissions.Read));
        return builder;
    }

    [EndpointSummary("List floating IPs for the caller's tenant")]
    [ProducesResponseType<IReadOnlyList<FloatingIpSummary>>(StatusCodes.Status200OK)]
    internal static async Task<IResult> HandleAsync(
        IFloatingIpService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var rows = await service.ListAsync(currentUser.TenantId, cancellationToken);
        var summaries = rows
            .Select(static ip => new FloatingIpSummary
            {
                Id = ip.Id,
                Address = ip.Address,
                Status = ip.Status.ToString(),
            })
            .ToArray();
        return Results.Ok(summaries);
    }
}
