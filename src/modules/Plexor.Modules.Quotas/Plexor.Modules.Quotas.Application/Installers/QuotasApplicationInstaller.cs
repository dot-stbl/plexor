using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Plexor.Modules.Quotas.Application.Installers;

/// <summary>
///     Single registration entry for the Quotas Application layer.
///     Hosts compose it as <c>builder.Services.AddQuotasApplicationCore(builder.Configuration);</c>.
/// </summary>
/// <remarks>
///     <para><b>Why this exists for 4.5.a.</b> The Application layer
///     ships no runnable services today — the catalog seed is an
///     Infrastructure-layer concern that runs at startup via an
///     <c>IHostedService</c> in <c>Plexor.Migrator</c>. The
///     installer method exists so the host chain
///     <c>AddQuotasApplicationCore().AddQuotasInfrastructureCore()</c>
///     stays stable as Application services land in 4.5.b+
///     (<c>IQuotaCatalog</c> resolver, scope walker, validators).</para>
/// </remarks>
public static class QuotasApplicationInstaller
{
    /// <summary>Register Quotas Application-layer services.</summary>
    /// <param name="services">The host's service collection.</param>
    /// <param name="configuration">Reserved for Options binding (4.5.b may add
    /// <c>QuotaOptions</c> for warning-threshold tuning).</param>
    /// <returns>The same <paramref name="services" /> for chaining.</returns>
    public static IServiceCollection AddQuotasApplicationCore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        _ = configuration;
        return services;
    }
}
