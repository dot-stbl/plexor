// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IAccountStateGuard — owns the policy for "is this account usable
// right now?". Pulled out of LoginCommandHandler per
// class-decomposition.md (extracted service > private method when
// the method has its own dependencies and a name that survives the
// extraction).
// ============================================================================

using Plexor.Modules.Sigil.Domain.Entities;

namespace Plexor.Modules.Sigil.Infrastructure.Auth;

/// <summary>
///     Account-state policy applied at login. Encapsulates lockout
///     window tracking + counter stamping; the handler stays focused
///     on credential verification + token issuance.
/// </summary>
public interface IAccountStateGuard
{
    /// <summary>
    ///     Throws <c>IdentityException</c> if the user is currently
    ///     locked. Stamps a clear on the row when the window has
    ///     elapsed (so a fresh login attempt can succeed without
    ///     forcing the user to wait out the original duration).
    /// </summary>
    /// <param name="user">Locked-until / status read from the DB.</param>
    /// <param name="cancellationToken">Forwarded to the DB write.</param>
    public Task EnsureNotLockedAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Increment the failed-login counter and stamp
    ///     <c>LockedUntil</c> when the counter crosses the threshold.
    /// </summary>
    /// <param name="userId">Identity of the user that just failed auth.</param>
    /// <param name="cancellationToken">Forwarded to the DB writes.</param>
    public Task RegisterFailedLoginAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Reset the failed-login counter + lockout, stamp
    ///     <c>LastLoginAt</c>.
    /// </summary>
    /// <param name="userId">Identity of the user that just succeeded auth.</param>
    /// <param name="cancellationToken">Forwarded to the DB write.</param>
    public Task RegisterSuccessfulLoginAsync(Guid userId, CancellationToken cancellationToken = default);
}