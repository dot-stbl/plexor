// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EfSigningKeyRepository — ISigningKeyRepository bound to
// IdentityDbContext. Reads public keys for verification; the
// bootstrapper writes the first keypair here.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Sigil.Application.Auth;
using Plexor.Modules.Sigil.Domain.Entities;
using Plexor.Modules.Sigil.Infrastructure.Persistence;

namespace Plexor.Modules.Sigil.Infrastructure.Auth;

/// <summary>
///     EF Core implementation of <see cref="ISigningKeyRepository" />.
///     All reads are <c>AsNoTracking</c> — these rows are
///     append-only and the verifier doesn't mutate them.
/// </summary>
/// <remarks>
///     <para><b>Why no caching here.</b> v0.1 has at most a handful
///     of active keys; a per-request DB hit is cheap. The verifier
///     (Phase 3.6) can layer its own in-memory cache on top — but
///     doing it inside this repo would couple it to the auth
///     pipeline.</para>
///     <para><b>Soft-delete via NotAfter.</b> Rotated keys are not
///     deleted — they live with a non-null
///     <see cref="SigningKey.NotAfter" /> until the access-JWT
///     lifetime (15 min) elapses, so the verifier can still
///     confirm in-flight tokens.</para>
/// </remarks>
public sealed class EfSigningKeyRepository(IdentityDbContext db) : ISigningKeyRepository
{
    /// <inheritdoc />
    public async Task<SigningKey?> GetActiveAsync(
        CancellationToken cancellationToken = default)
    {
        return await db.SigningKeys
            .AsNoTracking()
            .Where(key => key.NotAfter == null)
            .OrderByDescending(key => key.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SigningKey?> GetByKidAsync(
        string kid,
        CancellationToken cancellationToken = default)
    {
        return await db.SigningKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(key => key.Kid == kid, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SigningKey>> ListActiveAsync(
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        return await db.SigningKeys
            .AsNoTracking()
            .Where(key => key.NotAfter == null || key.NotAfter > now)
            .OrderByDescending(key => key.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddAsync(
        SigningKey key,
        CancellationToken cancellationToken = default)
    {
        await db.SigningKeys.AddAsync(key, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }
}