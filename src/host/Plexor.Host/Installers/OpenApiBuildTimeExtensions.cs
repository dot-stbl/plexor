// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// OpenApiBuildTimeExtensions — build-time OpenAPI generation safety.
//
// Microsoft.Extensions.ApiDescription.Server launches the host's entry point
// (as the `GetDocument.Insider` tool) just to enumerate endpoints, then
// stops the host — but .NET starts every IHostedService first. Without
// intervention, a plain `dotnet build` would run our migrators/workers and
// execute business logic (including DB I/O like `SigningKeyBootstrapper`)
// purely to emit `artifacts/openapi.json`.
//
// This extension detects that build-time mode and strips the app's own
// hosted services so the tool exits cleanly without needing Postgres.
// ============================================================================

using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Plexor.Host.Installers;

/// <summary>
///     Build-time OpenAPI document generation safety for <c>Plexor.Host</c>.
/// </summary>
public static class OpenApiBuildTimeExtensions
{
    /// <summary>
    ///     Name of the assembly the OpenAPI build tool runs as.
    ///     <c>Microsoft.Extensions.ApiDescription.Server</c> launches the
    ///     host's entry point under this identity to capture the document.
    /// </summary>
    private const string DocumentToolAssemblyName = "GetDocument.Insider";

    /// <summary>
    ///     <see langword="true" /> when the current process is the build-time
    ///     OpenAPI document generator, not a normal host run. Detected via
    ///     entry-assembly name (the build tool uses its own entry assembly).
    /// </summary>
    public static bool IsOpenApiDocumentGeneration { get; } = string.Equals(
        Assembly.GetEntryAssembly()?.GetName().Name,
        DocumentToolAssemblyName,
        StringComparison.Ordinal);

    /// <summary>
    ///     Removes the application's own (<c>Plexor.*</c>) hosted services when
    ///     running under build-time OpenAPI generation, so no migrator/worker
    ///     executes during a plain <c>dotnet build</c>. The framework's
    ///     web-host service is left intact so document capture is unchanged.
    ///     No-op at runtime (returns <paramref name="services" /> as-is).
    /// </summary>
    /// <param name="services">The service collection to strip hosted services from.</param>
    /// <returns>The same <paramref name="services" /> instance for chaining.</returns>
    public static IServiceCollection RemoveHostedServicesForOpenApiGeneration(this IServiceCollection services)
    {
        if (!IsOpenApiDocumentGeneration)
        {
            return services;
        }

        var hostedServices = services
                .Where(static descriptor => descriptor.ServiceType == typeof(IHostedService)
                                            && descriptor.ImplementationType?.Namespace?.StartsWith("Plexor.", StringComparison.Ordinal) == true)
                .ToList();

        foreach (var hostedService in hostedServices)
        {
            services.Remove(hostedService);
        }

        return services;
    }
}
