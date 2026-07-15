// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// MigrationRunner — applies pending EF Core migrations on startup.
// Resolves the application's DbContext set from the DI container,
// migrates each in turn, then triggers the host stop so the
// migrator CLI exits with the right status code.
// ==========================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Plexor.Shared.Persistence;

namespace Plexor.Migrator;

/// <summary>
///     Migrator host entry-point. Run with <c>dotnet run --project
///     src/host/Plexor.Migrator</c> (or
///     <c>plexor-migrator run</c> on a published binary) to apply
///     pending migrations and seed first-run data. The host stops
///     itself after migrations succeed so the process exits 0.
/// </summary>
internal sealed class MigrationRunner(
    IServiceProvider services,
    ILogger<MigrationRunner> logger,
    IHostApplicationLifetime lifetime) : IHostedService
{
    /// <summary>
    ///     Walks every registered <see cref="PlexorDbContext" /> and
    ///     applies its pending migrations. The order of
    ///     application matches the order of DbContext registration
    ///     in <c>AddPlexorModuleDbContexts</c>; Phase 5+ will resolve
    ///     FK dependencies between schemas (Outbox → Tenants →
    ///     Identity → Audit, etc.) and order them explicitly. Failure
    ///     to migrate aborts the process before the host can serve
    ///     any traffic.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var contexts = services.GetServices<PlexorDbContext>().ToList();
            logger.LogInformation(
                "plexor-migrator: applying migrations to {ContextCount} DbContext(s).",
                contexts.Count);

            foreach (var context in contexts)
            {
                var contextName = context.GetType().Name;
                var pending = await context.Database
                    .GetPendingMigrationsAsync(cancellationToken);
                if (!pending.Any())
                {
                    logger.LogInformation(
                        "plexor-migrator: {Context} is up to date.",
                        contextName);
                    continue;
                }

                logger.LogInformation(
                    "plexor-migrator: applying {Count} pending migration(s) to {Context}: {Names}",
                    pending.Count(),
                    contextName,
                    string.Join(", ", pending));
                await context.Database.MigrateAsync(cancellationToken);
            }

            logger.LogInformation("plexor-migrator: all migrations applied.");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "plexor-migrator: migration failed.");
            lifetime.StopApplication();
            throw;
        }

        // Let IdentityBootstrapper run after us, then stop the host.
        // IdentityBootstrapper is also IHostedService; the runtime
        // invokes them in registration order, so we register it
        // AFTER MigrationRunner in Program.cs.
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("plexor-migrator: stopping host.");
        return Task.CompletedTask;
    }
}
