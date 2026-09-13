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
///     <para><b>What lands in 4.5.f.</b>
///     <c>IOrgSeeder</c> (scoped — same DbContext lifetime as the
///     enforcer) and <c>OrgSeederHostedService</c> (singleton hosted
///     service — runs once at startup, opens its own scope). The
///     composition root (Plexor.Host / Plexor.Migrator) wires the
///     <c>Func&lt;CancellationToken, Task&lt;IReadOnlyCollection&lt;Guid&gt;&gt;&gt;</c>
///     delegate the hosted service consumes to enumerate org ids.</para>
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

        // IOrgSeeder — 4.5.f. Inserts one QuotaAssignment row per
        // catalog default for every org. Scoped — the hosted service
        // opens its own scope per sweep.
        services.AddScoped<IOrgSeeder, EfOrgSeeder>();

        // IQuotaAssignmentRepository — 4.5.g.2. Read + write surface
        // for the polymorphic quota_assignments table. The 4.5.g.2
        // GET /api/v1/quotas/assignments endpoint exercises the read
        // path; 4.5.g.3 will exercise UpsertAsync + DeleteAsync from
        // the PUT + DELETE handlers. Scoped — shares the per-request
        // DbContext with the controller.
        services.AddScoped<IQuotaAssignmentRepository, EfQuotaAssignmentRepository>();

        // IQuotaUsageReader — 4.5.g.2. Read-only surface over the
        // quota_usage snapshot table. The 4.5.g.2 GET
        // /api/v1/quotas/usage endpoint pairs each row with the
        // resolved effective limit. Scoped — same lifetime pattern as
        // the assignment repo.
        services.AddScoped<IQuotaUsageReader, EfQuotaUsageReader>();

        // IQuotaAuditEmitter — 4.5.h. v1 writes one structured log
        // line per quota audit event (UsageExceeded + LimitApproaching
        // from the enforcer; AssignmentChanged + AssignmentRemoved from
        // the controller). Phase 5+ swaps the implementation for an
        // atlas.audit_entries insert behind the same interface.
        // Scoped — the enforcer + the controller share the per-request
        // lifetime so the emit can carry request-scoped enrichments
        // via the logger scope.
        services.AddScoped<IQuotaAuditEmitter, LoggingQuotaAuditEmitter>();

        // RateLimitCleanupService — singleton hosted service. The
        // BackgroundService opens its own scope per sweep (DbContext
        // is scoped per request, not per host).
        services.AddHostedService<RateLimitCleanupService>();

        // OrgSeederHostedService — 4.5.f. Runs once at host/migrator
        // startup. Resolves the IOrgSeeder from a fresh scope and
        // delegates to it for the org ids the composition root
        // supplied via the Func<...> delegate.
        services.AddHostedService<OrgSeederHostedService>();

        return services;
    }
}
