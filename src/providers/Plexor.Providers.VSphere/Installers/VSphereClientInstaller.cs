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
using Microsoft.Extensions.Options;
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

        // AddRefitClient + AddStandardResilienceHandler — the canonical
        // pattern from http-resilience-refit.md §2. The handler chain
        // is outer-first from the consumer: resilience is registered
        // LAST (innermost), so it wraps the auth handler. A 401 from
        // basic-auth will be retried by Polly until MaxRetryAttempts
        // is exhausted (basic-auth is idempotent, so this is fine).
        // Per-request timeouts come from AddStandardResilienceHandler's
        // TotalRequestTimeout — do NOT also set HttpClient.Timeout
        // here, that creates two competing timeout sources.
        services
            .AddRefitClient<IVSphereClient>()
            .ConfigureHttpClient((sp, client) =>
            {
                var vCenterUrl = sp.GetRequiredService<IOptions<VSphereOptions>>().Value.VCenterUrl;
                if (!string.IsNullOrWhiteSpace(vCenterUrl))
                {
                    client.BaseAddress = new Uri(vCenterUrl);
                }

                client.DefaultRequestHeaders.UserAgent.ParseAdd("Plexor-Host/0.1");
            })
            .AddHttpMessageHandler<VSphereBasicAuthHandler>()
            .AddStandardResilienceHandler();

        return services;
    }
}
