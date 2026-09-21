// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// OrgAuthProviderSeeder — 4.6.1 first-boot hosted service. Runs
// IOrgAuthProviderSeeder for every org on host startup. The
// migrator hits this on first deploy; the host hits it on every
// restart (idempotent — already-seeded orgs are no-ops).
// ============================================================================

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Plexor.Modules.Realm.Application.AuthProviders;

/// <summary>
///     Runs the <see cref="IOrgAuthProviderSeeder" /> on host
///     startup. Idempotent — re-runs on an already-seeded fleet
///     are no-ops. The composition root wires it via
///     <c>AddRealmAuthProviders()</c>.
/// </summary>
/// <param name="scopeFactory">
///     Application-level scope factory. The hosted service is
///     singleton; <see cref="IOrgAuthProviderSeeder" /> is
///     scoped per request, so each startup sweep opens a fresh
///     scope.</param>
/// <param name="logger">Structured logger for the inserted rowcount.</param>
/// <remarks>
///     <para><b>Why a hosted service (not a migrator-side
///     runner).</b> The Migrator wires the same installer, so the
///     hosted service runs in both contexts — the Migrator's
///     short-lived <c>app.Run()</c> still gives
///     <see cref="StartAsync" /> a chance to fire.</para>
///     <para><b>Failure mode.</b> An exception in
///     <see cref="StartAsync" /> propagates and aborts startup;
///     the next host restart re-attempts the seed.</para>
/// </remarks>
public sealed class OrgAuthProviderSeeder(
    IServiceScopeFactory scopeFactory,
    ILogger<OrgAuthProviderSeeder> logger) : IHostedService
{
    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var seeder = scope.ServiceProvider.GetRequiredService<IOrgAuthProviderSeeder>();
            var inserted = await seeder.SeedAllOrgsAsync(cancellationToken);

            if (inserted > 0)
            {
                logger.LogInformation(
                    "OrgAuthProviderSeeder: inserted {Inserted} default Sigil row(s).",
                    inserted);
            }
            else
            {
                logger.LogDebug(
                    "OrgAuthProviderSeeder: all orgs already have a config row; skipping seed.");
            }
        }
        catch (Exception exception)
        {
            // Log + propagate so the host startup aborts; the next
            // restart re-attempts. Swallowing here would let the host
            // boot with a half-configured fleet and is exactly the
            // wrong shape for a first-boot invariant.
            logger.LogCritical(
                exception,
                "OrgAuthProviderSeeder: unexpected failure during first-run seed.");
            throw;
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
