// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// QuotasInfrastructureInstaller — single registration entry for the
// Quotas Infrastructure layer.
//
// The QuotasDbContext itself is registered centrally by the
// composition root (Plexor.Host Program.cs + Plexor.Migrator
// Program.cs) via an explicit
// AddModuleDbContext<QuotasDbContext>(connectionString) call.
// Re-registering here would double-register and cause
// scope/conflict errors — mirrors the Clusters pattern.
// ============================================================================

using Microsoft.Extensions.DependencyInjection;

namespace Plexor.Modules.Quotas.Infrastructure.Installers;

/// <summary>
///     DI registration extension for the Quotas Infrastructure layer.
/// </summary>
/// <remarks>
///     <para><b>What's here in 4.5.a.</b>
///     The catalog seed <c>IHostedService</c> lives in
///     <c>Plexor.Migrator</c> (alongside <c>IdentityBootstrapper</c>)
///     because the seed is part of the migrator's startup sequence —
///     the catalog must be present before <c>MigrationRunner</c>
///     finishes. It is registered via
///     <c>builder.Services.AddHostedService&lt;QuotaDefinitionSeeder&gt;()</c>
///     in <c>Plexor.Migrator/Program.cs</c>, not here.</para>
///     <para><b>What lands in 4.5.b+.</b>
///     <list type="bullet">
///       <item><c>IQuotaCatalog</c> EF implementation (Application interface
///       → Infrastructure binding).</item>
///       <item><c>IQuotaEnforcer</c> EF implementation + scope resolver.</item>
///       <item><c>IRateLimiter</c> EF implementation.</item>
///       <item>4.5.g exception handler mapping
///       <c>QuotaExceededException</c> → 429 ProblemDetails.</item>
///     </list></para>
/// </remarks>
public static class QuotasInfrastructureInstaller
{
    /// <summary>
    ///     Register Quotas Infrastructure-layer services. The
    ///     <see cref="Persistence.QuotasDbContext" /> itself is
    ///     registered centrally by
    ///     <c>AddModuleDbContext&lt;T&gt;</c> — do not re-register here.
    /// </summary>
    /// <param name="services">The DI container.</param>
    /// <returns>The container, for chaining.</returns>
    public static IServiceCollection AddQuotasInfrastructureCore(
        this IServiceCollection services)
    {
        // 4.5.a ships no Infrastructure services yet. The catalog
        // seeder is hosted by Plexor.Migrator (it must run after
        // schema migrations apply). 4.5.b adds IQuotaCatalog +
        // IQuotaEnforcer bindings; 4.5.e adds IRateLimiter; 4.5.g adds
        // the exception handler.
        return services;
    }
}
