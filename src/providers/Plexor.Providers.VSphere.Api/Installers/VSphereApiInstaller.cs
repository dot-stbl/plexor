// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VSphereApiInstaller — single registration entry for the vSphere
// API layer. The endpoints themselves are mapped explicitly from
// Program.cs via the static Map* extensions; this installer exists
// for the wiring-point contract (mirrors Plexor.Modules.Audit's
// AuditApiInstaller — empty in v1, slot reserved for future
// per-request DI surface).
// ============================================================================

using Microsoft.Extensions.DependencyInjection;
using Plexor.Providers.VSphere.Infrastructure.Persistence;

namespace Plexor.Providers.VSphere.Api.Installers;

/// <summary>
///     DI registration for the vSphere API layer. v1 ships no
///     per-request services — the endpoints resolve
///     <see cref="VSphereDbContext" /> + the inventory refresher +
///     the provisioning service through the composition root's
///     registrations.
/// </summary>
public static class VSphereApiInstaller
{
    /// <summary>
    ///     Register the vSphere API layer's services. Empty in v1
    ///     — the slot stays so Program.cs's call chain stays
    ///     stable as future validators / per-request helpers
    ///     land.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same <paramref name="services" /> for chaining.</returns>
    public static IServiceCollection AddVSphereApiCore(this IServiceCollection services)
    {
        return services;
    }
}
