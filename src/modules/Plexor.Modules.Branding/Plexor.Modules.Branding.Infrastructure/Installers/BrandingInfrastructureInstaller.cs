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

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Plexor.Modules.Branding.Application.Branding;
using Plexor.Modules.Branding.Infrastructure.Branding;
using Plexor.Modules.Branding.Infrastructure.ThemeManifests;

namespace Plexor.Modules.Branding.Infrastructure.Installers;

/// <summary>
///     DI registration for the Branding Infrastructure layer.
///     Registers the EF-backed <see cref="IBrandingService" /> +
///     the EF-backed <see cref="IThemeInstallationService" /> as
///     scoped. The
///     <see cref="Plexor.Modules.Branding.Infrastructure.Persistence.BrandingDbContext" />
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

        // IThemeInstallationService — EF implementation over
        // BrandingDbContext, plus the HMAC verifier + the bundled
        // CommunityThemeRegistry. Singletons for the
        // verifier / registry because they are stateless
        // (the verifier derives its key from the
        // purpose-scoped protector; the registry is a build-time
        // constant dict). Singleton keeps the key-derivation cost
        // off the request hot path.
        services.AddSingleton<IThemeManifestVerifier, HmacThemeManifestVerifier>();
        services.AddSingleton(CommunityThemeRegistryBuilder.Build());
        services.AddSingleton<IThemeInstallationService>(sp =>
            new EfThemeInstallationService(
                sp.GetRequiredService<Plexor.Modules.Branding.Infrastructure.Persistence.BrandingDbContext>(),
                sp.GetRequiredService<TimeProvider>(),
                sp.GetRequiredService<IThemeManifestVerifier>(),
                sp.GetRequiredService<CommunityThemeRegistry>()));

        return services;
    }
}

/// <summary>
///     Builds the bundled community-theme registry the host ships
///     with. Mirrors the FE bundle in
///     <c>web/apps/console/src/shared/lib/themes/community-themes.ts</c>;
///     when the publisher feed lands, a future commit swaps this
///     factory for a publisher-feed loader behind the same
///     <see cref="CommunityThemeRegistry" /> shape.
/// </summary>
internal static class CommunityThemeRegistryBuilder
{
    /// <summary>
    ///     Construct the registry from the built-in theme list.
    ///     Adding a theme here is a code change in lock-step with
    ///     the FE bundle — no configuration drift.
    /// </summary>
    public static CommunityThemeRegistry Build()
    {
        var themes = new Dictionary<string, CommunityTheme>(StringComparer.Ordinal)
        {
            ["synthwave-night-dark"] = new(
                Id: "synthwave-night-dark",
                Name: "Synthwave Night — Dark",
                Version: "0.1.0",
                Author: "plexor-themes"),
            ["synthwave-night-light"] = new(
                Id: "synthwave-night-light",
                Name: "Synthwave Night — Light",
                Version: "0.1.0",
                Author: "plexor-themes"),
            ["paper-light"] = new(
                Id: "paper-light",
                Name: "Paper Light",
                Version: "0.1.0",
                Author: "plexor-themes"),
        };
        return new CommunityThemeRegistry(themes);
    }
}
