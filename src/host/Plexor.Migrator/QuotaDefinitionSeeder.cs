// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// QuotaDefinitionSeeder — seeds the global QuotaDefinition catalog on
// migrator startup. Idempotent: if a row with the same Key already
// exists, the seeder leaves it alone. Adding a new catalog key in a
// future phase = appending a row to the CatalogEntries array — no
// schema change, no new migration.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Plexor.Modules.Quotas.Domain;
using Plexor.Modules.Quotas.Domain.Entities;
using Plexor.Modules.Quotas.Infrastructure.Persistence;

namespace Plexor.Migrator;

/// <summary>
///     Catalog seeder. Runs on migrator startup; inserts the
///     platform-shipped <see cref="QuotaDefinition" /> rows into the
///     <c>quotas.quota_definitions</c> table if and only if the row's
///     <see cref="QuotaDefinition.Key" /> is not already present.
/// </summary>
/// <param name="db"></param>
/// <param name="logger"></param>
/// <remarks>
///     <para><b>Why a global catalog (not per-org).</b>
///     <c>quotas.quota_definitions</c> is module-owned — there is no
///     <c>org_id</c> column. The catalog is a single shared registry
///     of stable keys + units + period + built-in default values; per-org
///     limits are stored separately in <c>quota_assignments</c>. The
///     4.5.f <c>OrgSeeder</c> creates per-org assignments from the
///     catalog's <see cref="QuotaDefinition.DefaultValue" />.</para>
///     <para><b>Catalog seed (4.5.a).</b>
///     <list type="table">
///         <listheader>
///             <term>Key</term><description>Unit / Period / Default</description>
///         </listheader>
///         <item><term><c>compute.vms.count</c></term>
///             <description>Count / None / 100</description></item>
///         <item><term><c>compute.vms.vcpu</c></term>
///             <description>Vcpu / None / 256</description></item>
///         <item><term><c>compute.vms.ram_gb</c></term>
///             <description>Gb / None / 1024</description></item>
///         <item><term><c>storage.volumes.count</c></term>
///             <description>Count / None / 200</description></item>
///         <item><term><c>storage.volumes.gb</c></term>
///             <description>Gb / None / 4096</description></item>
///         <item><term><c>network.floating_ips.count</c></term>
///             <description>Count / None / 10</description></item>
///         <item><term><c>network.load_balancers.count</c></term>
///             <description>Count / None / 20</description></item>
///         <item><term><c>api.requests.per_hour.user</c></term>
///             <description>ReqPerHour / Hour / 1000</description></item>
///         <item><term><c>api.requests.per_hour.org</c></term>
///             <description>ReqPerHour / Hour / 10000</description></item>
///     </list></para>
///     <para><b>Idempotency.</b> The seeder reads existing keys on every
///     run and only inserts rows whose <c>Key</c> is missing. Re-running
///     the seeder (after a deploy restart) is a no-op once the catalog is
///     fully populated.</para>
/// </remarks>
internal sealed class QuotaDefinitionSeeder(
    QuotasDbContext db,
    ILogger<QuotaDefinitionSeeder> logger) : IHostedService
{
    /// <summary>
    ///     Built-in catalog rows shipped with the platform. Each entry
    ///     encodes the stable <c>Key</c>, the unit, the period, and the
    ///     built-in default value the enforcer falls back to when no
    ///     per-scope assignment exists.
    /// </summary>
    private static readonly IReadOnlyList<CatalogEntry> CatalogEntries =
    [
        new("compute.vms.count",              "Number of VMs.",                              QuotaUnit.Count,      QuotaPeriod.None, 100m),
        new("compute.vms.vcpu",               "Cumulative vCPU across VMs.",                 QuotaUnit.Vcpu,       QuotaPeriod.None, 256m),
        new("compute.vms.ram_gb",             "Cumulative RAM GiB across VMs.",              QuotaUnit.Gb,         QuotaPeriod.None, 1024m),
        new("storage.volumes.count",          "Number of volumes.",                          QuotaUnit.Count,      QuotaPeriod.None, 200m),
        new("storage.volumes.gb",             "Cumulative volume GiB.",                      QuotaUnit.Gb,         QuotaPeriod.None, 4096m),
        new("network.floating_ips.count",     "Number of floating IPs.",                     QuotaUnit.Count,      QuotaPeriod.None, 10m),
        new("network.load_balancers.count",   "Number of load balancers.",                   QuotaUnit.Count,      QuotaPeriod.None, 20m),
        new("api.requests.per_hour.user",     "Sliding-window request count per user.",      QuotaUnit.ReqPerHour, QuotaPeriod.Hour, 1000m),
        new("api.requests.per_hour.org",      "Sliding-window request count per org.",       QuotaUnit.ReqPerHour, QuotaPeriod.Hour, 10000m),
    ];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var existingKeys = await db.QuotaDefinitions
            .AsNoTracking()
            .Select(static definition => definition.Key)
            .ToListAsync(cancellationToken);

        var existingSet = existingKeys.ToHashSet(StringComparer.Ordinal);
        var now = DateTimeOffset.UtcNow;
        var inserted = 0;

        foreach (var entry in CatalogEntries)
        {
            if (existingSet.Contains(entry.Key))
            {
                continue;
            }

            await db.QuotaDefinitions.AddAsync(new QuotaDefinition
            {
                Id = Guid.NewGuid(),
                Key = entry.Key,
                Description = entry.Description,
                Unit = entry.Unit,
                Period = entry.Period,
                DefaultValue = entry.DefaultValue,
                Builtin = true,
                CreatedAt = now,
                UpdatedAt = now,
            }, cancellationToken);

            inserted++;
        }

        if (inserted > 0)
        {
            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "QuotaDefinitionSeeder: inserted {Inserted} catalog row(s); catalog now has {Total} entries.",
                inserted,
                existingKeys.Count + inserted);
        }
        else
        {
            logger.LogDebug(
                "QuotaDefinitionSeeder: catalog already populated ({Count} entries); skipping seed.",
                existingKeys.Count);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     One row of the built-in catalog — a struct-shape value
    ///     (auto-properties + ctor) so the foreach above stays a pure
    ///     data iteration with no per-row branching.
    /// </summary>
    /// <param name="Key"></param>
    /// <param name="Description"></param>
    /// <param name="Unit"></param>
    /// <param name="Period"></param>
    /// <param name="DefaultValue"></param>
    private sealed record CatalogEntry(
        string Key,
        string Description,
        QuotaUnit Unit,
        QuotaPeriod Period,
        decimal DefaultValue);
}
