// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EfRefreshTokenOwnerResolver — EF Core binding for
// IRefreshTokenOwnerResolver. Walks refresh_tokens → users and
// role_bindings → roles. Used by RefreshCommandHandler.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Sigil.Application.Auth;
using Plexor.Modules.Sigil.Domain.Entities;
using Plexor.Modules.Sigil.Infrastructure.Persistence;

namespace Plexor.Modules.Sigil.Infrastructure.Auth;

/// <summary>
///     EF Core implementation of <see cref="IRefreshTokenOwnerResolver" />.
///     Looks up the user that owns a rotated refresh token (via the
///     token's hash) and the role names bound to that user.
/// </summary>
/// <param name="db">The Identity module's DbContext.</param>
/// <remarks>
///     <para><b>Why a join, not a navigation property.</b>
///     <see cref="RefreshToken" /> does not declare a navigation to
///     <c>User</c> — tokens are append-mostly and the lookup is
///     one-directional (token → owner). The explicit join keeps the
///     entity surface lean and avoids loading the entire user graph.</para>
/// </remarks>
public sealed class EfRefreshTokenOwnerResolver(IdentityDbContext db) : IRefreshTokenOwnerResolver
{
    /// <inheritdoc />
    public async Task<User?> ResolveByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        return await db.RefreshTokens
            .AsNoTracking()
            .Where(token => token.TokenHash == tokenHash)
            .Join(db.Users, token => token.UserId, user => user.Id, (_, user) => user)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<string>> LoadRoleNamesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await db.RoleBindings
            .AsNoTracking()
            .Where(binding => binding.UserId == userId)
            .Join(
                db.Roles.AsNoTracking(),
                binding => binding.RoleId,
                role => role.Id,
                (_, role) => role.Name)
            .Distinct()
            .ToArrayAsync(cancellationToken);
    }
}
