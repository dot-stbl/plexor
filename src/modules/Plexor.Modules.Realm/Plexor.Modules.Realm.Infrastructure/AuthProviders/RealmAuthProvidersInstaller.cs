// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// RealmAuthProvidersInstaller — registration entry for the
// auth-providers seam (4.6.1). Hosts compose it as
//   builder.Services.AddRealmAuthProviders();
// after AddRealmApplicationCore. Wires the first-boot
// IHostedService that ensures every org has a default Sigil row
// in realm.org_auth_provider_configs.
// ============================================================================

using Microsoft.Extensions.DependencyInjection;
using Plexor.Modules.Realm.Application.AuthProviders;

namespace Plexor.Modules.Realm.Infrastructure.AuthProviders;

/// <summary>
///     DI registration extension for the realm auth-providers
///     seam. Hosts compose it as
///     <c>builder.Services.AddRealmAuthProviders()</c> after
///     <c>AddModuleDbContext&lt;RealmDbContext&gt;</c> (the
///     DbContext itself is registered centrally by the
///     composition root — do NOT re-register here).
/// </summary>
/// <remarks>
///     <para><b>Why Infrastructure-layer.</b> The interface
///     <see cref="IOrgAuthProviderSeeder" /> lives in
///     <c>Plexor.Modules.Realm.Application</c>; the EF-backed
///     implementation <see cref="EfOrgAuthProviderSeeder" />
///     needs <see cref="Persistence.RealmDbContext" />, which
///     lives in this project. The hosted service
///     <see cref="OrgAuthProviderSeeder" /> lives in Application
///     so the migrator can call it without taking an
///     Infrastructure dependency on Realm.</para>
/// </remarks>
public static class RealmAuthProvidersInstaller
{
    /// <summary>
    ///     Register the realm auth-providers seam: the EF
    ///     implementation + the first-boot hosted service.
    ///     Idempotent on re-run; the underlying seeder skips orgs
    ///     that already have a config row.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same <paramref name="services" /> for chaining.</returns>
    public static IServiceCollection AddRealmAuthProviders(this IServiceCollection services)
    {
        // IOrgAuthProviderSeeder — scoped per request so the
        // RealmDbContext lifetime is respected.
        services.AddScoped<IOrgAuthProviderSeeder, EfOrgAuthProviderSeeder>();

        // IHostedService — singleton, lifetime matches the host.
        // The seeder opens its own scope on StartAsync so the
        // scoped IOrgAuthProviderSeeder is resolved correctly.
        services.AddHostedService<OrgAuthProviderSeeder>();

        return services;
    }
}
