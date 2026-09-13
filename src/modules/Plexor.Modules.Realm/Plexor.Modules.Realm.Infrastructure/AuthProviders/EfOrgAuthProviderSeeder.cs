// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EfOrgAuthProviderSeeder — 4.6.1 first-boot seed. EF implementation
// of IOrgAuthProviderSeeder; opens its own scope per call so the
// scoped RealmDbContext lifetime is respected. Idempotent: orgs
// that already have a config row are skipped.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Realm.Application.AuthProviders;
using Plexor.Modules.Realm.Domain.Entities;
using Plexor.Modules.Realm.Infrastructure.Persistence;

namespace Plexor.Modules.Realm.Infrastructure.AuthProviders;

/// <summary>
///     EF-backed implementation of
///     <see cref="IOrgAuthProviderSeeder" />. Reads
///     <c>realm.organizations</c>, computes the set of orgs without
///     a config row, and inserts a default
///     <see cref="OrgAuthProvider.Sigil" /> row per missing org.
/// </summary>
/// <param name="db">Scoped <see cref="RealmDbContext" />.</param>
internal sealed class EfOrgAuthProviderSeeder(RealmDbContext db) : IOrgAuthProviderSeeder
{
    /// <inheritdoc />
    public async Task<int> SeedAllOrgsAsync(CancellationToken cancellationToken)
    {
        var orgIds = await db.Organizations
            .AsNoTracking()
            .Select(static organization => organization.Id)
            .ToListAsync(cancellationToken);

        if (orgIds.Count == 0)
        {
            return 0;
        }

        var existingConfigOrgIds = await db.OrgAuthProviderConfigs
            .AsNoTracking()
            .Select(static config => config.OrgId)
            .ToListAsync(cancellationToken);

        var existingSet = existingConfigOrgIds.ToHashSet();
        var now = DateTimeOffset.UtcNow;
        var inserted = 0;

        foreach (var orgId in orgIds)
        {
            if (existingSet.Contains(orgId))
            {
                continue;
            }

            await db.OrgAuthProviderConfigs.AddAsync(new OrgAuthProviderConfig
            {
                Id = Guid.NewGuid(),
                OrgId = orgId,
                Provider = OrgAuthProvider.Sigil,
                OidcAuthority = null,
                OidcClientId = null,
                OidcClientSecretProtected = null,
                OidcScopes = ["openid", "profile", "email"],
                CreatedAt = now,
                UpdatedAt = now,
            }, cancellationToken);

            inserted++;
        }

        if (inserted > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return inserted;
    }
}
