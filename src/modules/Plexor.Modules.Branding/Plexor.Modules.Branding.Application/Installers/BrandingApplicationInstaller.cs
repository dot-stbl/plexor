// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// BrandingApplicationInstaller — single registration entry for the
// Branding Application layer. Mirrors QuotasApplicationInstaller
// (same shape; the hosted service in Application that needs the
// Infrastructure-layer service is registered alongside the hosted
// service here so the hosted service can be resolved from DI before
// the Infrastructure installer runs).
// ============================================================================

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Plexor.Modules.Branding.Application.Branding;

namespace Plexor.Modules.Branding.Application.Installers;

/// <summary>
///     DI registration for the Branding Application layer. The
///     <see cref="BrandingGlobalSeederHostedService" /> lives here
///     (singleton hosted service — opens its own scope per sweep);
///     the EF-backed <see cref="IBrandingService" /> is registered
///     in <c>BrandingInfrastructureInstaller</c> as scoped.
/// </summary>
public static class BrandingApplicationInstaller
{
    /// <summary>
    ///     Register Branding Application-layer services — the
    ///     singleton seeder hosted service in v1.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <param name="configuration">Reserved for Options binding
    /// (a future commit may add <c>BrandingOptions</c> for
    /// custom-CSS path etc.).</param>
    /// <returns>The same <paramref name="services" /> for chaining.</returns>
    public static IServiceCollection AddBrandingApplicationCore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        _ = configuration;

        services.AddHostedService<BrandingGlobalSeederHostedService>();

        return services;
    }
}