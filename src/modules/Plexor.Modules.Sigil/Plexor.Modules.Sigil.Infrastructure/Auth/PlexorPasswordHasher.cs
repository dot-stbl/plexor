// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PlexorPasswordHasher — wraps PasswordHasher<User> from
// Microsoft.Extensions.Identity.Core. The wrapper narrows the
// dependency to our IPasswordHasher (Application layer) so swapping
// the algorithm later doesn't ripple through every call site.
// ============================================================================

using Microsoft.AspNetCore.Identity;
using Plexor.Modules.Sigil.Application.Auth;
using Plexor.Modules.Sigil.Domain.Entities;
using AppPvr = Plexor.Modules.Sigil.Application.Auth.PasswordVerificationResult;
using MsPvr = Microsoft.AspNetCore.Identity.PasswordVerificationResult;

namespace Plexor.Modules.Sigil.Infrastructure.Auth;

/// <summary>
///     <see cref="IPasswordHasher" /> backed by .NET's built-in
///     <see cref="PasswordHasher{TUser}" />.
/// </summary>
/// <remarks>
///     <para><b>Algorithm.</b> PBKDF2-HMAC-SHA256 with 100,000
///     iterations, 16-byte salt, 32-byte subkey. The
///     <see cref="PasswordHasher{TUser}" /> encodes algorithm
///     version + iteration count + salt + subkey into a single
///     base64 string — round-trip safe across algorithm upgrades
///     (Argon2id can be added later without a migration).</para>
///     <para><b>Thread safety.</b> .NET's
///     <see cref="PasswordHasher{TUser}" /> is stateless and
///     thread-safe. Registered as singleton (3.4.b).</para>
/// </remarks>
public sealed class PlexorPasswordHasher(PasswordHasher<User> inner) : IPasswordHasher
{
    /// <inheritdoc />
    public string HashPassword(User user, string password)
    {
        return inner.HashPassword(user, password);
    }

    /// <inheritdoc />
    public AppPvr VerifyHashedPassword(
        User user,
        string hashedPassword,
        string providedPassword)
    {
        var result = inner.VerifyHashedPassword(user, hashedPassword, providedPassword);
        return result switch
        {
            MsPvr.Failed => AppPvr.Failed,
            MsPvr.Success => AppPvr.Success,
            MsPvr.SuccessRehashNeeded => AppPvr.SuccessRehashNeeded,
            _ => throw new InvalidOperationException(
                $"Unknown PasswordVerificationResult: {result}"),
        };
    }
}

