// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// StorageInfrastructureInstaller — single registration entry for the
// Storage Infrastructure layer.
//
// StorageDbContext itself is registered centrally by the composition
// root (Plexor.Host Program.cs + Plexor.Migrator Program.cs) via an
// explicit
// AddModuleDbContext<StorageDbContext>(plexorDataSource) call.
// Re-registering here would double-register and cause scope/conflict
// errors — mirrors the Branding / Quotas / Audit pattern.
// ============================================================================

using Microsoft.Extensions.DependencyInjection;
using Plexor.Modules.Storage.Application.Storage;

namespace Plexor.Modules.Storage.Infrastructure.Installers;

/// <summary>
///     DI registration extension for the Storage Infrastructure layer.
///     <see cref="Plexor.Modules.Storage.Infrastructure.Persistence.StorageDbContext" />
///     itself is registered centrally by
///     <c>AddModuleDbContext&lt;StorageDbContext&gt;</c> — do not
///     re-register here.
/// </summary>
public static class StorageInfrastructureInstaller
{
    /// <summary>
    ///     Register Storage Infrastructure-layer services. The
    ///     <see cref="Plexor.Modules.Storage.Infrastructure.Persistence.StorageDbContext" />
    ///     is registered centrally by
    ///     <c>AddModuleDbContext&lt;StorageDbContext&gt;</c> — do not
    ///     re-register.
    /// </summary>
    /// <param name="services">The DI container.</param>
    /// <returns>The container, for chaining.</returns>
    public static IServiceCollection AddStorageInfrastructureCore(
        this IServiceCollection services)
    {
        // IStorageQuotaReader — EF implementation over StorageDbContext.
        // The Quotas.Infrastructure project depends on this seam (via
        // Plexor.Modules.Storage.Application) and wires it in its own
        // QuotasInfrastructureInstaller — we register it here in the
        // owning module's composition so the registration is co-located
        // with the implementation it binds. Scoped — shares the
        // per-request DbContext with the enforcer the read participates
        // with (the advisory lock + UPDATE on quotas.quota_usage +
        // SELECT COUNT(*) on storage.volumes ride the same connection).
        services.AddScoped<IStorageQuotaReader, EfStorageQuotaReader>();

        return services;
    }
}
