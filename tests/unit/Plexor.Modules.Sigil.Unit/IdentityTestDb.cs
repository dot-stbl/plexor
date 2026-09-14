// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IdentityTestDb — factory for an isolated in-memory IdentityDbContext.
// Each call mints a fresh database (unique name) so tests don't
// share state. InMemory provider ignores PostgreSQL-specific column
// types (varchar vs text) — fine for the Sigil login / refresh /
// provisioner round-trips; the Postgres UNIQUE constraint on
// (org_id, email) is exercised by the migrator's integration
// tests in a follow-up.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Sigil.Infrastructure.Persistence;

namespace Plexor.Modules.Sigil.Unit;

/// <summary>
///     Test factory for <see cref="IdentityDbContext" />. Each call
///     mints a fresh in-memory database; tests that need to seed
///     users / roles / role-bindings do so against the same
///     instance.
/// </summary>
internal static class IdentityTestDb
{
    /// <summary>
    ///     Create a fresh in-memory <see cref="IdentityDbContext" />.
    ///     The schema is <em>not</em> seeded — the InMemory provider
    ///     can't model the <c>Permissions</c> array + the IReadOnlyList
    ///     <c>Email</c> value-object conversion that the production
    ///     schema uses. Tests that need a real schema must substitute
    ///     a mock DbContext; tests that need only the constructor
    ///     (e.g. IDP-guard paths that throw before any DB roundtrip)
    ///     use this factory and rely on the test mock for the
    ///     downstream queries.
    /// </summary>
    public static Task<IdentityDbContext> CreateAsync()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase($"sigil-test-{Guid.NewGuid():N}")
            .Options;
        return Task.FromResult(new IdentityDbContext(options));
    }
}
