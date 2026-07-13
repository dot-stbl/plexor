// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Plexor.Migrator — CLI for running EF Core migrations and database utilities.
// ============================================================================
// Wires:
//   - AddSigilInfrastructureCore() / AddRealmInfrastructureCore() so each
//     module's DbContext is registered against the shared Postgres connection.
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
using Plexor.Modules.Sigil.Infrastructure.Installers;
using Plexor.Shared.Persistence;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddPlexorModuleDbContexts(
    builder.Configuration.GetConnectionString("Postgres")
        ?? throw new InvalidOperationException(
            "ConnectionStrings:Postgres is not configured."));

builder.Services.AddSigilInfrastructureCore(builder.Configuration);

builder.Services.AddHostedService<MigrationRunner>();

var app = builder.Build();
app.Run();
