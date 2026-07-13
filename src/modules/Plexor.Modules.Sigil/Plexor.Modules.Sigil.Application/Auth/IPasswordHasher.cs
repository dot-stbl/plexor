// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IPasswordHasher — abstract over the hashing algorithm so Application
// services don't directly depend on Microsoft.Extensions.Identity.
// Implementation wraps PasswordHasher<User> for PBKDF2-HMAC-SHA256
// (100k iterations, 16-byte salt, 32-byte subkey).
// ============================================================================

using Plexor.Modules.Sigil.Domain.Entities;

namespace Plexor.Modules.Sigil.Application.Auth;

/// <summary>
///     Hashes and verifies user passwords using PBKDF2-HMAC-SHA256.
///     Application services inject this interface instead of
///     <c>Microsoft.AspNetCore.Identity.PasswordHasher&lt;User&gt;</c>
///     directly so the crypto choice can change without rippling
///     through every call site.
/// </summary>
/// <remarks>
///     <para><b>Algorithm.</b> PBKDF2-HMAC-SHA256 with 100,000
///     iterations, 16-byte salt, 32-byte subkey. Encoded by .NET's
///     <see cref="Microsoft.AspNetCore.Identity.PasswordHasher{TUser}" />
///     into a self-describing string
///     (<c>AQAAAAAAAACk...</c>) carrying algorithm version, iteration
///     count, salt, and subkey. Future versions (Argon2id) can be
///     detected and upgraded without a schema change.</para>
///     <para><b>Why not custom.</b> The .NET implementation is
///     maintained, audited, and uses constant-time comparison.
///     A handwritten bcrypt or PBKDF2 is one more thing to get
///     wrong (especially around encoding) for no measurable win.</para>
/// </remarks>
public interface IPasswordHasher
{
    /// <summary>
    ///     Hash a plain-text password for storage on
    ///     <see cref="User.PasswordHash" />. The returned string is
    ///     self-describing (algorithm + salt + subkey embedded).
    /// </summary>
    /// <param name="user">The user this password belongs to. Used by
    ///     the built-in hasher as a personalization seed; passing the
    ///     same user across hash+verify keeps the encoded hash stable.</param>
    /// <param name="password">Plain-text password (UTF-8). Never logged.</param>
    /// <returns>Self-describing hash string. Safe to persist as-is.</returns>
    public string HashPassword(User user, string password);

    /// <summary>
    ///     Verify a plain-text password against a stored hash. Returns
    ///     <see cref="PasswordVerificationResult.Success" /> on match,
    ///     <see cref="PasswordVerificationResult.Failed" /> on
    ///     mismatch, or
    ///     <see cref="PasswordVerificationResult.SuccessRehashNeeded" />
    ///     when the stored hash uses an older algorithm and should be
    ///     upgraded on next successful login.
    /// </summary>
    /// <param name="user">Same user that was passed to
    ///     <see cref="HashPassword" /> when the hash was produced.</param>
    /// <param name="hashedPassword">The stored hash. Read from
    ///     <see cref="User.PasswordHash" />.</param>
    /// <param name="providedPassword">Plain-text password the caller
    ///     just supplied (from <c>POST /auth/login</c>).</param>
    public PasswordVerificationResult VerifyHashedPassword(
        User user,
        string hashedPassword,
        string providedPassword);
}

/// <summary>
///     Tri-state result of <see cref="IPasswordHasher.VerifyHashedPassword" />.
///     Mirrors
///     <see cref="Microsoft.AspNetCore.Identity.PasswordVerificationResult" />
///     so the wrapping layer doesn't leak the underlying enum
///     signature into the Application layer.
/// </summary>
public enum PasswordVerificationResult
{
    /// <summary>Password matched the stored hash.</summary>
    Success = 0,

    /// <summary>Password did not match. Treat as a failed login.</summary>
    Failed = 1,

    /// <summary>Password matched but the hash uses an outdated
    /// algorithm — rehash on next successful login.</summary>
    SuccessRehashNeeded = 2,
}

