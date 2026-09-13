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
using Plexor.Modules.Quotas.Application.Quotas;
using Plexor.Modules.Quotas.Infrastructure.Persistence;
using Plexor.Modules.Quotas.Infrastructure.Quotas;
using Plexor.Shared.Kernel.Quotas;

namespace Plexor.Modules.Quotas.Infrastructure.Installers;

/// <summary>
///     DI registration extension for the Quotas Infrastructure layer.
/// </summary>
/// <remarks>
///     <para><b>What's here in 4.5.b.</b>
///     <c>IQuotaCatalog</c>, <c>IQuotaScopeResolver</c>, and
///     <c>IQuotaEnforcer</c> EF implementations. All three are scoped
///     — they share the <see cref="QuotasDbContext" /> lifetime with
///     the caller's resource-create transaction. The migrator hosts
///     the catalog seeder (<c>QuotaDefinitionSeeder</c>) outside this
///     installer because the seed must run after schema migrations
///     apply.</para>
///     <para><b>What lands in 4.5.e.</b>
///     <c>IRateLimiter</c> (scoped — shares the per-request DbContext
///     with the controller the filter wraps) and
///     <see cref="RateLimitCleanupService" /> (singleton hosted
///     service — opens its own scope per sweep).</para>
///     <para><b>What lands in 4.5.g+.</b>
///     The <c>QuotaExceededException</c> → 429 ProblemDetails exception
///     handler lives in the Api project (registered alongside the
///     controllers).</para>
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
        // IQuotaCatalog — read-only EF surface over quota_definitions.
        // Used by the resolver (effective-limit lookup) and the 4.5.g
        // REST endpoint.
        services.AddScoped<IQuotaCatalog, EfQuotaCatalog>();

        // IQuotaScopeResolver — walks folder → org → default. The team
        // row is Phase 2. Reads only.
        services.AddScoped<IQuotaScopeResolver, EfQuotaScopeResolver>();

        // IQuotaEnforcer — atomic check + reserve via
        // pg_advisory_xact_lock. Participates in the caller's
        // transaction; the caller rolls back on Denied.
        services.AddScoped<IQuotaEnforcer, EfQuotaEnforcer>();

        // IRateLimiter — sliding-window rate limit. Scoped — shares
        // the per-request DbContext with the controller the
        // RateLimitFilter wraps. Reads the count + inserts one event
        // row per call; the cleanup BackgroundService keeps the table
        // bounded.
        services.AddScoped<IRateLimiter, EfRateLimiter>();

        // RateLimitCleanupService — singleton hosted service. The
        // BackgroundService opens its own scope per sweep (DbContext
        // is scoped per request, not per host).
        services.AddHostedService<RateLimitCleanupService>();

        return services;
    }
}
