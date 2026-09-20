// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VSphereInfrastructureInstaller — single registration entry for the
// vSphere Infrastructure layer. Registers:
//   - VSphereInventoryRefresher (scoped — opens its own scope if
//     called from a singleton hosted service)
//   - VSphereProvisioningService (scoped — uses the per-request
//     VSphereDbContext so the audit-trail row lives in the same
//     transaction as the upstream call's response handling)
//
// VSphereDbContext itself is registered centrally by the
// composition root (Plexor.Host Program.cs + Plexor.Migrator
// Program.cs) via AddModuleDbContext<VSphereDbContext>(plexorDataSource).
// Re-registering here would double-register.
// ============================================================================

using Microsoft.Extensions.DependencyInjection;
using Plexor.Providers.VSphere.Infrastructure.Inventory;
using Plexor.Providers.VSphere.Infrastructure.Persistence;
using Plexor.Providers.VSphere.Infrastructure.Provisioning;

namespace Plexor.Providers.VSphere.Infrastructure.Installers;

/// <summary>
///     DI registration for the vSphere Infrastructure layer.
///     Registers the inventory refresher + the provisioning
///     service. The <see cref="VSphereDbContext" /> is registered
///     centrally by the composition root via
///     <c>AddModuleDbContext&lt;VSphereDbContext&gt;</c> — do not
///     re-register.
/// </summary>
public static class VSphereInfrastructureInstaller
{
    /// <summary>
    ///     Register vSphere Infrastructure-layer services.
    /// </summary>
    /// <param name="services">The DI container.</param>
    /// <returns>The container, for chaining.</returns>
    public static IServiceCollection AddVSphereInfrastructureCore(
        this IServiceCollection services)
    {
        services.AddScoped<VSphereInventoryRefresher>();
        services.AddScoped<VSphereProvisioningService>();

        return services;
    }
}
