// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// BrandingInfrastructureInstaller — single registration entry for the
// Branding Infrastructure layer. Hosts compose it as
//   builder.Services.AddBrandingInfrastructureCore()
// after AddBrandingApplicationCore.
//
// BrandingDbContext itself is registered centrally by the
// composition root (Plexor.Host Program.cs + Plexor.Migrator
// Program.cs) via an explicit
// AddModuleDbContext<BrandingDbContext>(plexorDataSource) call.
// Re-registering here would double-register.
// ============================================================================

using Microsoft.Extensions.DependencyInjection;
using Plexor.Modules.Branding.Application.Branding;
using Plexor.Modules.Branding.Infrastructure.Branding;

namespace Plexor.Modules.Branding.Infrastructure.Installers;

/// <summary>
///     DI registration for the Branding Infrastructure layer.
///     Registers the EF-backed <see cref="IBrandingService" /> as
///     scoped. The <see cref="Plexor.Modules.Branding.Infrastructure.Persistence.BrandingDbContext" />
///     is registered centrally by
///     <c>AddModuleDbContext&lt;BrandingDbContext&gt;</c> — do not
///     re-register.
/// </summary>
public static class BrandingInfrastructureInstaller
{
    /// <summary>
    ///     Register Branding Infrastructure-layer services.
    /// </summary>
    /// <param name="services">The DI container.</param>
    /// <returns>The container, for chaining.</returns>
    public static IServiceCollection AddBrandingInfrastructureCore(
        this IServiceCollection services)
    {
        // IBrandingService — EF implementation over BrandingDbContext.
        // Scoped — shares the per-request DbContext with the
        // controller the action handler wraps.
        services.AddScoped<IBrandingService, EfBrandingService>();

        return services;
    }
}