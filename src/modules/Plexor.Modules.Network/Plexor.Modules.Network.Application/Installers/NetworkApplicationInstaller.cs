// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NetworkApplicationInstaller — single registration entry for the
// Network Application layer. Mirrors the BrandingApplicationInstaller
// pattern: empty in v0.1 (no hosted services or application services
// today), but the registration call exists so Program.cs's chain
// stays stable as future services land.
// ============================================================================

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Plexor.Modules.Network.Application.Installers;

/// <summary>
///     DI registration for the Network Application layer. No services
///     to register in v0.1 — the contract <c>INetworkQuotaReader</c>
///     is bound to <c>EfNetworkQuotaReader</c> in
///     <c>NetworkInfrastructureInstaller</c> alongside the DbContext
///     wiring. The installer exists so the Program.cs chain stays
///     stable as Application-layer services land (e.g. a future
///     <c>IFloatingIpCreateHandler</c> would be registered here).
/// </summary>
public static class NetworkApplicationInstaller
{
    /// <summary>
    ///     Register Network Application-layer services.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <param name="configuration">Reserved for Options binding.</param>
    /// <returns>The same <paramref name="services" /> for chaining.</returns>
    public static IServiceCollection AddNetworkApplicationCore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        _ = configuration;
        return services;
    }
}
