// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EfUserLookup — IUserLookup implementation backed by IdentityDbContext.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Sigil.Application.Users;
using Plexor.Modules.Sigil.Domain.Entities;
using Plexor.Modules.Sigil.Domain.ValueObjects;
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
        // Compare against the Email value object — EF translates
        // the equality through the value converter to a column
        // comparison.
        var emailValue = new Email(email);
        return db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                user => user.OrgId == orgId && user.Email == emailValue,
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task<User?> FindByUsernameAsync(
        Guid orgId,
        string username,
        CancellationToken cancellationToken = default)
    {
        // Username = email local-part. EF Core can't project the
        // value-object property + StartsWith combo through the
        // Email value converter on every provider, so filter
        // org-scoped users first and resolve the prefix in memory.
        // The org-scoped query is indexed (ix_sigil_users_org_id_*)
        // so the working set is small.
        var orgUsers = await db.Users
            .AsNoTracking()
            .Where(user => user.OrgId == orgId)
            .ToListAsync(cancellationToken);
        return orgUsers
            .FirstOrDefault(user => user.Email.Value
                .StartsWith(username + "@", StringComparison.Ordinal));
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
