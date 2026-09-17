// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NetworkInfrastructureInstaller — single registration entry for the
// Network Infrastructure layer.
//
// NetworkDbContext itself is registered centrally by the composition
// root (Plexor.Host Program.cs + Plexor.Migrator Program.cs) via an
// explicit
// AddModuleDbContext<NetworkDbContext>(plexorDataSource) call.
// Re-registering here would double-register and cause scope/conflict
// errors — mirrors the Branding / Quotas / Storage / Audit pattern.
// ============================================================================

using Microsoft.Extensions.DependencyInjection;
using Plexor.Modules.Network.Application.FloatingIps;
using Plexor.Modules.Network.Application.LoadBalancers;
using Plexor.Modules.Network.Application.Network;
using Plexor.Modules.Network.Infrastructure.FloatingIps;
using Plexor.Modules.Network.Infrastructure.Installers;
using Plexor.Modules.Network.Infrastructure.LoadBalancers;

namespace Plexor.Modules.Network.Infrastructure.Installers;

/// <summary>
///     DI registration extension for the Network Infrastructure
///     layer. <see cref="Persistence.NetworkDbContext" /> is
///     registered centrally by
///     <c>AddModuleDbContext&lt;NetworkDbContext&gt;</c> — do not
///     re-register.
/// </summary>
public static class NetworkInfrastructureInstaller
{
    /// <summary>
    ///     Register Network Infrastructure-layer services. The
    ///     <see cref="Persistence.NetworkDbContext" /> itself is
    ///     registered centrally by
    ///     <c>AddModuleDbContext&lt;NetworkDbContext&gt;</c> — do not
    ///     re-register.
    /// </summary>
    public static IServiceCollection AddNetworkInfrastructureCore(
        this IServiceCollection services)
    {
        // INetworkQuotaReader — EF implementation over NetworkDbContext.
        // The Quotas.Infrastructure project depends on this seam (via
        // Plexor.Modules.Network.Application) and consumes it from DI
        // at runtime; we register it here in the owning module's
        // composition so the registration is co-located with the
        // implementation it binds. Scoped — shares the per-request
        // DbContext with the enforcer the read participates with.
        services.AddScoped<INetworkQuotaReader, EfNetworkQuotaReader>();

        // IFloatingIpService — EF implementation. Scoped — shares the
        // per-request DbContext with the API endpoint. Used by the
        // REST endpoints mounted from NetworkEndpoints in commit 4.
        services.AddScoped<IFloatingIpService, EfFloatingIpService>();

        // ILoadBalancerService — EF implementation. Scoped — same
        // pattern as IFloatingIpService.
        services.AddScoped<ILoadBalancerService, EfLoadBalancerService>();

        return services;
    }
}
