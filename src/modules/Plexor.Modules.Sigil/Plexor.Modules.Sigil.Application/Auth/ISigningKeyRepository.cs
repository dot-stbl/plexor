// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ISigningKeyRepository — Application-layer contract for SigningKey
// CRUD. JwtSigningService reads public keys here; the bootstrapper
// writes active keys here.
// ============================================================================

using Plexor.Modules.Sigil.Domain.Entities;

namespace Plexor.Modules.Sigil.Application.Auth;

/// <summary>
///     Persistence boundary for <see cref="SigningKey" /> records.
///     JwtSigningService reads <see cref="SigningKey.PublicKeyPem" />
///     to verify signatures; <c>SigningKeyBootstrapper</c> (3.4.a)
///     inserts the first keypair on startup.
/// </summary>
/// <remarks>
///     <para><b>Why a separate repository.</b>
///     <see cref="SigningKey" /> is small, rarely-written, and
///     has its own access pattern (always-by-kid). Keeping it out
///     of the user-store / refresh-store surfaces makes it
///     auditable: a future "list active keys" endpoint only sees
///     this contract.</para>
///     <para><b>Thread safety.</b> Reads are concurrent-safe
///     (no mutation). Writes go through <see cref="AddAsync" />;
///     no update flow in v0.1 — rotation just inserts a new key
///     with a fresh kid.</para>
/// </remarks>
public interface ISigningKeyRepository
{
    /// <summary>
    ///     Read the currently-active signing key (the one whose
    ///     <see cref="SigningKey.NotAfter" /> is <c>null</c>).
    ///     <c>null</c> if no active key exists — caller should
    ///     trigger bootstrap.
    /// </summary>
    /// <param name="cancellationToken">Forwarded to the database.</param>
    public Task<SigningKey?> GetActiveAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Read a key by its <see cref="SigningKey.Kid" />. Used by
    ///     verify-by-kid lookup. <c>null</c> if the kid is unknown.
    /// </summary>
    /// <param name="kid">Key id from the JWT <c>kid</c> header.</param>
    /// <param name="cancellationToken">Forwarded to the database.</param>
    public Task<SigningKey?> GetByKidAsync(
        string kid,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     List every key whose <see cref="SigningKey.NotAfter" />
    ///     is in the future or <c>null</c>. Used by the verifier's
    ///     "kid miss → fallback to scan" path.
    /// </summary>
    /// <param name="cancellationToken">Forwarded to the database.</param>
    public Task<IReadOnlyList<SigningKey>> ListActiveAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Insert a new signing key. Used by the bootstrapper on
    ///     first startup and by the rotation job (Phase 4+).
    ///     </summary>
    /// <param name="key">The key to persist. Caller has already
    ///     generated the keypair and computed the kid.</param>
    /// <param name="cancellationToken">Forwarded to the database.</param>
    public Task AddAsync(
        SigningKey key,
        CancellationToken cancellationToken = default);
}