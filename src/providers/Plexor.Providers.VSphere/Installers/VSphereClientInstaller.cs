// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VSphereClientInstaller — single registration entry for the vSphere
// Refit client. Wires:
//   1. VSphereOptions (ValidateDataAnnotations + ValidateOnStart)
//   2. IVSphereClient (Refit-typed; HTTP basic + Polly resilience)
//   3. VSphereBasicAuthHandler (singleton; the monitor refresh picks
//      up rotated credentials without a restart)
//
// The host composition root calls AddVSphereProvider(builder.Configuration)
// after every other provider is registered so the chain stays
// explicit.
// ============================================================================

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace Plexor.Providers.VSphere.Installers;

/// <summary>
///     DI registration for the vSphere Refit client. One installer —
///     one entry point — keeps the host composition root readable.
///     Mirrors the convention used by Plexor.Modules.* Application
///     installers (two-arg overload + parameterless overload that
///     reads from the host's <see cref="IConfiguration" />).
/// </summary>
public static class VSphereClientInstaller
{
    /// <summary>
    ///     Register the vSphere Refit client + options + auth handler.
    ///     Host invokes this overload from its composition root.
    /// </summary>
    /// <param name="services">The host's DI service collection.</param>
    /// <param name="configuration">The host's
    ///     <see cref="IConfiguration" /> — reads the
    ///     <see cref="VSphereOptions.SectionName" /> section.</param>
    /// <returns>The same <paramref name="services" /> for chaining.</returns>
    public static IServiceCollection AddVSphereProvider(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // vSphere is opt-in — the host may deploy without the
        // [Providers:VSphere] section configured. We bind + validate
        // the *shape* (Url / Length attributes) but DO NOT call
        // ValidateOnStart — that would fail the host startup when
        // the operator hasn't deployed a vSphere integration. The
        // inventory + clone endpoints check IsConfigured() and
        // return 503 when the section is missing or incomplete.
        services.AddOptions<VSphereOptions>()
            .Bind(configuration.GetSection(VSphereOptions.SectionName))
            .ValidateDataAnnotations();

        services.AddSingleton<VSphereBasicAuthHandler>();

        var section = configuration.GetSection(VSphereOptions.SectionName);
        var vCenterUrl = section["VCenterUrl"];

        // AddRefitClient + AddStandardResilienceHandler — the canonical
        // pattern from http-resilience-refit.md §2. Bearer handler
        // sits before resilience in the chain so auth failures are
        // surfaced immediately (no retry on 401).
        services
            .AddRefitClient<IVSphereClient>()
            .ConfigureHttpClient(client =>
            {
                if (!string.IsNullOrWhiteSpace(vCenterUrl))
                {
                    client.BaseAddress = new Uri(vCenterUrl);
                }

                client.DefaultRequestHeaders.UserAgent.ParseAdd("Plexor-Host/0.1");
                client.Timeout = TimeSpan.FromMinutes(2);
            })
            .AddHttpMessageHandler<VSphereBasicAuthHandler>()
            .AddStandardResilienceHandler();

        return services;
    }
}
