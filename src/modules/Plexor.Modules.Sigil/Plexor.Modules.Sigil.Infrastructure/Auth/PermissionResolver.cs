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
/// <param name="db"></param>
public sealed class PermissionResolver(IdentityDbContext db) : IPermissionResolver
{
    /// <inheritdoc />
    public async Task<IReadOnlyCollection<string>> ResolveAsync(
        Guid userId,
        Guid orgId,
        CancellationToken cancellationToken = default)
    {
        // Materialise the role rows eagerly then flatten the
        // permissions collection in memory. Postgres can flatten
        // text[] in SQL; SQLite (and other providers without native
        // array types) can't translate SelectMany over the value
        // converter. The binding table is org-scoped so the
        // working set is small.
        var bindings = await db.RoleBindings
            .AsNoTracking()
            .Where(binding => binding.UserId == userId && binding.OrgId == orgId)
            .Join(
                db.Roles.AsNoTracking(),
                binding => binding.RoleId,
                role => role.Id,
                (_, role) => role)
            .ToListAsync(cancellationToken);

        return bindings
            .SelectMany(static role => role.Permissions)
            .Select(static scope => scope.Value)
            .Distinct()
            .ToArray();
    }
}
