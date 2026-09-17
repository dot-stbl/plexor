// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NetworkEndpoints — central route constants for the floating-IP +
// load-balancer resource groups. The four endpoint files
// (ListFloatingIpsEndpoint / GetFloatingIpEndpoint / CreateFloatingIpEndpoint
// / DeleteFloatingIpEndpoint + the LB equivalents) all reference the
// constants here so the URL composition + route names live in one
// place.
// ============================================================================

using Plexor.Shared.Contracts.Routes;

namespace Plexor.Modules.Network.Api.Endpoints;

/// <summary>
///     Constants shared across the floating-IP endpoint family.
/// </summary>
public static class FloatingIpEndpoints
{
    /// <summary>Base URL for the floating-IP resource group.</summary>
    public const string GroupRoute = ApiRoutes.Base + "/network/floating-ips";

    /// <summary>Stable route names referenced by OpenAPI +
    /// each handler's <c>WithName(...)</c> call.</summary>
    public static class RouteNames
    {
        /// <summary><c>GET /api/v1/network/floating-ips</c>.</summary>
        public const string List = "network-floating-ips-list";

        /// <summary><c>GET /api/v1/network/floating-ips/{floatingIpId}</c>.</summary>
        public const string Get = "network-floating-ips-get";

        /// <summary><c>POST /api/v1/network/floating-ips</c>.</summary>
        public const string Create = "network-floating-ips-create";

        /// <summary><c>DELETE /api/v1/network/floating-ips/{floatingIpId}</c>.</summary>
        public const string Delete = "network-floating-ips-delete";
    }
}

/// <summary>
///     Constants shared across the load-balancer endpoint family.
/// </summary>
public static class LoadBalancerEndpoints
{
    /// <summary>Base URL for the load-balancer resource group.</summary>
    public const string GroupRoute = ApiRoutes.Base + "/network/load-balancers";

    /// <summary>Stable route names referenced by OpenAPI +
    /// each handler's <c>WithName(...)</c> call.</summary>
    public static class RouteNames
    {
        /// <summary><c>GET /api/v1/network/load-balancers</c>.</summary>
        public const string List = "network-load-balancers-list";

        /// <summary><c>GET /api/v1/network/load-balancers/{loadBalancerId}</c>.</summary>
        public const string Get = "network-load-balancers-get";

        /// <summary><c>POST /api/v1/network/load-balancers</c>.</summary>
        public const string Create = "network-load-balancers-create";

        /// <summary><c>DELETE /api/v1/network/load-balancers/{loadBalancerId}</c>.</summary>
        public const string Delete = "network-load-balancers-delete";
    }
}
