// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Plexor.Migrator — CLI for running EF Core migrations and database utilities.
// ============================================================================
// Skeleton placeholder. Real implementation will use CommandLineParser to
// dispatch subcommands (migrate, rollback, seed, status, reset).
//
// Rule: end with `app.Run()` (sync). NO await at top level — see VSTHRD200
// and async-and-tasks.md §3. Work is done inside IHostedService implementations
// (MigrationRunner, SeedDispatcher) that own their async lifecycle.
// ============================================================================

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Plexor.Migrator;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<MigrationRunner>();

var app = builder.Build();
app.Run();