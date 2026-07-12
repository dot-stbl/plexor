// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EfRefreshTokenStore — IRefreshTokenStore bound to IdentityDbContext.
// All rotation logic is in this single transaction; the auth
// service never needs to peek at token state itself.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Sigil.Application.Auth;
using Plexor.Modules.Sigil.Domain.Entities;
using Plexor.Modules.Sigil.Infrastructure.Persistence;

namespace Plexor.Modules.Sigil.Infrastructure.Auth;

/// <summary>
///     EF Core implementation of <see cref="IRefreshTokenStore" />.
///     Stores only <see cref="RefreshToken.TokenHash" /> (SHA-256);
///     raw tokens are returned to the client on issue/rotate and
///     never persisted.
/// </summary>
/// <remarks>
///     <para><b>Rotation is atomic.</b>
///     <see cref="RotateAsync" /> wraps the read + write + insert
///     in a single transaction. Either the entire rotation chain
///     advances, or nothing changes — no torn state where the old
///     token is revoked but the new one isn't yet visible.</para>
///     <para><b>Replay detection.</b>
///     If the presented token is in the
///     <see cref="RefreshToken.RevokedAt" /> state, we surface
///     <see cref="RefreshRotationResult.Replayed" /> without
///     touching the chain — the auth service then calls
///     <see cref="RevokeFamilyAsync" /> to nuke every token in the
///     compromised family.</para>
/// </remarks>
public sealed class EfRefreshTokenStore(IdentityDbContext db) : IRefreshTokenStore
{
    /// <inheritdoc />
    public async Task<RefreshToken> IssueAsync(
        Guid userId,
        string rawToken,
        DateTimeOffset expiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FamilyId = Guid.NewGuid(),
            TokenHash = RefreshTokenHasher.Hash(rawToken),
            ExpiresAt = expiresAtUtc,
            RevokedAt = null,
            ReplacedBy = null,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        await db.RefreshTokens.AddAsync(entity, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    /// <inheritdoc />
    public async Task<RefreshToken?> FindByRawTokenAsync(
        string rawToken,
        CancellationToken cancellationToken = default)
    {
        var hash = RefreshTokenHasher.Hash(rawToken);
        return await db.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(token => token.TokenHash == hash, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RefreshRotationResult> RotateAsync(
        string rawToken,
        string newRawToken,
        DateTimeOffset newExpiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await db.Database.BeginTransactionAsync(cancellationToken);

        var hash = RefreshTokenHasher.Hash(rawToken);
        var old = await db.RefreshTokens
            .FirstOrDefaultAsync(token => token.TokenHash == hash, cancellationToken);

        if (old is null)
        {
            return RefreshRotationResult.NotFound;
        }

        if (old.RevokedAt is not null)
        {
            // Already rotated or revoked. Caller MUST treat this as
            // a replay attempt and nuke the family.
            return RefreshRotationResult.Replayed;
        }

        var newEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = old.UserId,
            FamilyId = old.FamilyId,
            TokenHash = RefreshTokenHasher.Hash(newRawToken),
            ExpiresAt = newExpiresAtUtc,
            RevokedAt = null,
            ReplacedBy = null,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        await db.RefreshTokens.AddAsync(newEntity, cancellationToken);

        // Mark the old one consumed. Keep ReplacedBy as a
        // back-pointer for audit / chain traversal. Use raw
        // update because RefreshToken has init-only properties
        // (immutable aggregate).
        await db.RefreshTokens
            .Where(token => token.Id == old.Id)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(token => token.RevokedAt, DateTimeOffset.UtcNow)
                    .SetProperty(token => token.ReplacedBy, (Guid?)newEntity.Id),
                cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return RefreshRotationResult.Success;
    }

    /// <inheritdoc />
    public async Task<bool> RevokeAsync(
        string rawToken,
        CancellationToken cancellationToken = default)
    {
        var hash = RefreshTokenHasher.Hash(rawToken);
        var rows = await db.RefreshTokens
            .Where(token => token.TokenHash == hash && token.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(token => token.RevokedAt, DateTimeOffset.UtcNow),
                cancellationToken);
        return rows > 0;
    }

    /// <inheritdoc />
    public async Task<int> RevokeFamilyAsync(
        Guid familyId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        return await db.RefreshTokens
            .Where(token => token.FamilyId == familyId && token.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(token => token.RevokedAt, now),
                cancellationToken);
    }
}
