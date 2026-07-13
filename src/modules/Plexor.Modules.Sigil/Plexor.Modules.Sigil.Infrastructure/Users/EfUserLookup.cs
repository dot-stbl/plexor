// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EfUserLookup — IUserLookup implementation backed by IdentityDbContext.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Sigil.Application.Users;
using Plexor.Modules.Sigil.Domain.Entities;
using Plexor.Modules.Sigil.Infrastructure.Persistence;

namespace Plexor.Modules.Sigil.Infrastructure.Users;

/// <summary>
///     EF Core implementation of <see cref="IUserLookup" />. All queries
///     are AsNoTracking (no update path on this surface) and bounded
///     by tenant via the (org_id, email/username) index.
/// </summary>
/// <param name="db"></param>
public sealed class EfUserLookup(IdentityDbContext db) : IUserLookup
{
    /// <inheritdoc />
    public Task<User?> FindByEmailAsync(
        Guid orgId,
        string email,
        CancellationToken cancellationToken = default)
    {
        return db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                user => user.OrgId == orgId && user.Email.Value == email,
                cancellationToken);
    }

    /// <inheritdoc />
    public Task<User?> FindByUsernameAsync(
        Guid orgId,
        string username,
        CancellationToken cancellationToken = default)
    {
        // Username = email local-part. EF Core translates StartsWith
        // with a literal '@' as a LIKE predicate against the email
        // column — but email is stored validated + lowercased, so a
        // direct prefix match works without culture-sensitive tricks.
        return db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                user => user.OrgId == orgId && user.Email.Value.StartsWith(username + "@"),
                cancellationToken);
    }

    /// <inheritdoc />
    public Task<User?> FindByIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);
    }
}
