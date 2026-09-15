// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// AuditApplicationInstaller — single registration entry for the Audit
// Application layer. v1 ships no Application-layer services (audit emit
// is a generic kernel contract; the read endpoint lands in 5.2).
// ============================================================================

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Plexor.Modules.Audit.Application.Installers;

/// <summary>
///     DI registration for the Audit Application layer. Empty in 5.1
///     — the audit emit pipeline is a kernel contract (no Application
///     services in the path); the read endpoint in 5.2 will register
///     its query service here.
/// </summary>
public static class AuditApplicationInstaller
{
    /// <summary>Register Audit Application-layer services.</summary>
    /// <param name="services">The host's service collection.</param>
    /// <param name="configuration">Reserved for Options binding (5.2
    /// may add <c>AuditOptions</c> for retention defaults).</param>
    /// <returns>The same <paramref name="services" /> for chaining.</returns>
    public static IServiceCollection AddAuditApplicationCore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        _ = configuration;
        return services;
    }
}
