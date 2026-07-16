// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PlexorCertAuthorityInstaller — DI wiring for the Plexor CA. One
// extension method, one place where the service graph is composed.
// ============================================================================

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Plexor.Shared.Mtls;

/// <summary>
///     Composition root for the Plexor CA. Register the services,
///     bind options, register the IHostedService that warms the
///     root on boot.
/// </summary>
public static class PlexorCertAuthorityInstaller
{
    /// <summary>
    ///     CA root lifetime — 10 years. Matches the design
    ///     decision in plan-mvp-secure-deploy (no rotation, leaf
    ///     cert TTL = CA TTL). Constant here so both the root
    ///     authority and the file store can read it without a
    ///     DI cycle.
    /// </summary>
    public static readonly TimeSpan DefaultCaLifetime = TimeSpan.FromDays(3650);

    /// <summary>Add Plexor CA services to the host's DI container.</summary>
    public static IServiceCollection AddPlexorCertAuthority(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<CertAuthorityOptions>(
            configuration.GetSection(CertAuthorityOptions.SectionName));

        // PlexorCaFileStore takes CertAuthorityOptions directly (not
        // IOptions<T>) so the ctor stays parameter-light. Resolve
        // through IOptions at registration time — the same pattern
        // every other options-bound service in the codebase uses.
        services.AddSingleton<PlexorCaFileStore>(static sp =>
            new PlexorCaFileStore(
                sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CertAuthorityOptions>>().Value));

        services.AddSingleton<PlexorCaRoot>();
        services.AddSingleton<RevokedCertCache>();
        services.AddSingleton<ICertificateAuthority, PlexorCertificateIssuer>();
        // PlexorCaStartup (IHostedService) is host-specific — the
        // host's composition root registers it directly so it can
        // pull the right services into the DI graph for the entry
        // point (control plane vs NodeAgent).

        return services;
    }
}