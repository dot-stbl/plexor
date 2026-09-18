// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// DeleteFloatingIpEndpoint — DELETE /api/v1/network/floating-ips/{id}.
// Tenant-scoped idempotent delete; returns 204 on success, 404 when
// no row matched (id + org).
// ============================================================================

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Plexor.Modules.Network.Application.FloatingIps;
using Plexor.Shared.Authorization;
using Plexor.Shared.Kernel.Identity;
using Plexor.Shared.Kernel.Network;

namespace Plexor.Modules.Network.Api.Endpoints;

/// <summary>
///     Minimal-API endpoint that deletes a floating IP by id, scoped
///     to the caller's tenant.
/// </summary>
public static class DeleteFloatingIpEndpoint
{
    /// <summary>
    ///     Register <c>DELETE /api/v1/network/floating-ips/{floatingIpId}</c>.
    ///     Returns the same <paramref name="builder" /> for chaining.
    /// </summary>
    public static IEndpointRouteBuilder MapDeleteFloatingIpEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapDelete(FloatingIpEndpoints.GroupRoute + "/{floatingIpId:guid}", HandleAsync)
            .WithName(FloatingIpEndpoints.RouteNames.Delete)
            .WithTags("network")
            .RequireAuthorization(AuthorizationPolicyNames.For(NetworkPermissions.Write));
        return builder;
    }

    [EndpointSummary("Delete a floating IP by id")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    internal static async Task<IResult> HandleAsync(
        Guid floatingIpId,
        IFloatingIpService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var deleted = await service.DeleteAsync(floatingIpId, currentUser.TenantId, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    }
}
