// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IOrgAuthProviderSeeder — first-boot seed seam for the per-org
// authentication provider configuration. The implementation lives
// in Plexor.Modules.Realm.Infrastructure; this interface lives in
// Application so hosted services in Application + Infrastructure
// installers can both depend on it without a circular reference.
// ============================================================================

namespace Plexor.Modules.Realm.Application.AuthProviders;

/// <summary>
///     First-boot seed for <see cref="Domain.Entities.OrgAuthProviderConfig" />.
///     Ensures every existing organization has a default
///     <see cref="Domain.Entities.OrgAuthProvider.Sigil" /> row in
///     <c>realm.org_auth_provider_configs</c>; re-runs against an
///     already-seeded fleet are no-ops.
/// </summary>
public interface IOrgAuthProviderSeeder
{
    /// <summary>
    ///     Seed default Sigil rows for every org in
    ///     <c>realm.organizations</c> that doesn't already have a
    ///     config row. Returns the number of rows inserted.
    /// </summary>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    public Task<int> SeedAllOrgsAsync(CancellationToken cancellationToken);
}
