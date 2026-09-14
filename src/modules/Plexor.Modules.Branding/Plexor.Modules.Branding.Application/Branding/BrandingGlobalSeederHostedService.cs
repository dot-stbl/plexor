// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// BrandingGlobalSeederHostedService — runs IBrandingService on host
// startup to ensure the singleton GlobalThemeConfig row exists.
// Idempotent — re-runs on an already-seeded row are no-ops. Lives in
// the Application layer (mirrors Quotas.Application / OrgSeederHostedService)
// because the seeder runs against the shared DbContext without
// coupling to the IBrandingService's EF internals.
// ============================================================================

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Plexor.Modules.Branding.Domain.Entities;

namespace Plexor.Modules.Branding.Application.Branding;

/// <summary>
///     Singleton hosted service that ensures the singleton
///     <c>branding.global_theme_config</c> row exists on host
///     startup. Runs once; idempotent on rerun.
/// </summary>
/// <param name="scopeFactory">Application-level scope factory —
/// the hosted service is singleton; <see cref="IBrandingService" />
/// is scoped per request, so each startup sweep opens a fresh
/// scope.</param>
/// <param name="logger">Structured logger for the seeded rowcount.</param>
public sealed class BrandingGlobalSeederHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<BrandingGlobalSeederHostedService> logger) : IHostedService
{
    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IBrandingService>();
            var current = await service.GetGlobalAsync(cancellationToken);

            // The repository returns the singleton row when present;
            // otherwise a freshly-constructed sentinel (no row id).
            // Detect the latter and force an upsert of the defaults
            // so the row exists for the first-boot experience.
            if (current.Id != GlobalThemeConfig.SingletonId)
            {
                await service.UpsertGlobalAsync(
                    new GlobalThemeConfig
                    {
                        BrandName = "Plexor",
                        DefaultPresetId = "plexor-default-light",
                    },
                    actorUserId: null,
                    cancellationToken: cancellationToken);
                logger.LogInformation(
                    "BrandingGlobalSeeder: seeded singleton global_theme_config row.");
            }
            else
            {
                logger.LogDebug(
                    "BrandingGlobalSeeder: singleton row already present; skipping seed.");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "BrandingGlobalSeeder: could not ensure singleton row; continuing startup.");
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}