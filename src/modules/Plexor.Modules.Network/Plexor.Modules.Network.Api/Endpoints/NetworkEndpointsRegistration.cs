// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NetworkEndpointsRegistration — single registration entry for every
// Network minimal-API endpoint. Mirrors Plexor.Modules.Storage.Api.Endpoints
// .StorageEndpoints — one public static class with extension methods
// that Program.cs chains.
//
// Usage from Plexor.Host/Program.cs:
//
//     app.MapNetworkEndpoints();
// ============================================================================

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Plexor.Modules.Network.Api.Endpoints;

/// <summary>
///     Top-level registration extension for every Network endpoint.
///     Maps the four floating-IP endpoints (list / get / create /
///     delete) and the four load-balancer endpoints (list / get /
///     create / delete) under
///     <c>/api/v1/network/floating-ips</c> and
///     <c>/api/v1/network/load-balancers</c> respectively.
/// </summary>
public static class NetworkEndpointsRegistration
{
    /// <summary>
    ///     Register every Network endpoint. Returns the same
    ///     <paramref name="builder" /> for chaining.
    /// </summary>
    public static IEndpointRouteBuilder MapNetworkEndpoints(this IEndpointRouteBuilder builder)
    {
        builder.MapListFloatingIpsEndpoint();
        builder.MapCreateFloatingIpEndpoint();
        builder.MapGetFloatingIpEndpoint();
        builder.MapDeleteFloatingIpEndpoint();

        builder.MapListLoadBalancersEndpoint();
        builder.MapCreateLoadBalancerEndpoint();
        builder.MapGetLoadBalancerEndpoint();
        builder.MapDeleteLoadBalancerEndpoint();

        return builder;
    }
}
