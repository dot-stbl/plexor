// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// AuditInfrastructureInstaller — single registration entry for the Audit
// Infrastructure layer. Registers the IAuditEmitter implementation
// (DbAuditEmitter, scoped — shares the per-request AuditDbContext with
// the caller).
//
// AuditDbContext itself is registered centrally by the composition root
// (Plexor.Host Program.cs + Plexor.Migrator Program.cs) via an explicit
// AddModuleDbContext<AuditDbContext>(plexorDataSource) call. Re-registering
// here would double-register — mirrors the Branding/Quotas pattern.
// ============================================================================

using Microsoft.Extensions.DependencyInjection;
using Plexor.Modules.Audit.Infrastructure.Audit;
using Plexor.Shared.Kernel.Audit;

namespace Plexor.Modules.Audit.Infrastructure.Installers;

/// <summary>
///     DI registration for the Audit Infrastructure layer.
///     Registers the EF-backed <see cref="IAuditEmitter" /> as scoped.
///     The <see cref="Plexor.Modules.Audit.Infrastructure.Persistence.AuditDbContext" />
///     is registered centrally by
///     <c>AddModuleDbContext&lt;AuditDbContext&gt;</c> — do not re-register.
/// </summary>
public static class AuditInfrastructureInstaller
{
    /// <summary>
    ///     Register Audit Infrastructure-layer services.
    /// </summary>
    /// <param name="services">The DI container.</param>
    /// <returns>The container, for chaining.</returns>
    public static IServiceCollection AddAuditInfrastructureCore(
        this IServiceCollection services)
    {
        // IAuditEmitter — EF implementation over AuditDbContext.
        // Scoped — shares the per-request AuditDbContext with the
        // controller / enforcer that called EmitAsync so the audit
        // INSERT lives in the same transaction as the action that
        // triggered it.
        services.AddScoped<IAuditEmitter, DbAuditEmitter>();

        // AuditRetentionService — singleton hosted service. Opens its
        // own scope per sweep (DbContext is scoped per request, not
        // per host). Mirrors the Quotas RateLimitCleanupService
        // pattern; the AuditOptions binding lives in the composition
        // root (Plexor.Host / Plexor.Migrator Program.cs) so the
        // IConfiguration access stays where every other Options
        // binding lives (BrandingOptions, CertAuthorityOptions, ...).
        services.AddHostedService<AuditRetentionService>();

        return services;
    }
}
