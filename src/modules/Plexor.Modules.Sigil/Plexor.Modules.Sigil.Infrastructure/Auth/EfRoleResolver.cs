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
}
