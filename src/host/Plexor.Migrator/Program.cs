// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Plexor.Migrator — CLI for running EF Core migrations and database utilities.
// ============================================================================
// Wires:
//   - AddSigilInfrastructureCore() so each module's DbContext is registered
//     against the shared Postgres connection.
//   - MigrationRunner is the IHostedService that applies pending migrations
//     in FK-dependency order on host startup.
//
// Rule: end with `app.Run()` (sync). NO await at top level — see VSTHRD200
// and async-and-tasks.md §3. Work is done inside IHostedService implementations
// (MigrationRunner, SeedDispatcher) that own their async lifecycle.
// ============================================================================

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Plexor.Migrator;
using Plexor.Modules.Branding.Infrastructure.Installers;
using Plexor.Modules.Branding.Infrastructure.Persistence;
using Plexor.Modules.Clusters.Infrastructure.Persistence;
using Plexor.Modules.Quotas.Infrastructure.Installers;
using Plexor.Modules.Quotas.Infrastructure.Persistence;
using Plexor.Modules.Realm.Infrastructure.AuthProviders;
using Plexor.Modules.Realm.Infrastructure.Persistence;
using Plexor.Modules.Sigil.Infrastructure.Installers;
using Plexor.Modules.Sigil.Infrastructure.Persistence;
using Plexor.Shared.Configuration;
using Plexor.Shared.Mtls.Persistence;
using Plexor.Shared.Persistence;

var builder = Host.CreateApplicationBuilder(args);

// ----------------------------------------------------------------------------
// Plexor config stack — TOML + PLX_* env vars on top of the default
// JSON sources the Host builder wired up. See Plexor.Shared.Configuration
// for layering + priority order.
// ----------------------------------------------------------------------------
builder.Configuration.AddPlexorConfiguration();

// Connection string resolution: prefer the explicit MIGRATOR_CONNECTION
// env var (used by tooling + dev workflows); fall back to the
// appsettings.json value. The env var is what `dotnet ef` design-time
// tooling uses, and keeping both paths means the same connection string
// flows uniformly to migrator and design-time tools.
var migrationConnection =
    Environment.GetEnvironmentVariable("MIGRATOR_CONNECTION")
    ?? builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:Postgres (or MIGRATOR_CONNECTION env var) is not configured.");

// Build the shared NpgsqlDataSource once. The migrator and the Host
// use the same pool pattern so a design-time `dotnet ef database
// update` from this directory resolves the same physical connection
// as the running host.
var plexorDataSource = builder.Services.AddPlexorDataSource(migrationConnection);

// Explicit DbContext registration. Every PlexorDbContext subclass
// owns its own table set + migrations; the migrator applies them
// in the order declared below. Adding a new DbContext requires
// adding a call here — the compiler will not silently miss it.
//
// FK-dependency order: Realm (organizations referenced by sigil.users)
// → Identity (users referenced by clusters.nodes) → Clusters
// (FKs to sigil.users + realm.organizations) → Mtls RevokedCerts
// (no FKs, kept last; shares forge schema with Clusters).
// Quotas is isolated (polymorphic ScopeId, no FKs into Realm /
// Sigil) — runs after them so a freshly-migrated quotas schema can
// reference the catalog rows the seeder is about to insert.
builder.Services.AddModuleDbContext<RealmDbContext>(plexorDataSource);
builder.Services.AddModuleDbContext<IdentityDbContext>(plexorDataSource);
builder.Services.AddModuleDbContext<ClusterDbContext>(plexorDataSource);
builder.Services.AddModuleDbContext<RevokedCertsDbContext>(plexorDataSource);
builder.Services.AddModuleDbContext<QuotasDbContext>(plexorDataSource);
builder.Services.AddModuleDbContext<BrandingDbContext>(plexorDataSource);

builder.Services.AddSigilInfrastructureCore();

// Quotas infrastructure — needed for the OrgSeederHostedService (4.5.f)
// and its IOrgSeeder dependency. Other Quotas services registered by
// this installer (catalog / scope resolver / enforcer / rate limiter /
// RateLimitCleanupService) are inert for the migrator's short
// lifetime — only IOrgSeeder + OrgSeederHostedService are actually
// resolved. RateLimitCleanupService is a no-op during the typical
// one-shot migrate cycle (no hourly sweep boundary falls inside).
builder.Services.AddQuotasInfrastructureCore();

// Branding infrastructure — needed for BrandingGlobalSeederHostedService
// and its IBrandingService dependency. The singleton seeder runs on
// startup to ensure the global_theme_config row exists.
builder.Services.AddBrandingInfrastructureCore();

// OrgSeederHostedService (4.5.f) needs a way to enumerate the org ids
// to seed. Same pattern as Plexor.Host — singleton delegate opens a
// fresh scope so the scoped RealmDbContext lifetime is respected.
// Non-async lambda body — the Realm query is a simple SELECT id, so
// sync ToList is fine; wrapping in Task.FromResult avoids the
// Task<List<T>> → Task<IReadOnlyCollection<T>> invariance issue that
// trips the async-lambda form. In v0.1 the Realm query typically
// returns zero rows (the migrator runs Realm migrations but does not
// seed an org yet — that's a Phase 2 concern); the hosted service
// no-ops gracefully in that case.
builder.Services.AddSingleton<Func<CancellationToken, Task<IReadOnlyCollection<Guid>>>>(
    static sp => cancellationToken =>
    {
        using var scope = sp.CreateAsyncScope();
        var realm = scope.ServiceProvider.GetRequiredService<RealmDbContext>();
        var ids = realm.Organizations
            .Select(static organization => organization.Id)
            .ToList();
        return Task.FromResult<IReadOnlyCollection<Guid>>(ids);
    });

builder.Services.AddHostedService<MigrationRunner>();
builder.Services.AddHostedService<IdentityBootstrapper>();
builder.Services.AddHostedService<QuotaDefinitionSeeder>();
builder.Services.AddHostedService<Plexor.Modules.Branding.Application.Branding.BrandingGlobalSeederHostedService>();

var app = builder.Build();
app.Run();
