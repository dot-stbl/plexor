// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PermissionResolver — EF Core implementation of IPermissionResolver.
// Reads role_bindings + roles for a user and unions the bound roles'
// permissions into a deduplicated read-only list.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Sigil.Application.Auth;
using Plexor.Modules.Sigil.Infrastructure.Persistence;

namespace Plexor.Modules.Sigil.Infrastructure.Auth;

/// <summary>
///     LINQ implementation that joins role_bindings to roles and flattens
///     the bound roles' <c>PermissionScope.Value</c> collection into the
///     permission string list. AsNoTracking because the read is
///     fire-and-forget (no entity update path). Single roundtrip to
///     PostgreSQL — the join + select-many pushes the flatten to SQL.
/// </summary>
public sealed class PermissionResolver(IdentityDbContext db) : IPermissionResolver
{
    /// <inheritdoc />
    public async Task<IReadOnlyCollection<string>> ResolveAsync(
        Guid userId,
        Guid orgId,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(userId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(orgId, Guid.Empty);

        return await db.RoleBindings
            .AsNoTracking()
            .Where(binding => binding.UserId == userId && binding.OrgId == orgId)
            .Join(
                db.Roles.AsNoTracking(),
                binding => binding.RoleId,
                role => role.Id,
                (_, role) => role.Permissions)
            .SelectMany(static perms => perms)
            .Select(static scope => scope.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
    }
}
