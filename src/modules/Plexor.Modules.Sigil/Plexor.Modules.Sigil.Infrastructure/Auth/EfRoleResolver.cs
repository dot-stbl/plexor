// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EfRoleResolver — IRoleResolver implementation. Joins role_bindings
// to roles and projects to role.Name. Mirrors the existing private
// LoadRolesAsync on AuthCommandHandlers but lifted to its own service
// so the future dispatcher can consume it without depending on a
// command handler.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Sigil.Application.Auth;
using Plexor.Modules.Sigil.Infrastructure.Persistence;

namespace Plexor.Modules.Sigil.Infrastructure.Auth;

/// <summary>
///     LINQ implementation that joins role_bindings to roles and
///     projects to the bound roles' <c>Name</c> collection.
///     <c>AsNoTracking</c> because the read is fire-and-forget (no
///     entity update path). Single roundtrip to PostgreSQL — the join
///     + distinct push to SQL.
/// </summary>
/// <param name="db">Identity module DbContext.</param>
public sealed class EfRoleResolver(IdentityDbContext db) : IRoleResolver
{
    /// <inheritdoc />
    public async Task<IReadOnlyCollection<string>> ResolveAsync(
        Guid userId,
        Guid orgId,
        CancellationToken cancellationToken = default)
    {

        return await db.RoleBindings
            .AsNoTracking()
            .Where(binding => binding.UserId == userId && binding.OrgId == orgId)
            .Join(
                db.Roles.AsNoTracking(),
                binding => binding.RoleId,
                role => role.Id,
                (_, role) => role.Name)
            .Distinct()
            .ToArrayAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<string>> RolesByNamesForOrgAsync(
        Guid orgId,
        IReadOnlyCollection<string> candidateNames,
        CancellationToken cancellationToken = default)
    {
        // Project the candidate set through the Roles table — keeps
        // callers from being able to inject a role from another
        // tenant. Single roundtrip + the underlying Provider is
        // responsible for translating EF's string → text[] semantics
        // (Roles.Name is a plain varchar column, no array trickery).
        var candidates = candidateNames
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (candidates.Length == 0)
        {
            return [];
        }

        return await db.Roles
            .AsNoTracking()
            .Where(role => role.OrgId == orgId && candidates.Contains(role.Name))
            .Select(static role => role.Name)
            .Distinct()
            .ToArrayAsync(cancellationToken);
    }
}
