// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// OrgSeederHostedService — runs IOrgSeeder for every org on host startup.
// The migrator hits this on first deploy (Realm migrations have created
// the default org); the host hits it on every restart (idempotent —
// already-seeded orgs are no-ops). v0.2+ multi-tenant deploys will add
// a Realm domain-event consumer so SaaS-created orgs get seeded without
// a restart; out of scope for 4.5.f.
// ============================================================================

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Plexor.Shared.Kernel.Quotas;

namespace Plexor.Modules.Quotas.Application.Quotas;

/// <summary>
///     Runs the <see cref="IOrgSeeder" /> for every organization known
///     to the composition root on host startup. Idempotent — re-runs on
///     an already-seeded fleet are no-ops.
/// </summary>
/// <param name="scopeFactory">
///     Application-level scope factory. The hosted service is singleton;
///     <see cref="IOrgSeeder" /> is scoped per request, so each startup
///     sweep opens a fresh scope.</param>
/// <param name="orgIdsProvider">
///     Delegate the composition root wires to enumerate org ids.
///     Returns an empty collection when no orgs exist yet (the migrator
///     hasn't run its seed step).</param>
/// <param name="logger">Structured logger for the seeded rowcount.</param>
/// <remarks>
///     <para><b>Why a delegate, not RealmDbContext directly.</b>
///     <see cref="OrgSeederHostedService" /> lives in
///     <c>Plexor.Modules.Quotas.Application</c> and does not depend on
///     <c>Plexor.Modules.Realm</c>. The composition root supplies a
///     delegate that resolves <c>RealmDbContext</c> from its own scope
///     so the seam stays loose. The migrator supplies the same shape
///     with the one well-known dev org id.</para>
///     <para><b>Why a one-shot <see cref="IHostedService" />.</b>
///     <c>StartAsync</c> runs once before <c>app.Run</c>. There is no
///     loop — the catalog of default assignments does not change at
///     runtime in v1; a restart picks up new catalog rows.</para>
///     <para><b>Failure mode.</b> An exception in the org-id delegate is
///     logged + swallowed (the host must still start); an exception in
///     the seed itself propagates and aborts startup. The startup is
///     idempotent so the next host restart re-attempts the seed.</para>
/// </remarks>
public sealed class OrgSeederHostedService(
    IServiceScopeFactory scopeFactory,
    Func<CancellationToken, Task<IReadOnlyCollection<Guid>>> orgIdsProvider,
    ILogger<OrgSeederHostedService> logger) : IHostedService
{
    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        IReadOnlyCollection<Guid> orgIds;
        try
        {
            orgIds = await orgIdsProvider(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "OrgSeeder: could not enumerate orgs; skipping seed.");
            return;
        }

        if (orgIds.Count == 0)
        {
            logger.LogDebug("OrgSeeder: no orgs to seed; skipping.");
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IOrgSeeder>();
        var inserted = await seeder.SeedAllOrgsAsync(orgIds, cancellationToken);

        logger.LogInformation(
            "OrgSeeder: seeded {Inserted} default assignment row(s) across {OrgCount} org(s).",
            inserted,
            orgIds.Count);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}