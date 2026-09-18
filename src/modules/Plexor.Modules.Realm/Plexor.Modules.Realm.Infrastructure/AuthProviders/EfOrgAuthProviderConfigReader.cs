// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EfOrgAuthProviderConfigReader — Phase 4.6.3a EF Core implementation of
// IOrgAuthProviderConfigReader. Read-only lookups against
// realm.org_auth_provider_configs. AsNoTracking reads; scoped lifetime
// (matches RealmDbContext).
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Realm.Application.AuthProviders;
using Plexor.Modules.Realm.Domain.Entities;
using Plexor.Modules.Realm.Infrastructure.Persistence;

namespace Plexor.Modules.Realm.Infrastructure.AuthProviders;

/// <summary>
///     EF Core implementation of
///     <see cref="IOrgAuthProviderConfigReader" />. Uses
///     <see cref="RealmDbContext" /> with <c>AsNoTracking()</c> reads —
///     no change tracker involvement, no per-row state for the writer
///     to confuse on the call site.
/// </summary>
/// <param name="db">Scoped <see cref="RealmDbContext" />.</param>
internal sealed class EfOrgAuthProviderConfigReader(RealmDbContext db) : IOrgAuthProviderConfigReader
{
    /// <inheritdoc />
    public Task<OrgAuthProviderConfig?> GetForOrgAsync(
        Guid orgId,
        CancellationToken cancellationToken = default)
    {
        return db.OrgAuthProviderConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(config => config.OrgId == orgId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<OrgAuthProviderConfig?> GetByOidcIssuerAsync(
        string oidcAuthority,
        CancellationToken cancellationToken = default)
    {
        return db.OrgAuthProviderConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(
                config => config.OidcAuthority == oidcAuthority,
                cancellationToken);
    }
}
