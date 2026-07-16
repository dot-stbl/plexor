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
using Plexor.Modules.Clusters.Infrastructure.Persistence;
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

// Explicit DbContext registration. Every PlexorDbContext subclass
// owns its own table set + migrations; the migrator applies them
// in the order declared below. Adding a new DbContext requires
// adding a call here — the compiler will not silently miss it.
//
// FK-dependency order: Realm (organizations referenced by sigil.users)
// → Identity (users referenced by clusters.nodes) → Clusters
// (FKs to sigil.users + realm.organizations) → Mtls RevokedCerts
// (no FKs, kept last; shares forge schema with Clusters).
builder.Services.AddModuleDbContext<RealmDbContext>(migrationConnection);
builder.Services.AddModuleDbContext<IdentityDbContext>(migrationConnection);
builder.Services.AddModuleDbContext<ClusterDbContext>(migrationConnection);
builder.Services.AddModuleDbContext<RevokedCertsDbContext>(migrationConnection);

builder.Services.AddSigilInfrastructureCore(builder.Configuration);

builder.Services.AddHostedService<MigrationRunner>();
builder.Services.AddHostedService<IdentityBootstrapper>();

var app = builder.Build();
app.Run();
