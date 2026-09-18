// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// RealmAuthProvidersInstaller — registration entry for the
// auth-providers seam (4.6.1 + 4.6.3a). Hosts compose it as
//   builder.Services.AddRealmAuthProviders();
// after AddRealmApplicationCore. Wires the first-boot
// IHostedService that ensures every org has a default Sigil row
// in realm.org_auth_provider_configs, plus the Phase 4.6.3a
// read seam + the data-protection secret wrapper.
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
///     <para><b>Phase 4.6.3a additions.</b> The
///     <see cref="IOrgAuthProviderConfigReader" /> seam lets the
///     OIDC token client (Sigil.Infrastructure) read the per-
///     tenant config without a direct dependency on
///     <see cref="Persistence.RealmDbContext" />. The
///     <see cref="OrgAuthProviderSecretProtector" /> wraps the
///     <c>Microsoft.AspNetCore.DataProtection.IDataProtectionProvider</c>
///     registered in <c>Plexor.Host/Program.cs</c> so the
///     secret-protect / secret-unprotect logic lives in one
///     place.</para>
/// </remarks>
public static class RealmAuthProvidersInstaller
{
    /// <summary>
    ///     Register the realm auth-providers seam: the EF
    ///     implementation + the first-boot hosted service, plus
    ///     the Phase 4.6.3a read seam + secret protector. The
    ///     underlying seeder is idempotent on re-run (skips orgs
    ///     that already have a config row).
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

        // Phase 4.6.3a — read seam for callers outside the Realm
        // module (today: the OIDC token client in Sigil.Infrastructure).
        // Scoped — AsNoTracking() reads share the per-request scope.
        services.AddScoped<IOrgAuthProviderConfigReader, EfOrgAuthProviderConfigReader>();

        // Phase 4.6.3a — purpose-bound IDataProtector wrapper for the
        // OIDC client secret. Singleton — the underlying IDataProtector
        // is thread-safe and the wrapper holds no per-request state.
        // Registered against the interface so callers outside the Realm
        // module (Sigil.Infrastructure.OidcTokenClient) consume the seam
        // rather than the concrete wrapper (Law 3 — modules don't
        // reference each other's Infrastructure directly).
        services.AddSingleton<OrgAuthProviderSecretProtector>();
        services.AddSingleton<IOrgAuthProviderSecretProtector>(
            sp => sp.GetRequiredService<OrgAuthProviderSecretProtector>());

        return services;
    }
}
