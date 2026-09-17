// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// GetFloatingIpEndpoint — GET /api/v1/network/floating-ips/{id}. Tenant
// scoped; returns 404 (not 403) on cross-tenant.
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
///     Minimal-API endpoint that returns a single floating IP by id,
///     scoped to the caller's tenant.
/// </summary>
public static class GetFloatingIpEndpoint
{
    /// <summary>
    ///     Register <c>GET /api/v1/network/floating-ips/{floatingIpId}</c>.
    ///     Returns the same <paramref name="builder" /> for chaining.
    /// </summary>
    public static IEndpointRouteBuilder MapGetFloatingIpEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapGet(FloatingIpEndpoints.GroupRoute + "/{floatingIpId:guid}", HandleAsync)
            .WithName(FloatingIpEndpoints.RouteNames.Get)
            .WithTags("network")
            .RequireAuthorization(AuthorizationPolicyNames.For(NetworkPermissions.Read));
        return builder;
    }

    [EndpointSummary("Get a floating IP by id")]
    [ProducesResponseType<FloatingIpDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    internal static async Task<IResult> HandleAsync(
        Guid floatingIpId,
        IFloatingIpService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var row = await service.GetAsync(floatingIpId, currentUser.TenantId, cancellationToken);
        if (row is null)
        {
            return Results.NotFound();
        }

        var detail = new FloatingIpDetail
        {
            Id = row.Id,
            OrgId = row.OrgId,
            ClusterId = row.ClusterId,
            Address = row.Address,
            Status = row.Status.ToString(),
            CreatedAt = row.CreatedAt,
            UpdatedAt = row.UpdatedAt,
        };
        return Results.Ok(detail);
    }
}
